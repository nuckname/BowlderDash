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
    [Tooltip("Primary component that provides bowling controls (enabled in bowling mode).")]
    public DrawBowlManager bowlingControlScript;
    [Tooltip("Primary component that provides placement controls (enabled in placement mode).")]
    public HexGridSnapper placementControlScript;

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

    // Fallback camera state when no camMove is present.
    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private camMove cameraFollowScript;

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
            cameraFollowScript = cam.GetComponent<camMove>();
            originalCameraPosition = cam.transform.position;
            originalCameraRotation = cam.transform.rotation;
        }

        // Only apply initial mode if GameManager isn't driving the state
        if (GameManager.Instance == null)
        {
            ApplyMode(bowlingMode);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (GameManager.Instance == null || GameManager.Instance.CanToggleMode())
            {
                ToggleMode();
                Debug.Log("Mode Toggled");
            }
            else Debug.LogError("Mode toggle attempted but blocked by GameManager.");
        }
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

        // enable/disable the primary control components (prefer component toggles over GameObject activation)
        if (bowlingControlScript != null)
        {
            bowlingControlScript.enabled = bowlingMode;
        }
        if (placementControlScript != null)
        {
            placementControlScript.enabled = !bowlingMode;
        }

        if (grid != null)
        {
            // show grid gizmos in placement mode for convenience
            grid.showGizmos = !bowlingMode;
        }

        var cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam != null)
        {
            if (cameraFollowScript == null)
            {
                cameraFollowScript = cam.GetComponent<camMove>();
            }

            if (cameraFollowScript != null)
            {
                cameraFollowScript.SetPlacementMode(!bowlingMode, placementCameraTarget, cameraTransitionTime);
            }
            else if (placementCameraTarget != null)
            {
                if (!bowlingMode)
                {
                    cam.transform.SetPositionAndRotation(placementCameraTarget.position, placementCameraTarget.rotation);
                }
                else
                {
                    cam.transform.SetPositionAndRotation(originalCameraPosition, originalCameraRotation);
                }
            }
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnModeChanged(bowlingMode);
        }
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
