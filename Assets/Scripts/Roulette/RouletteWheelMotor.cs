using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
public sealed class RouletteWheelMotor : MonoBehaviour
{
    public enum MotorPhase { Stopped, Accelerating, Holding, Decelerating }

    [Header("Wheel Physics")]
    [Tooltip("The wheel turns around this body using its local axis")]
    [SerializeField] private Vector3 localAxis = Vector3.up;
    [SerializeField] private string wheelLayerName = "RouletteWheel";
    [SerializeField] private string pocketLayerName = "RoulettePocket";
    [SerializeField] private string ballLayerName = "RouletteBall";
    [SerializeField] private RoulettePocket[] pockets = new RoulettePocket[32];

    [Header("Spin Profile")]
    [SerializeField, Min(0.01f), Tooltip("Speed in degrees per second with the axis controlling direction")]
    private float fullSpeed = 90f;
    [SerializeField, Min(0.01f)] private float accelerationSeconds = 2f;
    [SerializeField, Min(0f), Tooltip("Minimum time at full speed while waiting for ball release")]
    private float minimumHoldSeconds = 1f;
    [SerializeField, Min(0.01f)] private float decelerationSeconds = 8f;

    [Header("Wheel Audio (Optional)")]
    [SerializeField] private SoundCueSO wheelLoopCue;
    [SerializeField, Range(0f, 1f)] private float loopVolume = 1f;
    [SerializeField, Range(0.1f, 3f)] private float slowPitch = 0.65f;
    [SerializeField, Range(0.1f, 3f)] private float fullSpeedPitch = 1.1f;

    private Rigidbody body;
    private float phaseSeconds;
    private bool slowdownRequested;
    private bool loopAttempted;
    private bool ambiguousPocketReported;
    private GameAudio audioService;
    private GameAudio.LoopHandle wheelLoop;

    public Rigidbody Body => body != null ? body : body = GetComponent<Rigidbody>();
    public MotorPhase Phase { get; private set; }
    public float SpeedDegreesPerSecond { get; private set; }
    public Vector3 WorldAxis => Body.rotation * localAxis.normalized;
    public bool IsAtFullSpeed => Phase == MotorPhase.Holding;
    public bool IsStopped => Phase == MotorPhase.Stopped;

    public bool TryValidateSetup(out string reason)
    {
        reason = null;
        if (!isActiveAndEnabled || Body == null || !Body.isKinematic || Body.useGravity ||
            !Body.detectCollisions || Body.interpolation != RigidbodyInterpolation.Interpolate ||
            Body.includeLayers.value != 0 || Body.excludeLayers.value != 0 ||
            Body.constraints != RigidbodyConstraints.None ||
            GetComponentsInChildren<Rigidbody>(true).Length != 1 ||
            !IsFinite(Body.position) || !IsFinite(Body.rotation) ||
            !IsFinite(transform.lossyScale) || (transform.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
            reason = "Wheel pivot needs unit world scale and one kinematic, interpolated Rigidbody with gravity off, collisions on, no constraints and no layer overrides.";
        else if (!IsFinite(localAxis) || !IsFinite(localAxis.sqrMagnitude) || localAxis.sqrMagnitude < 0.0001f ||
            !Positive(fullSpeed) || !Positive(accelerationSeconds) || !Positive(decelerationSeconds) ||
            !IsFinite(minimumHoldSeconds) || minimumHoldSeconds < 0f)
            reason = "Wheel axis and spin timings must be finite; speed, acceleration and deceleration must be positive.";
        if (reason != null) return false;

        int wheelLayer = LayerMask.NameToLayer(wheelLayerName);
        int pocketLayer = LayerMask.NameToLayer(pocketLayerName);
        int ballLayer = LayerMask.NameToLayer(ballLayerName);
        int ticketLayer = LayerMask.NameToLayer("RouletteTicket");
        if (wheelLayer < 0 || pocketLayer < 0 || ballLayer < 0 || ticketLayer < 0 ||
            wheelLayer == pocketLayer || wheelLayer == ballLayer || pocketLayer == ballLayer ||
            wheelLayer == ticketLayer || pocketLayer == ticketLayer || ballLayer == ticketLayer ||
            gameObject.layer != wheelLayer || !gameObject.CompareTag("Untagged") ||
            Physics.GetIgnoreLayerCollision(ballLayer, wheelLayer) ||
            Physics.GetIgnoreLayerCollision(ballLayer, pocketLayer) ||
            !Physics.GetIgnoreLayerCollision(ticketLayer, wheelLayer) ||
            !Physics.GetIgnoreLayerCollision(ticketLayer, pocketLayer) ||
            !Physics.GetIgnoreLayerCollision(ticketLayer, ballLayer))
        {
            reason = "Assign distinct roulette wheel/pocket/ball layers: ball sees wheel and pockets; tickets see none of them.";
            return false;
        }

        var ids = new HashSet<int>();
        var seats = new HashSet<Collider>();
        int red = 0, black = 0, green = 0;
        if (pockets == null || pockets.Length != 32 || GetComponentsInChildren<RoulettePocket>(true).Length != 32)
            reason = "Register exactly 32 pocket components under this wheel pivot.";
        else foreach (RoulettePocket pocket in pockets)
        {
            if (pocket == null || !pocket.HasAuthoredSetup(Body, pocketLayer) ||
                !ids.Add(pocket.PocketId) || !seats.Add(pocket.Seat))
            {
                reason = "Every registered pocket needs a unique ID (0-31), colour and enabled child seat trigger owned by this wheel.";
                break;
            }
            if (pocket.Colour == RouletteColour.Red) red++;
            else if (pocket.Colour == RouletteColour.Black) black++;
            else green++;
        }
        if (reason == null && (red != 15 || black != 15 || green != 2))
            reason = "Pocket colours must total 15 red, 15 black and 2 green.";
        if (reason != null) return false;

        int solids = 0;
        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
        {
            if (!collider.enabled || !collider.gameObject.activeInHierarchy || collider.attachedRigidbody != Body ||
                collider.includeLayers.value != 0 || collider.excludeLayers.value != 0 ||
                (collider.isTrigger ? !seats.Contains(collider) : collider.gameObject.layer != wheelLayer))
            {
                reason = "Wheel colliders must be active and owned by its body: solid wheel-layer surfaces or registered pocket triggers.";
                return false;
            }
            if (!collider.isTrigger) solids++;
        }
        if (solids == 0) reason = "Author solid wheel, rim and separator colliders; pocket triggers cannot support the ball.";
        return reason == null;
    }

    public bool BeginSpin()
    {
        if (!IsStopped || !TryValidateSetup(out _)) return false;
        StopAndClear();
        Phase = MotorPhase.Accelerating;
        return true;
    }

    public void RequestSlowdown()
    {
        if (!IsStopped) slowdownRequested = true;
    }

    private void FixedUpdate()
    {
        if (IsStopped) return;
        if (Body == null || !Body.isKinematic || !IsFinite(Body.position) || !IsFinite(Body.rotation) ||
            !IsFinite(localAxis) || !IsFinite(localAxis.sqrMagnitude) || localAxis.sqrMagnitude < 0.0001f ||
            !Positive(fullSpeed) || !Positive(accelerationSeconds) || !Positive(decelerationSeconds) ||
            !IsFinite(minimumHoldSeconds) || minimumHoldSeconds < 0f)
        {
            StopAndClear();
            return;
        }

        float previousSpeed = SpeedDegreesPerSecond;
        phaseSeconds += Time.fixedDeltaTime;
        if (Phase == MotorPhase.Accelerating)
        {
            SpeedDegreesPerSecond = fullSpeed * Mathf.Clamp01(phaseSeconds / accelerationSeconds);
            if (phaseSeconds >= accelerationSeconds) SetPhase(MotorPhase.Holding);
        }
        else if (Phase == MotorPhase.Holding)
        {
            SpeedDegreesPerSecond = fullSpeed;
            if (slowdownRequested && phaseSeconds >= minimumHoldSeconds) SetPhase(MotorPhase.Decelerating);
        }
        else
        {
            SpeedDegreesPerSecond = fullSpeed * (1f - Mathf.Clamp01(phaseSeconds / decelerationSeconds));
        }

        float angle = (previousSpeed + SpeedDegreesPerSecond) * 0.5f * Time.fixedDeltaTime;
        if (!IsFinite(angle))
        {
            StopAndClear();
            return;
        }
        Body.MoveRotation(Body.rotation * Quaternion.AngleAxis(angle, localAxis.normalized));
        if (Phase == MotorPhase.Decelerating && SpeedDegreesPerSecond == 0f) StopAndClear();
        else UpdateWheelAudio();
    }

    private void SetPhase(MotorPhase phase)
    {
        Phase = phase;
        phaseSeconds = 0f;
    }

    public Vector3 GetPointVelocity(Vector3 worldPoint)
    {
        return Vector3.Cross(WorldAxis * (SpeedDegreesPerSecond * Mathf.Deg2Rad), worldPoint - Body.position);
    }

    public bool TryGetPocketCandidate(Rigidbody registeredBall, out RoulettePocket candidate)
    {
        candidate = null;
        if (!isActiveAndEnabled || registeredBall == null ||
            registeredBall.gameObject.layer != LayerMask.NameToLayer(ballLayerName) ||
            registeredBall.isKinematic || !registeredBall.detectCollisions ||
            !IsFinite(registeredBall.linearVelocity) || !IsFinite(registeredBall.angularVelocity) || pockets == null)
            return false;
        foreach (RoulettePocket pocket in pockets)
        {
            if (pocket == null || !pocket.ContainsBallCentre(registeredBall)) continue;
            if (candidate != null)
            {
                candidate = null;
                if (!ambiguousPocketReported)
                    Debug.LogWarning("Roulette ball occupies multiple pocket seats. Check detector overlap; no candidate is selected.", this);
                ambiguousPocketReported = true;
                return false;
            }
            candidate = pocket;
        }
        ambiguousPocketReported = false;
        return candidate != null;
    }

    private void UpdateWheelAudio()
    {
        float amount = Mathf.Clamp01(SpeedDegreesPerSecond / fullSpeed);
        if (!loopAttempted && amount > 0f)
        {
            loopAttempted = true;
            audioService = GameAudio.Instance;
            if (audioService != null)
            {
                audioService.WorldAudioStopped += StopWheelAudio;
                if (wheelLoopCue != null && !wheelLoopCue.isUI && wheelLoopCue.spatialBlend == 0f)
                    wheelLoop = audioService.BeginLoop(wheelLoopCue, this, loopVolume * amount,
                        Mathf.Lerp(slowPitch, fullSpeedPitch, amount));
            }
        }
        if (wheelLoop.IsValid)
            wheelLoop.SetParameters(loopVolume * amount, Mathf.Lerp(slowPitch, fullSpeedPitch, amount));
    }

    private void StopWheelAudio()
    {
        wheelLoop.Stop(0f);
        wheelLoop = default;
        if (audioService != null) audioService.WorldAudioStopped -= StopWheelAudio;
        audioService = null;
    }

    public void StopAndClear()
    {
        SetPhase(MotorPhase.Stopped);
        SpeedDegreesPerSecond = 0f;
        slowdownRequested = false;
        loopAttempted = false;
        ambiguousPocketReported = false;
        StopWheelAudio();
    }

    private void Start()
    {
        if (!TryValidateSetup(out string reason)) Debug.LogWarning($"Roulette wheel is not ready: {reason}", this);
    }

    private void OnDisable() => StopAndClear();

    private static bool Positive(float value) => IsFinite(value) && value > 0f;
    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    private static bool IsFinite(Quaternion value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
}
