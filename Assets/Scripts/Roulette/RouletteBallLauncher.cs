using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public sealed class RouletteBallLauncher : MonoBehaviour, IPathDrawListener
{
    public enum LaunchPhase { Idle, AwaitingGesture, WindingUp, Released }

    [Header("Throw Tuning")]
    [SerializeField, Min(0.01f), Tooltip("Slowest launch speed in world units per second")]
    private float minimumSpeed = 1.5f;
    [SerializeField, Min(0.01f), Tooltip("Fastest launch speed in world units per second")]
    private float maximumSpeed = 4.5f;
    [SerializeField, Min(0.01f), Tooltip("Initial path distance in world units used to aim the throw")]
    private float directionSampleDistance = 0.3f;
    [SerializeField, Range(0f, 1f), Tooltip("Smallest part of the throw aimed toward the wheel centre")]
    private float minimumInwardAmount = 0.05f;
    [SerializeField, Range(0f, 1f), Tooltip("Largest part of the throw aimed toward the wheel centre")]
    private float maximumInwardAmount = 0.65f;
    [SerializeField, Min(0f)] private float windupSeconds = 0.18f;
    [SerializeField, Min(0f)] private float pullbackDistance = 0.12f;
    [SerializeField, Min(0f)] private float liftHeight = 0.04f;
    [SerializeField] private SoundCueSO releaseCue;

    private Rigidbody body;
    private RouletteController owner;
    private RoulettePathDrawer drawer;
    private RouletteWheelMotor wheel;
    private Transform ballStart;
    private Func<bool> isCurrentSpin;
    private GameAudio audioService;
    private bool preparedOnce;
    private float windupAge;
    private Vector3 launchVelocity;

    public Rigidbody Body => body != null ? body : body = GetComponent<Rigidbody>();
    public LaunchPhase Phase { get; private set; }
    public int SpinId { get; private set; }
    public bool IsAwaitingGesture => Phase == LaunchPhase.AwaitingGesture && HasCurrentOwner();
    public event Action<RouletteBallLauncher, int> Released;
    public event Action<RouletteBallLauncher, int> DrawCancelled;
    public event Action<RouletteBallLauncher, int> Invalidated;
    public event Action<RouletteBallLauncher, int, string> InstructionChanged;

    public bool HasAuthoredSetup(int layer)
    {
        var sphere = GetComponent<SphereCollider>();
        if (layer < 0 || !enabled || !gameObject.activeSelf || Body == null || Body.isKinematic ||
            !Body.useGravity || !Body.detectCollisions || Body.includeLayers.value != 0 || Body.excludeLayers.value != 0 ||
            sphere == null || !sphere.enabled || sphere.isTrigger || !FinitePositive(sphere.radius) ||
            sphere.includeLayers.value != 0 || sphere.excludeLayers.value != 0 || sphere.center != Vector3.zero ||
            !RouletteWheelMotor.IsFinite(transform.lossyScale) ||
            (transform.lossyScale - Vector3.one).sqrMagnitude > 0.0001f ||
            GetComponentsInChildren<Rigidbody>(true).Length != 1 || GetComponentsInChildren<Collider>(true).Length != 1 ||
            GetComponentsInChildren<AudioSource>(true).Length != 0 || !HasValidTuning()) return false;
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child.gameObject.layer != layer || !child.CompareTag("Untagged")) return false;
        foreach (MonoBehaviour component in GetComponentsInChildren<MonoBehaviour>(true))
            if (component != this) return false;
        return true;
    }

    private bool HasValidTuning()
    {
        return FinitePositive(minimumSpeed) && FinitePositive(maximumSpeed) && maximumSpeed >= minimumSpeed &&
            FinitePositive(directionSampleDistance) && NonNegative(windupSeconds) && NonNegative(pullbackDistance) &&
            NonNegative(liftHeight) && NonNegative(minimumInwardAmount) && NonNegative(maximumInwardAmount) &&
            minimumInwardAmount <= maximumInwardAmount && maximumInwardAmount < 1f;
    }

    public bool Prepare(RouletteController controller, int spinId, Transform start, RoulettePathDrawer pathDrawer,
        RouletteWheelMotor motor, Func<bool> currentSpin)
    {
        if (preparedOnce || !isActiveAndEnabled || controller == null || !controller.IsOpen || spinId <= 0 ||
            currentSpin == null || !currentSpin() || start == null || motor == null ||
            pathDrawer == null || !pathDrawer.TryValidateSetup(start, out _) ||
            !HasAuthoredSetup(LayerMask.NameToLayer("RouletteBall")) || transform.IsChildOf(motor.transform)) return false;
        owner = controller;
        SpinId = spinId;
        ballStart = start;
        drawer = pathDrawer;
        wheel = motor;
        isCurrentSpin = currentSpin;
        preparedOnce = true;
        Body.linearVelocity = Vector3.zero;
        Body.angularVelocity = Vector3.zero;
        Body.isKinematic = true;
        Body.useGravity = false;
        Body.detectCollisions = false;
        HoldAtStart();
        Phase = LaunchPhase.AwaitingGesture;
        audioService = GameAudio.Instance;
        if (audioService != null) audioService.WorldAudioStopped += Invalidate;
        if (drawer.Begin(this, ballStart)) return true;
        ResetForCleanup();
        return false;
    }

    private bool HasCurrentOwner()
    {
        return isActiveAndEnabled && owner != null && owner.IsOpen && isCurrentSpin != null && isCurrentSpin();
    }

    private bool HasLiveSetup()
    {
        var sphere = GetComponent<SphereCollider>();
        return HasCurrentOwner() && Body != null && ballStart != null && ballStart.gameObject.activeInHierarchy &&
            drawer != null && drawer.IsAvailable && wheel != null && wheel.isActiveAndEnabled && wheel.Body != null &&
            sphere != null && sphere.enabled && !sphere.isTrigger && FinitePositive(sphere.radius) &&
            HasValidTuning() && RouletteWheelMotor.IsFinite(Body.position) &&
            RouletteWheelMotor.IsFinite(ballStart.position) && RouletteWheelMotor.IsFinite(ballStart.forward) &&
            RouletteWheelMotor.IsFinite(ballStart.up);
    }

    public void OnDrawCompleted(List<CurvePoint> path, float drawDuration)
    {
        if (GameplayPause.IsInputBlocked(this)) return;
        if (!IsAwaitingGesture || drawer == null || !drawer.IsCompletingFor(this)) return;
        if (!HasLiveSetup() || !TryGetVelocity(path, drawDuration, out launchVelocity))
        {
            OnDrawCancelled();
            return;
        }
        Phase = LaunchPhase.WindingUp;
        windupAge = 0f;
        drawer.Stop(this);
        InstructionChanged?.Invoke(this, SpinId, "Throwing the ball");
    }

    private bool TryGetVelocity(List<CurvePoint> path, float duration, out Vector3 velocity)
    {
        velocity = default;
        if (path == null || path.Count < 2 || !RouletteWheelMotor.IsFinite(duration) || duration <= 0f) return false;
        Vector3 up = drawer.DrawFrame.up;
        Vector3 heading = Vector3.zero;
        foreach (CurvePoint point in path)
        {
            if (!drawer.Contains(point.position)) return false;
            heading = Vector3.ProjectOnPlane(point.position - path[0].position, up);
            if (heading.magnitude >= directionSampleDistance) break;
        }
        if (heading.sqrMagnitude < 0.0001f || Vector3.Dot(heading, drawer.DrawFrame.forward) <= 0f) return false;
        Vector3 inward = Vector3.ProjectOnPlane(wheel.Body.position - ballStart.position, up);
        if (!RouletteWheelMotor.IsFinite(inward) || inward.sqrMagnitude < 0.0001f) return false;
        inward.Normalize();
        // Keep the throw along the rim with an inward bias
        Vector3 tangent = Vector3.Cross(up, inward).normalized;
        if (Vector3.Dot(tangent, drawer.DrawFrame.forward) < 0f) tangent = -tangent;
        float inwardAmount = Mathf.Clamp(Vector3.Dot(heading.normalized, inward), minimumInwardAmount, maximumInwardAmount);
        Vector3 direction = tangent * Mathf.Sqrt(1f - inwardAmount * inwardAmount) + inward * inwardAmount;
        velocity = direction * Mathf.Lerp(minimumSpeed, maximumSpeed, drawer.GetPower(duration));
        return RouletteWheelMotor.IsFinite(velocity);
    }

    public void OnDrawCancelled()
    {
        if (GameplayPause.IsPaused(this)) return;
        if (!IsAwaitingGesture) return;
        InstructionChanged?.Invoke(this, SpinId, "Draw from the ball to try again");
        if (IsAwaitingGesture) DrawCancelled?.Invoke(this, SpinId);
    }

    public void OnInstructionUpdated(string message)
    {
        if (HasCurrentOwner()) InstructionChanged?.Invoke(this, SpinId, message);
    }

    private void Update()
    {
        if (GameplayPause.IsInputBlocked(this)) return;
        if (Phase == LaunchPhase.Idle) return;
        if (!HasLiveSetup()) { Invalidate(); return; }
        if (Phase == LaunchPhase.WindingUp && Input.GetMouseButtonDown(1)) RetryWindup();
    }

    private void FixedUpdate()
    {
        if (GameplayPause.IsPaused(this)) return;
        if (Phase != LaunchPhase.WindingUp) return;
        if (!HasLiveSetup()) { Invalidate(); return; }
        windupAge += Time.fixedDeltaTime;
        float progress = windupSeconds > 0f ? Mathf.Clamp01(windupAge / windupSeconds) : 1f;
        if (progress >= 1f)
        {
            HoldAtStart();
            Body.isKinematic = false;
            Body.useGravity = true;
            Body.detectCollisions = true;
            Body.linearVelocity = launchVelocity;
            Body.angularVelocity = Vector3.zero;
            Phase = LaunchPhase.Released;
            if (releaseCue != null && !releaseCue.isUI) audioService?.PlayOwned(releaseCue, this, Body.position);
            Released?.Invoke(this, SpinId);
            return;
        }
        float swing = Mathf.Sin(progress * Mathf.PI);
        Vector3 position = ballStart.position - launchVelocity.normalized * (pullbackDistance * swing) +
            drawer.DrawFrame.up * (liftHeight * swing);
        if (!RouletteWheelMotor.IsFinite(position)) { Invalidate(); return; }
        Body.MovePosition(position);
    }

    private void RetryWindup()
    {
        if (GameplayPause.IsInputBlocked(this)) return;
        if (Phase != LaunchPhase.WindingUp || !HasLiveSetup()) return;
        HoldAtStart();
        Phase = LaunchPhase.AwaitingGesture;
        if (!drawer.Begin(this, ballStart)) { Invalidate(); return; }
        OnDrawCancelled();
    }

    private void HoldAtStart()
    {
        Body.position = ballStart.position;
        Body.rotation = ballStart.rotation;
    }

    public void CancelFromDrawer() => Invalidate();

    private void Invalidate()
    {
        if (Phase == LaunchPhase.Idle) return;
        ResetForCleanup();
        Invalidated?.Invoke(this, SpinId);
    }

    public void ResetForCleanup()
    {
        if (Phase == LaunchPhase.Idle) return;
        Phase = LaunchPhase.Idle;
        if (drawer != null) drawer.Stop(this);
        if (Body != null)
        {
            if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
            Body.isKinematic = true;
            Body.useGravity = false;
            Body.detectCollisions = false;
        }
        owner = null;
        isCurrentSpin = null;
        if (audioService != null) audioService.StopOwnedTransients(this);
        if (audioService != null) audioService.WorldAudioStopped -= Invalidate;
        audioService = null;
    }

    private void OnApplicationFocus(bool focused) { if (!focused) RetryWindup(); }
    private void OnApplicationPause(bool paused) { if (paused) RetryWindup(); }
    private void OnDisable() => Invalidate();
    private static bool FinitePositive(float value) => RouletteWheelMotor.IsFinite(value) && value > 0f;
    private static bool NonNegative(float value) => RouletteWheelMotor.IsFinite(value) && value >= 0f;
}
