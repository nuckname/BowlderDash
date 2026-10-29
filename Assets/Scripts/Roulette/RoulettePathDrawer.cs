using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RoulettePathDrawer : MonoBehaviour
{
    [Header("Table Drawing")]
    [SerializeField] private Transform drawFrame;
    [SerializeField] private BoxCollider drawSurface;
    [SerializeField] private LayerMask drawMask;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private UnityEngine.UI.Slider powerSlider;
    [SerializeField] private Vector2 areaMinimum = new Vector2(-1.5f, -0.5f);
    [SerializeField] private Vector2 areaMaximum = new Vector2(1.5f, 2.5f);
    [SerializeField, Min(0.01f)] private float rayDistance = 250f;
    [SerializeField, Min(0.01f)] private float startRadius = 0.35f;
    [SerializeField, Min(0.01f)] private float pointSpacing = 0.05f;
    [SerializeField, Min(0.01f)] private float minimumPathLength = 0.35f;
    [SerializeField, Min(0.01f)] private float minimumForwardDistance = 0.3f;
    [SerializeField, Min(0f)] private float backtrackTolerance = 0.1f;
    [SerializeField, Range(2, 512)] private int maximumPoints = 128;
    [SerializeField, Min(0f)] private float lineHeight = 0.025f;

    [Header("Draw Timing")]
    [SerializeField, Min(0.01f)] private float minimumDrawSeconds = 0.15f;
    [SerializeField, Min(0.01f), Tooltip("Power stays full until this many seconds have passed")]
    private float fullPowerSeconds = 0.65f;
    [SerializeField, Min(0.01f), Tooltip("Power reaches its minimum after this many seconds")]
    private float maximumDrawSeconds = 1.5f;
    [SerializeField, Min(0f), Tooltip("Seconds allowed to resume drawing after releasing the mouse")]
    private float releaseGraceSeconds = 0.15f;

    [Header("Drawing Audio")]
    [SerializeField] private SoundCueSO drawingLoopCue;
    [SerializeField, Min(0f)] private float audioFadeSeconds = 0.05f;

    private readonly List<CurvePoint> points = new List<CurvePoint>();
    private RouletteBallLauncher activeBall;
    private IPathDrawListener listener;
    private Vector3 startLocal;
    private Vector3 lastLocal;
    private bool drawing;
    private bool completing;
    private bool waitingForRelease;
    private bool audioAttempted;
    private int beginFrame;
    private float drawStarted;
    private float releaseAge;
    private float releasedDuration;
    private float pathLength;
    private GameAudio audioService;
    private GameAudio.LoopHandle drawingLoop;

    public Transform DrawFrame => drawFrame;
    public float CurrentPower { get; private set; }
    public bool IsAvailable => isActiveAndEnabled && drawFrame != null && drawFrame.gameObject.activeInHierarchy &&
        drawSurface != null && drawSurface.enabled && drawSurface.gameObject.activeInHierarchy &&
        targetCamera != null && targetCamera.isActiveAndEnabled && lineRenderer != null &&
        lineRenderer.gameObject.activeInHierarchy;

    public bool TryValidateSetup(Transform ballStart, out string reason)
    {
        reason = null;
        if (!isActiveAndEnabled || drawFrame == null || drawSurface == null || targetCamera == null ||
            !targetCamera.isActiveAndEnabled || lineRenderer == null || !lineRenderer.gameObject.activeInHierarchy ||
            !drawFrame.gameObject.activeInHierarchy || ballStart == null || !ballStart.gameObject.activeInHierarchy ||
            !drawSurface.enabled || !drawSurface.gameObject.activeInHierarchy || !drawSurface.isTrigger ||
            drawSurface.attachedRigidbody != null || !drawSurface.transform.IsChildOf(drawFrame) ||
            !ballStart.IsChildOf(drawFrame) || drawFrame.GetComponentInParent<Rigidbody>() != null ||
            drawFrame.GetComponentInParent<RouletteWheelMotor>() != null ||
            GetComponentInParent<Rigidbody>() != null || GetComponentInParent<RouletteWheelMotor>() != null ||
            lineRenderer.GetComponentInParent<Rigidbody>() != null ||
            !RouletteWheelMotor.IsFinite(drawFrame.lossyScale) ||
            (drawFrame.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
            reason = "Assign an active camera line and stationary unit scale draw frame with a start marker and trigger surface";
        else if (!Positive(rayDistance) || !Positive(startRadius) || !Positive(pointSpacing) ||
            !Positive(minimumPathLength) || !Positive(minimumForwardDistance) ||
            !Positive(minimumDrawSeconds) || !Positive(fullPowerSeconds) || !Positive(maximumDrawSeconds) ||
            fullPowerSeconds < minimumDrawSeconds || maximumDrawSeconds <= fullPowerSeconds ||
            !NonNegative(releaseGraceSeconds) || !NonNegative(backtrackTolerance) ||
            !NonNegative(lineHeight) || !NonNegative(audioFadeSeconds) || maximumPoints < 2 || maximumPoints > 512 ||
            !RouletteWheelMotor.IsFinite(new Vector3(areaMinimum.x, areaMinimum.y, 0f)) ||
            !RouletteWheelMotor.IsFinite(new Vector3(areaMaximum.x, areaMaximum.y, 0f)) ||
            areaMaximum.x <= areaMinimum.x || areaMaximum.y <= areaMinimum.y || !Contains(ballStart.position))
            reason = "Check the drawing bounds distances point budget and timing values";
        if (reason != null) return false;

        int layer = LayerMask.NameToLayer("RouletteDraw");
        if (layer < 0 || drawSurface.gameObject.layer != layer || drawMask.value != (1 << layer) ||
            drawSurface.includeLayers.value != 0 || drawSurface.excludeLayers.value != 0)
            reason = "Use only the RouletteDraw layer for the surface and drawing mask with no layer overrides";
        else for (int other = 0; other < 32; other++)
            if (!Physics.GetIgnoreLayerCollision(layer, other))
            {
                reason = "Disable every collision pair for the raycast only RouletteDraw layer";
                break;
            }
        return reason == null;
    }

    public bool Begin(RouletteBallLauncher ball, Transform ballStart)
    {
        if (ball == null || !ball.IsAwaitingGesture ||
            (activeBall != null && activeBall != ball) || !TryValidateSetup(ballStart, out _)) return false;
        ClearSession();
        activeBall = ball;
        listener = ball;
        startLocal = drawFrame.InverseTransformPoint(ballStart.position);
        startLocal.y = 0f;
        waitingForRelease = true;
        beginFrame = Time.frameCount;
        audioService = GameAudio.Instance;
        if (audioService != null) audioService.WorldAudioStopped += Stop;
        return true;
    }

    public bool IsCompletingFor(RouletteBallLauncher ball) => completing && activeBall == ball;

    public bool Contains(Vector3 worldPoint)
    {
        if (drawFrame == null || !RouletteWheelMotor.IsFinite(worldPoint)) return false;
        Vector3 local = drawFrame.InverseTransformPoint(worldPoint);
        return RouletteWheelMotor.IsFinite(local) && local.x >= areaMinimum.x && local.x <= areaMaximum.x &&
            local.z >= areaMinimum.y && local.z <= areaMaximum.y;
    }

    public float GetPower(float duration)
    {
        return 1f - Mathf.InverseLerp(fullPowerSeconds, maximumDrawSeconds, duration);
    }

    private void Update()
    {
        if (activeBall == null) { if (listener != null) Stop(); return; }
        if (!activeBall.IsAwaitingGesture || !IsAvailable)
        {
            Stop();
            return;
        }
        if (Time.timeScale <= 0f) { if (drawing) CancelGesture(); return; }
        if (waitingForRelease)
        {
            // Finish the previous click before drawing can begin
            if (Time.frameCount > beginFrame && !Input.GetMouseButton(0) && !Input.GetMouseButton(1))
                waitingForRelease = false;
            return;
        }
        if (Input.GetMouseButtonDown(1)) { if (drawing) CancelGesture(); return; }
        if (!drawing)
        {
            if (Input.GetMouseButtonDown(0)) StartGesture();
            return;
        }
        bool reachedBoundary = false;
        if (!ReadPointer(out Vector3 local))
        {
            if (!ReadBoundary(out local)) { CancelGesture(); return; }
            reachedBoundary = true;
        }
        if (Input.GetMouseButton(0))
        {
            if (releaseAge > 0f && Vector3.Distance(local, lastLocal) > startRadius)
            {
                CancelGesture();
                return;
            }
            releaseAge = 0f;
            drawingLoop.SetParameters(1f, 1f, audioFadeSeconds);
            if (local.z < lastLocal.z - backtrackTolerance) { CancelGesture(); return; }
            if (local.z >= lastLocal.z && Vector3.Distance(local, lastLocal) >= pointSpacing)
            {
                if (points.Count >= maximumPoints) { CompleteGesture(Time.time - drawStarted); return; }
                AddPoint(local);
            }
            if (reachedBoundary) { CompleteGesture(Time.time - drawStarted); return; }
            SetPower(GetPower(Time.time - drawStarted));
        }
        else
        {
            if (releaseAge == 0f) releasedDuration = Time.time - drawStarted;
            drawingLoop.SetParameters(0f, 1f, audioFadeSeconds);
            releaseAge += Time.deltaTime;
            if (reachedBoundary || releaseAge >= releaseGraceSeconds) CompleteGesture(releasedDuration);
        }
    }

    private bool ReadPointer(out Vector3 local)
    {
        local = default;
        if (PinInteractionAudio.IsPointerOverUI()) return false;
        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, drawMask, QueryTriggerInteraction.Collide) ||
            hit.collider != drawSurface || !Contains(hit.point)) return false;
        local = drawFrame.InverseTransformPoint(hit.point);
        local.y = 0f;
        return true;
    }

    private bool ReadBoundary(out Vector3 local)
    {
        local = default;
        if (PinInteractionAudio.IsPointerOverUI()) return false;
        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(drawFrame.up, drawFrame.position);
        if (!plane.Raycast(ray, out float distance) || distance > rayDistance) return false;
        local = drawFrame.InverseTransformPoint(ray.GetPoint(distance));
        local.y = 0f;
        if (!RouletteWheelMotor.IsFinite(local) ||
            (local.x >= areaMinimum.x && local.x <= areaMaximum.x &&
             local.z >= areaMinimum.y && local.z <= areaMaximum.y)) return false;

        // Finish at the first edge crossed by the current segment
        Vector3 delta = local - lastLocal;
        if (!RouletteWheelMotor.IsFinite(delta)) return false;
        float fraction = 1f;
        if (local.x < areaMinimum.x) fraction = Mathf.Min(fraction, (areaMinimum.x - lastLocal.x) / delta.x);
        if (local.x > areaMaximum.x) fraction = Mathf.Min(fraction, (areaMaximum.x - lastLocal.x) / delta.x);
        if (local.z < areaMinimum.y) fraction = Mathf.Min(fraction, (areaMinimum.y - lastLocal.z) / delta.z);
        if (local.z > areaMaximum.y) fraction = Mathf.Min(fraction, (areaMaximum.y - lastLocal.z) / delta.z);
        local = lastLocal + delta * Mathf.Clamp01(fraction);

        // Leave room for rounding when the launcher reads world positions
        float insetX = Mathf.Min(0.0001f, (areaMaximum.x - areaMinimum.x) * 0.25f);
        float insetZ = Mathf.Min(0.0001f, (areaMaximum.y - areaMinimum.y) * 0.25f);
        local.x = Mathf.Clamp(local.x, areaMinimum.x + insetX, areaMaximum.x - insetX);
        local.z = Mathf.Clamp(local.z, areaMinimum.y + insetZ, areaMaximum.y - insetZ);
        return true;
    }

    private void StartGesture()
    {
        if (PinInteractionAudio.IsPointerOverUI()) return;
        if (!ReadPointer(out Vector3 local) || Vector3.Distance(local, startLocal) > startRadius)
        {
            CancelGesture();
            return;
        }
        ResetGesture();
        drawing = true;
        drawStarted = Time.time;
        lineRenderer.useWorldSpace = true;
        lineRenderer.enabled = true;
        AddPoint(startLocal);
        if (local.z >= startLocal.z && Vector3.Distance(local, startLocal) >= pointSpacing) AddPoint(local);
        SetPower(1f);
        if (!audioAttempted)
        {
            audioAttempted = true;
            if (audioService != null && drawingLoopCue != null && !drawingLoopCue.isUI && drawingLoopCue.spatialBlend == 0f)
                drawingLoop = audioService.BeginLoop(drawingLoopCue, this, fadeInTime: audioFadeSeconds);
        }
        listener.OnInstructionUpdated("Draw forward and release or cross the edge to throw");
    }

    private void AddPoint(Vector3 local)
    {
        if (points.Count > 0) pathLength += Vector3.Distance(lastLocal, local);
        lastLocal = local;
        Vector3 world = drawFrame.TransformPoint(local);
        points.Add(new CurvePoint { position = world, directionIndex = 0 });
        lineRenderer.positionCount = points.Count;
        lineRenderer.SetPosition(points.Count - 1, world + drawFrame.up * lineHeight);
    }

    private void CompleteGesture(float duration)
    {
        if (points.Count < 2 || pathLength < minimumPathLength ||
            lastLocal.z - startLocal.z < minimumForwardDistance || duration < minimumDrawSeconds ||
            !RouletteWheelMotor.IsFinite(duration))
        {
            CancelGesture();
            return;
        }
        var completed = new List<CurvePoint>(points);
        var receiver = listener;
        ResetGesture();
        waitingForRelease = true;
        completing = true;
        try { receiver?.OnDrawCompleted(completed, duration); }
        finally { completing = false; }
    }

    private void CancelGesture()
    {
        ResetGesture();
        waitingForRelease = true;
        listener?.OnDrawCancelled();
    }

    private void ResetGesture()
    {
        drawing = false;
        completing = false;
        releaseAge = 0f;
        pathLength = 0f;
        points.Clear();
        drawingLoop.Stop(0f);
        drawingLoop = default;
        audioAttempted = false;
        if (lineRenderer != null) { lineRenderer.positionCount = 0; lineRenderer.enabled = false; }
        SetPower(0f);
    }

    private void SetPower(float power)
    {
        CurrentPower = power;
        if (powerSlider != null)
            powerSlider.SetValueWithoutNotify(Mathf.Lerp(powerSlider.minValue, powerSlider.maxValue, power));
    }

    public void Stop()
    {
        var ball = activeBall;
        ClearSession();
        if (ball != null) ball.CancelFromDrawer();
    }

    public void Stop(RouletteBallLauncher ball)
    {
        if (activeBall == ball) ClearSession();
    }

    private void ClearSession()
    {
        activeBall = null;
        listener = null;
        ResetGesture();
        if (audioService != null) audioService.WorldAudioStopped -= Stop;
        audioService = null;
    }

    private void OnApplicationFocus(bool focused) { if (!focused && drawing) CancelGesture(); }
    private void OnApplicationPause(bool paused) { if (paused && drawing) CancelGesture(); }
    private void OnEnable() => ResetGesture();
    private void OnDisable() => Stop();
    private static bool Positive(float value) => RouletteWheelMotor.IsFinite(value) && value > 0f;
    private static bool NonNegative(float value) => RouletteWheelMotor.IsFinite(value) && value >= 0f;
}
