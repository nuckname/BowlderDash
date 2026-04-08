using UnityEngine;

/// <summary>
/// Simple mode switcher to toggle between bowling controls and pin-placement mode.
/// Disable the controls for the inactive mode by deactivating provided GameObjects or disabling specified Behaviours.
/// Also optionally toggles a HexGridSnapper's gizmo visibility while in placement mode.
/// </summary>
public class BowlingModeSwitcher : MonoBehaviour
{
    /// <summary>
    /// Global indicator that placement mode is active. Draggable/placement components
    /// can check this to enable/disable behavior while the scene has a single
    /// authoritative mode switcher.
    /// </summary>
    public static bool isPlacementMode { get; private set; }

    [Header("Mode Targets")]
    [Tooltip("Container GameObject that holds bowling-related controls / scripts. Will be enabled in bowling mode.")]
    public GameObject bowlingControls;
    [Tooltip("Container GameObject that holds pin-placement UI / scripts. Will be enabled in placement mode.")]
    public GameObject placementControls;

    [Header("Behaviours (optional)")]
    [Tooltip("Individual behaviours to enable in bowling mode (will be disabled in placement mode)")]
    public Behaviour[] bowlingBehaviours;
    [Tooltip("Individual behaviours to enable in placement mode (will be disabled in bowling mode)")]
    public Behaviour[] placementBehaviours;

    [Header("Grid")]
    [Tooltip("Optional HexGridSnapper to toggle gizmos for placement preview")]
    public HexGridSnapper grid;

    [Header("Options")]
    public bool startInBowlingMode = true;
    public KeyCode toggleKey = KeyCode.Tab;

    [Header("Camera Transition (optional)")]
    [Tooltip("Camera to move during transitions. If null, Camera.main will be used.")]
    public Camera targetCamera;
    [Tooltip("Transform representing desired camera position & rotation for pin placement mode.")]
    public Transform placementCameraTarget;
    [Tooltip("Seconds to interpolate camera to/from placement pose.")]
    public float cameraTransitionTime = 1f;

    private bool bowlingMode = true;

    // Camera transition state
    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private Coroutine cameraTransitionCoroutine;

    private void Start()
    {
        bowlingMode = startInBowlingMode;
        // Ensure we capture the camera's 'original' pose after other Start() logic runs
        // (some systems may move the camera in Start). Wait one frame then record,
        // then apply the initial mode to avoid triggering camera transitions back to
        // an uninitialized original pose.
        StartCoroutine(InitAtStart());
    }

    private System.Collections.IEnumerator InitAtStart()
    {
        // wait one frame so other Start() calls that move the camera have executed
        yield return null;
        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam != null)
        {
            originalCameraPosition = cam.transform.position;
            originalCameraRotation = cam.transform.rotation;
        }

        // Now apply the initial mode. Doing this after recording prevents ApplyMode
        // from attempting to transition the camera back to an uninitialized pose.
        ApplyMode(bowlingMode);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) ToggleMode();
    }

    /// <summary>
    /// Toggle between bowling and placement.
    /// </summary>
    public void ToggleMode()
    {
        bowlingMode = !bowlingMode;
        ApplyMode(bowlingMode);
    }

    /// <summary>
    /// Enable bowlingMode=true for bowling controls, false for placement mode.
    /// </summary>
    public void ApplyMode(bool bowling)
    {
        bowlingMode = bowling;
        // update global placement-mode indicator for other components
        isPlacementMode = !bowlingMode;
        if (bowlingControls != null) bowlingControls.SetActive(bowlingMode);
        if (placementControls != null) placementControls.SetActive(!bowlingMode);

        if (bowlingBehaviours != null)
        {
            foreach (var b in bowlingBehaviours)
                if (b != null) b.enabled = bowlingMode;
        }
        if (placementBehaviours != null)
        {
            foreach (var b in placementBehaviours)
                if (b != null) b.enabled = !bowlingMode;
        }

        if (grid != null)
        {
            // show grid gizmos in placement mode for convenience
            grid.showGizmos = !bowlingMode;
        }

        // Camera transition: when entering placement mode, move camera to placementCameraTarget.
        // When leaving placement mode, move back to original pose.
        if (placementCameraTarget != null)
        {
            // resolve camera
            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam != null)
            {
                // stop any existing transition
                if (cameraTransitionCoroutine != null)
                    StopCoroutine(cameraTransitionCoroutine);

                if (!bowlingMode)
                {
                    // entering placement: move from current pose to placement target
                    cameraTransitionCoroutine = StartCoroutine(TransitionCamera(cam.transform, cam.transform.position, cam.transform.rotation, placementCameraTarget.position, placementCameraTarget.rotation, cameraTransitionTime));
                }
                else
                {
                    // returning to bowling: move back to the original pose captured at startup
                    cameraTransitionCoroutine = StartCoroutine(TransitionCamera(cam.transform, cam.transform.position, cam.transform.rotation, originalCameraPosition, originalCameraRotation, cameraTransitionTime));
                }
            }
        }
    }

    private System.Collections.IEnumerator TransitionCamera(Transform camT, Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot, float duration)
    {
        if (duration <= 0f)
        {
            camT.position = toPos;
            camT.rotation = toRot;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            camT.position = Vector3.Lerp(fromPos, toPos, alpha);
            camT.rotation = Quaternion.Slerp(fromRot, toRot, alpha);
            yield return null;
        }

        camT.position = toPos;
        camT.rotation = toRot;
        cameraTransitionCoroutine = null;
    }

    /// <summary>
    /// Convenience API to explicitly enable placement mode.
    /// </summary>
    public void EnablePlacementMode() => ApplyMode(false);

    /// <summary>
    /// Convenience API to explicitly enable bowling mode.
    /// </summary>
    public void EnableBowlingMode() => ApplyMode(true);
}
