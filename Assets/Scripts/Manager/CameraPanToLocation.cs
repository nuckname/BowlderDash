using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public enum CameraLocation 
{ 
    PinCameraLocation, 
    BallThrowingCameraLocation, 
    ShopCameraLocation,
    SpawnBossObjectsCameraPosition,
    ScoringCameraLocation,
    
    TicketCameraUi,
    ScoreToBeatUi,
    BallsThrowUi,
    ShowIncreaseRound,
    MainMenuCameraLocation,
    LeaderboardCameraLocation,
    SettingCameraLocation,
    RouletteOverview,
    RouletteRed,
    RouletteBlack,
    RouletteGreen,
    RouletteDraw,
    RouletteSpin,
}

[System.Serializable]
public struct CameraLocationMapping
{
    public CameraLocation location;
    public Transform target;
}

public class CameraPanToLocation : MonoBehaviour
{
    public Camera mainCamera;
    public float lerpSpeed = 3f;

    [SerializeField] private bool showDebugMenu = true;
    
    [Header("Transform Targets")]
    public List<CameraLocationMapping> cameraMappings = new List<CameraLocationMapping>();

    private Coroutine activeLerp;
    private MoveRequest activeRequest;
    private GamePauseController presentationOwner;
    private Camera presentationCamera;
    private Vector3 savedPosition;
    private Quaternion savedRotation;
    private float savedFieldOfView, savedOrthographicSize, savedNearClip, savedFarClip;
    private bool savedOrthographic;
    private Vector3 presentationStartPosition, presentationTargetPosition;
    private Quaternion presentationStartRotation, presentationTargetRotation;
    private float presentationElapsed, presentationDuration;

    internal bool HasPresentationOverride => presentationOwner != null;
    internal bool IsPresentationTransitioning { get; private set; }
    private bool MustWait => HasPresentationOverride || GameplayPause.IsPaused(this);

    internal bool TryBeginPausedPresentation(GamePauseController owner, Transform target, float durationSeconds)
    {
        if (owner == null || !owner.IsPaused || presentationOwner != null || !isActiveAndEnabled ||
            mainCamera == null || target == null || owner.gameObject.scene != gameObject.scene ||
            target.gameObject.scene != gameObject.scene || !IsValidPresentationDuration(durationSeconds)) return false;
        presentationOwner = owner;
        presentationCamera = mainCamera;
        savedPosition = mainCamera.transform.position;
        savedRotation = mainCamera.transform.rotation;
        savedFieldOfView = mainCamera.fieldOfView;
        savedOrthographic = mainCamera.orthographic;
        savedOrthographicSize = mainCamera.orthographicSize;
        savedNearClip = mainCamera.nearClipPlane;
        savedFarClip = mainCamera.farClipPlane;
        BeginPresentationTransition(target.position, target.rotation, durationSeconds);
        return true;
    }

    internal bool TryReturnPausedPresentation(GamePauseController owner, float durationSeconds)
    {
        if (presentationOwner != owner || presentationCamera == null || !IsValidPresentationDuration(durationSeconds)) return false;
        BeginPresentationTransition(savedPosition, savedRotation, durationSeconds);
        return true;
    }

    private static bool IsValidPresentationDuration(float duration) => duration > 0f && !float.IsInfinity(duration);

    private void BeginPresentationTransition(Vector3 position, Quaternion rotation, float duration)
    {
        presentationStartPosition = presentationCamera.transform.position;
        presentationStartRotation = presentationCamera.transform.rotation;
        presentationTargetPosition = position;
        presentationTargetRotation = rotation;
        presentationElapsed = 0f;
        presentationDuration = duration;
        IsPresentationTransitioning = true;
    }

    internal void AdvancePausedPresentation(GamePauseController owner)
    {
        if (presentationOwner != owner || presentationCamera == null || !IsPresentationTransitioning) return;
        presentationElapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(presentationElapsed / presentationDuration);
        float eased = progress * progress * (3f - 2f * progress);
        presentationCamera.transform.SetPositionAndRotation(
            Vector3.Lerp(presentationStartPosition, presentationTargetPosition, eased),
            Quaternion.Slerp(presentationStartRotation, presentationTargetRotation, eased));
        if (progress < 1f) return;
        presentationCamera.transform.SetPositionAndRotation(presentationTargetPosition, presentationTargetRotation);
        IsPresentationTransitioning = false;
    }

    internal void EndPausedPresentation(GamePauseController owner)
    {
        if (presentationOwner != owner) return;
        if (presentationCamera != null)
        {
            presentationCamera.transform.SetPositionAndRotation(savedPosition, savedRotation);
            presentationCamera.fieldOfView = savedFieldOfView;
            presentationCamera.orthographic = savedOrthographic;
            presentationCamera.orthographicSize = savedOrthographicSize;
            presentationCamera.nearClipPlane = savedNearClip;
            presentationCamera.farClipPlane = savedFarClip;
        }
        presentationCamera = null;
        presentationOwner = null;
        IsPresentationTransitioning = false;
    }

    public sealed class MoveRequest
    {
        public bool IsActive { get; internal set; } = true;
        internal Action<bool> onFinished;
    }

    public void MoveCameraTo(CameraLocation location, float speedOverride = -1f)
    {
        TryMoveCameraTo(location, null, out _, speedOverride);
    }

    public bool CanMoveTo(CameraLocation location, float speedOverride = -1f)
    {
        float speed = speedOverride > 0f ? speedOverride : lerpSpeed;
        return isActiveAndEnabled && mainCamera != null && speed > 0f &&
            !float.IsInfinity(speed) && TryGetCameraTarget(location, out _);
    }

    public bool TryMoveCameraTo(CameraLocation location, Action<bool> onFinished,
        out MoveRequest request, float speedOverride = -1f, float durationSeconds = 0f)
    {
        request = null;
        if (!CanMoveTo(location, speedOverride) || float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds))
        {
            Debug.LogWarning($"Cannot move camera to {location}: check camera, target and pan speed.", this);
            return false;
        }

        TryGetCameraTarget(location, out Transform target);
        CancelActiveMove();
        if (activeRequest != null || !CanMoveTo(location, speedOverride)) return false;
        request = new MoveRequest { onFinished = onFinished };
        activeRequest = request;
        float speedToUse = speedOverride > 0f ? speedOverride : lerpSpeed;
        activeLerp = StartCoroutine(durationSeconds > 0f
            ? MoveCameraForDuration(target, durationSeconds, request)
            : LerpCamera(target, speedToUse, request));
        return true;
    }

    public bool TryGetCameraTarget(CameraLocation location, out Transform target)
    {
        foreach (CameraLocationMapping mapping in cameraMappings)
        {
            if (mapping.location == location && mapping.target != null)
            {
                target = mapping.target;
                return true;
            }
        }

        target = null;
        return false;
    }

    public void CancelActiveMove()
    {
        if (activeLerp != null) StopCoroutine(activeLerp);
        activeLerp = null;
        FinishMove(activeRequest, false);
    }

    public void CancelMove(MoveRequest request)
    {
        if (request != null && request == activeRequest) CancelActiveMove();
    }

    private void FinishMove(MoveRequest request, bool arrived)
    {
        if (request == null || !request.IsActive) return;
        request.IsActive = false;
        if (activeRequest == request) activeRequest = null;
        Action<bool> callback = request.onFinished;
        request.onFinished = null;
        callback?.Invoke(arrived);
    }

    private IEnumerator MoveCameraForDuration(Transform target, float duration, MoveRequest request)
    {
        while (MustWait) yield return null;
        // Let the caller retain the request before any arrival callback.
        yield return null;
        while (MustWait) yield return null;
        if (mainCamera == null || target == null)
        {
            activeLerp = null;
            FinishMove(request, false);
            yield break;
        }
        Vector3 startPosition = mainCamera.transform.position;
        Quaternion startRotation = mainCamera.transform.rotation;
        float elapsed = 0f;
        while (mainCamera != null && target != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float remaining = 1f - progress;
            float eased = 1f - remaining * remaining * remaining;
            mainCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, target.position, eased),
                Quaternion.Slerp(startRotation, target.rotation, eased));
            if (progress >= 1f)
            {
                mainCamera.transform.SetPositionAndRotation(target.position, target.rotation);
                activeLerp = null;
                FinishMove(request, true);
                yield break;
            }
            yield return null;
            while (MustWait) yield return null;
        }
        activeLerp = null;
        FinishMove(request, false);
    }

    private IEnumerator LerpCamera(Transform target, float speed, MoveRequest request)
    {
        while (MustWait) yield return null;
        // Defer even an already-arrived move so its caller can retain the request first.
        yield return null;
        while (MustWait) yield return null;
        while (mainCamera != null && target != null)
        {
            if (Vector3.Distance(mainCamera.transform.position, target.position) <= 0.01f &&
                Quaternion.Angle(mainCamera.transform.rotation, target.rotation) <= 0.01f)
            {
                mainCamera.transform.SetPositionAndRotation(target.position, target.rotation);
                activeLerp = null;
                FinishMove(request, true);
                yield break;
            }
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, target.position, Time.deltaTime * speed);
            mainCamera.transform.rotation = Quaternion.Lerp(mainCamera.transform.rotation, target.rotation, Time.deltaTime * speed);
            yield return null;
            while (MustWait) yield return null;
        }
        activeLerp = null;
        FinishMove(request, false);
    }

    private void OnDisable()
    {
        EndPausedPresentation(presentationOwner);
        CancelActiveMove();
    }
    
    // Debug GUI for testing camera locations, turn on showDebugMenu and look top right, just ai slop
    private void OnGUI()
    {
        if (GameplayPause.IsInputBlocked(this)) return;
        // This will hide the entire GUI when set to false in the Inspector or by another script
        if (!showDebugMenu) return;

        GUILayout.BeginArea(new Rect(10, 10, 250, 400));
        
        foreach (CameraLocation location in System.Enum.GetValues(typeof(CameraLocation)))
        {
            if (GUILayout.Button(location.ToString(), GUILayout.Height(30)))
            {
                MoveCameraTo(location);
            }
        }
        
        GUILayout.EndArea();
    }
}
