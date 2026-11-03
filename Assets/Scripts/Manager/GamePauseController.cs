using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-950)]
[DisallowMultipleComponent]
public sealed class GamePauseController : MonoBehaviour
{
    [SerializeField] private RoundStateManager roundStateManager;
    [SerializeField] private PauseMenuUI pauseMenu;
    [SerializeField] private GameObject settingsRoot;
    [SerializeField] private UnityEngine.UI.Button settingsBackButton;
    [SerializeField, Min(0.05f)] private float optionsTransitionSeconds = 0.65f;
    private CameraPanToLocation optionsPan;
    private CameraFollowBallScript optionsFollow;
    private bool settingsWereActive;
    private Transform settingsParent;
    private Vector3 settingsLocalPosition, settingsLocalScale;
    private Quaternion settingsLocalRotation;
    private int settingsSiblingIndex;
    private bool settingsWereReparented;
    private bool optionsInputReady;
    private bool returningFromOptions;
    private int optionsOpenedFrame, optionsReleasedFrame;
    private readonly Dictionary<UnityEngine.UI.Selectable, bool> settingsSelectableStates = new Dictionary<UnityEngine.UI.Selectable, bool>();
    private readonly Dictionary<UnityEngine.UI.GraphicRaycaster, bool> settingsRaycasterStates = new Dictionary<UnityEngine.UI.GraphicRaycaster, bool>();
    private readonly List<SettingsGroupState> settingsGroupStates = new List<SettingsGroupState>();
    private readonly Dictionary<UnityEngine.UI.Graphic, bool> optionsGraphicStates = new Dictionary<UnityEngine.UI.Graphic, bool>();

    private struct SettingsGroupState
    {
        public CanvasGroup group;
        public float alpha;
        public bool interactable, blocksRaycasts;
    }
    private GameAudio audioService;
    private float timeScaleBeforePause;
    private bool waitingForFreshInput;
    private int resumeFrame;
    private int releasedFrame = -1;
    private GameObject previousSelection;
    private CursorLockMode cursorLockBeforePause;
    private bool cursorVisibleBeforePause;
    private bool ownsMenuCursor;
    private readonly List<Transform> menuInputRoots = new List<Transform>();
    private readonly Dictionary<UnityEngine.UI.Selectable, bool> selectableStates = new Dictionary<UnityEngine.UI.Selectable, bool>();
    private readonly Dictionary<UnityEngine.UI.GraphicRaycaster, bool> raycasterStates = new Dictionary<UnityEngine.UI.GraphicRaycaster, bool>();

    public bool IsPaused { get; private set; }
    public bool IsShowingOptions { get; private set; }
    public bool IsGameplayInputBlocked => IsPaused || waitingForFreshInput;
    public bool CanPause => isActiveAndEnabled && roundStateManager != null &&
        roundStateManager.PauseController == this && roundStateManager.CanPauseGameplay;
    internal float TimeScaleBeforePause => timeScaleBeforePause;

    private void OnEnable()
    {
        if (roundStateManager == null) roundStateManager = GetComponent<RoundStateManager>();
        if (roundStateManager == null || !roundStateManager.TryRegisterPauseController(this))
        {
            Debug.LogError("Assign one local RoundStateManager to GamePauseController", this);
            enabled = false;
            return;
        }
        if (pauseMenu != null) pauseMenu.Initialize(this);
        if (settingsRoot != null) RegisterMenuInputRoot(settingsRoot.transform);
    }

    public bool TryOpenPauseMenu()
    {
        if (IsPaused) return !IsShowingOptions;
        if (!CanPause) return false;
        if (pauseMenu == null || !pauseMenu.Initialize(this))
        {
            Debug.LogError("Assign the PauseMenu view, panel and four buttons on GamePauseController", this);
            return false;
        }
        if (!TryPause()) return false;
        if (!ownsMenuCursor)
        {
            cursorLockBeforePause = Cursor.lockState;
            cursorVisibleBeforePause = Cursor.visible;
            ownsMenuCursor = true;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        pauseMenu.Show();
        return true;
    }

    public bool TryOpenOptions()
    {
        if (!IsPaused || IsShowingOptions || !CanPause || pauseMenu == null || !pauseMenu.IsVisible) return false;
        var pan = roundStateManager.cameraPanToLocation;
        var follow = roundStateManager.cameraFollowScript;
        if (settingsRoot == null || settingsRoot.scene != gameObject.scene ||
            settingsBackButton == null || !settingsBackButton.enabled || !settingsBackButton.transform.IsChildOf(settingsRoot.transform) ||
            pan == null || pan.gameObject.scene != gameObject.scene || !pan.isActiveAndEnabled ||
            pan.mainCamera == null || pan.mainCamera.gameObject.scene != gameObject.scene ||
            follow == null || follow.gameObject.scene != gameObject.scene || follow.targetCamera != pan.mainCamera ||
            !pan.TryGetCameraTarget(CameraLocation.SettingCameraLocation, out var target) ||
            target.gameObject.scene != gameObject.scene)
        {
            Debug.LogError("Assign local SettingTvs, its Back button, camera pan/follow and SettingCameraLocation for paused Options", this);
            return false;
        }
        for (var part = settingsBackButton.transform; part != settingsRoot.transform; part = part.parent)
        {
            if (part.gameObject.activeSelf) continue;
            Debug.LogError("Enable the settings Back button and its parents inside SettingTvs before opening paused Options", this);
            return false;
        }
        if (!pan.TryBeginPausedPresentation(this, target, optionsTransitionSeconds))
        {
            Debug.LogError("Paused Options needs an available camera and a finite positive transition duration", this);
            return false;
        }
        if (!follow.TryBeginPausedPresentation(this, pan.mainCamera) || !roundStateManager.TryBeginPausedOptions(this))
        {
            follow.EndPausedPresentation(this);
            pan.EndPausedPresentation(this);
            Debug.LogError("Paused Options could not acquire its local navigation and camera context", this);
            return false;
        }
        optionsPan = pan;
        optionsFollow = follow;
        RegisterMenuInputRoot(settingsRoot.transform);
        settingsWereActive = settingsRoot.activeSelf;
        settingsParent = settingsRoot.transform.parent;
        settingsWereReparented = settingsParent != null && !settingsParent.gameObject.activeInHierarchy;
        if (settingsWereReparented)
        {
            settingsLocalPosition = settingsRoot.transform.localPosition;
            settingsLocalRotation = settingsRoot.transform.localRotation;
            settingsLocalScale = settingsRoot.transform.localScale;
            settingsSiblingIndex = settingsRoot.transform.GetSiblingIndex();
            var visibleParent = settingsParent.parent;
            while (visibleParent != null && !visibleParent.gameObject.activeInHierarchy) visibleParent = visibleParent.parent;
            settingsRoot.transform.SetParent(visibleParent, true);
        }
        foreach (var control in settingsRoot.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
        {
            settingsSelectableStates[control] = control.interactable;
            control.interactable = false;
        }
        foreach (var raycaster in settingsRoot.GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>(true))
        {
            settingsRaycasterStates[raycaster] = raycaster.enabled;
            raycaster.enabled = false;
        }
        foreach (var group in settingsRoot.GetComponentsInChildren<CanvasGroup>(true))
        {
            settingsGroupStates.Add(new SettingsGroupState { group = group, alpha = group.alpha,
                interactable = group.interactable, blocksRaycasts = group.blocksRaycasts });
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
        IsShowingOptions = true;
        optionsInputReady = false;
        returningFromOptions = false;
        optionsOpenedFrame = Time.frameCount;
        optionsReleasedFrame = -1;
        pauseMenu.Hide();
        settingsRoot.SetActive(true);
        foreach (var graphic in FindObjectsByType<UnityEngine.UI.Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (graphic.gameObject.scene != gameObject.scene || IsMenuInput(graphic.transform)) continue;
            optionsGraphicStates[graphic] = graphic.enabled;
            graphic.enabled = false;
        }
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        return true;
    }

    public void OnResetRunRequested() => TryReloadRun(true);
    public void OnMainMenuRequested() => TryReloadRun(false);

    private void TryReloadRun(bool startNewRun)
    {
        if (!IsPaused || IsShowingOptions || pauseMenu == null || !pauseMenu.IsVisible || roundStateManager == null) return;
        roundStateManager.TryReloadFromPause(this, startNewRun);
    }

    internal void ReleaseForSceneReload()
    {
        if (!IsPaused) return;
        RestoreOptionsPresentation();
        if (pauseMenu != null) pauseMenu.Hide();
        RestoreMenuCursor();
        IsPaused = false;
        waitingForFreshInput = false;
        RestoreGameplayUI(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (audioService != null) audioService.RestoreGameplaySuspension(this);
        audioService = null;
    }

    internal void GuardInputAfterSceneReload()
    {
        waitingForFreshInput = true;
        resumeFrame = Time.frameCount;
        releasedFrame = -1;
        SuspendGameplayUI();
        RestoreSelectableStates();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public void TryReturnFromOptions()
    {
        if (IsPaused && IsShowingOptions && optionsInputReady) BeginOptionsReturn();
    }

    private void BeginOptionsReturn()
    {
        if (returningFromOptions) return;
        returningFromOptions = true;
        optionsInputReady = false;
        foreach (var state in settingsSelectableStates) if (state.Key != null) state.Key.interactable = false;
        foreach (var state in settingsRaycasterStates) if (state.Key != null) state.Key.enabled = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (optionsPan == null || !optionsPan.TryReturnPausedPresentation(this, optionsTransitionSeconds)) Resume();
    }

    private void RestoreOptionsPresentation()
    {
        if (!IsShowingOptions) return;
        IsShowingOptions = false;
        optionsInputReady = false;
        returningFromOptions = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (settingsRoot != null)
        {
            if (settingsWereReparented && settingsParent != null)
            {
                settingsRoot.transform.SetParent(settingsParent, false);
                settingsRoot.transform.localPosition = settingsLocalPosition;
                settingsRoot.transform.localRotation = settingsLocalRotation;
                settingsRoot.transform.localScale = settingsLocalScale;
                settingsRoot.transform.SetSiblingIndex(settingsSiblingIndex);
            }
            settingsRoot.SetActive(settingsWereActive);
        }
        settingsParent = null;
        settingsWereReparented = false;
        foreach (var state in settingsSelectableStates) if (state.Key != null) state.Key.interactable = state.Value;
        foreach (var state in settingsRaycasterStates) if (state.Key != null) state.Key.enabled = state.Value;
        foreach (var state in settingsGroupStates)
        {
            if (state.group == null) continue;
            state.group.alpha = state.alpha;
            state.group.interactable = state.interactable;
            state.group.blocksRaycasts = state.blocksRaycasts;
        }
        settingsSelectableStates.Clear();
        settingsRaycasterStates.Clear();
        settingsGroupStates.Clear();
        foreach (var state in optionsGraphicStates) if (state.Key != null) state.Key.enabled = state.Value;
        optionsGraphicStates.Clear();
        if (optionsPan != null) optionsPan.EndPausedPresentation(this);
        if (optionsFollow != null) optionsFollow.EndPausedPresentation(this);
        optionsPan = null;
        optionsFollow = null;
        if (roundStateManager != null) roundStateManager.EndPausedOptions(this);
    }

    private void UpdateOptionsInput()
    {
        if (optionsPan.IsPresentationTransitioning) return;
        if (!optionsInputReady && Time.frameCount > optionsOpenedFrame)
        {
            if (optionsReleasedFrame >= 0 && Time.frameCount > optionsReleasedFrame)
            {
                optionsInputReady = true;
                foreach (var state in settingsSelectableStates)
                    if (state.Key != null) state.Key.interactable = state.Key == settingsBackButton || state.Key is UnityEngine.UI.Slider;
                foreach (var state in settingsRaycasterStates) if (state.Key != null) state.Key.enabled = true;
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(settingsBackButton.gameObject);
            }
            else if (!Input.anyKey && !Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2))
                optionsReleasedFrame = Time.frameCount;
        }
        if (!optionsInputReady || EventSystem.current == null) return;
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || !selected.transform.IsChildOf(settingsRoot.transform) ||
            !selected.TryGetComponent<UnityEngine.UI.Selectable>(out var control) || !control.IsActive() || !control.IsInteractable())
            EventSystem.current.SetSelectedGameObject(settingsBackButton.gameObject);
    }

    public bool TryPause()
    {
        if (IsPaused) return true;
        if (!CanPause) return false;

        audioService = GameAudio.Instance;
        if (audioService == null || !audioService.TrySuspendGameplay(this)) return false;

        timeScaleBeforePause = Time.timeScale;
        waitingForFreshInput = false;
        releasedFrame = -1;
        roundStateManager.PrepareForPause();
        SuspendGameplayUI();
        IsPaused = true;
        Time.timeScale = 0f;
        return true;
    }

    public void Resume()
    {
        if (!IsPaused || (roundStateManager != null && roundStateManager.IsSceneReloadPending)) return;
        RestoreOptionsPresentation();
        if (pauseMenu != null) pauseMenu.Hide();
        RestoreMenuCursor();
        IsPaused = false;
        waitingForFreshInput = true;
        resumeFrame = Time.frameCount;
        releasedFrame = -1;
        RestoreSelectableStates();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (roundStateManager == null || !roundStateManager.OwnsLeaderboardPause)
        {
            if (Time.timeScale == 0f) Time.timeScale = timeScaleBeforePause;
        }
        if (audioService != null) audioService.RestoreGameplaySuspension(this);
        audioService = null;
    }

    internal void HandoffToLeaderboard()
    {
        if (roundStateManager == null || !roundStateManager.OwnsLeaderboardPause) return;
        RestoreOptionsPresentation();
        if (pauseMenu != null) pauseMenu.Hide();
        RestoreMenuCursor();
        IsPaused = false;
        waitingForFreshInput = false;
        RestoreGameplayUI(false);
        if (audioService != null) audioService.RestoreGameplaySuspension(this);
        audioService = null;
    }

    private void OnDisable()
    {
        Resume();
        if (pauseMenu != null) pauseMenu.Hide();
        RestoreMenuCursor();
        waitingForFreshInput = false;
        RestoreGameplayUI(true);
        if (roundStateManager != null) roundStateManager.UnregisterPauseController(this);
    }

    private void Update()
    {
        if (roundStateManager != null && roundStateManager.IsSceneReloadPending) return;
        if (!IsPaused) UpdateFreshInputGuard();
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsShowingOptions) BeginOptionsReturn();
            else if (IsPaused) Resume();
            else if (!waitingForFreshInput) TryOpenPauseMenu();
            return;
        }
        if (IsPaused)
        {
            if (IsShowingOptions)
            {
                if (settingsRoot == null || !settingsRoot.activeInHierarchy || settingsBackButton == null ||
                    !settingsBackButton.isActiveAndEnabled || optionsPan == null || !optionsPan.isActiveAndEnabled || optionsPan.mainCamera == null ||
                    !optionsPan.HasPresentationOverride || optionsFollow == null)
                {
                    Resume();
                    return;
                }
                optionsPan.AdvancePausedPresentation(this);
                if (returningFromOptions)
                {
                    if (!optionsPan.IsPresentationTransitioning) Resume();
                    return;
                }
                UpdateOptionsInput();
                return;
            }
            if (ownsMenuCursor && (pauseMenu == null || !pauseMenu.isActiveAndEnabled || !pauseMenu.IsVisible))
            {
                Resume();
                return;
            }
            if (pauseMenu != null && pauseMenu.isActiveAndEnabled) pauseMenu.EnsureSelection();
        }
    }

    private void UpdateFreshInputGuard()
    {
        if (!waitingForFreshInput || Time.frameCount <= resumeFrame) return;
        if (releasedFrame >= 0 && Time.frameCount > releasedFrame)
        {
            waitingForFreshInput = false;
            RestoreGameplayUI(true);
            return;
        }
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (Input.anyKey || Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2)) return;
        releasedFrame = Time.frameCount;
    }

    private void RestoreMenuCursor()
    {
        if (!ownsMenuCursor) return;
        Cursor.lockState = cursorLockBeforePause;
        Cursor.visible = cursorVisibleBeforePause;
        ownsMenuCursor = false;
    }

    public void RegisterMenuInputRoot(Transform root)
    {
        if (root != null && root.gameObject.scene == gameObject.scene && !menuInputRoots.Contains(root))
        {
            menuInputRoots.Add(root);
            foreach (var selectable in new List<UnityEngine.UI.Selectable>(selectableStates.Keys))
            {
                if (selectable == null || !IsMenuInput(selectable.transform)) continue;
                selectable.interactable = selectableStates[selectable];
                selectableStates.Remove(selectable);
            }
            foreach (var raycaster in new List<UnityEngine.UI.GraphicRaycaster>(raycasterStates.Keys))
            {
                if (raycaster == null || !IsMenuInput(raycaster.transform)) continue;
                raycaster.enabled = raycasterStates[raycaster];
                raycasterStates.Remove(raycaster);
            }
        }
    }

    private bool IsMenuInput(Transform target)
    {
        foreach (var root in menuInputRoots)
            if (root != null && (target == root || target.IsChildOf(root))) return true;
        return false;
    }

    private void SuspendGameplayUI()
    {
        if (EventSystem.current != null)
        {
            if (previousSelection == null) previousSelection = EventSystem.current.currentSelectedGameObject;
            EventSystem.current.SetSelectedGameObject(null);
        }
        foreach (var selectable in FindObjectsByType<UnityEngine.UI.Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (selectable.gameObject.scene != gameObject.scene || IsMenuInput(selectable.transform)) continue;
            selectableStates[selectable] = selectable.interactable;
            selectable.interactable = false;
        }
        foreach (var raycaster in FindObjectsByType<UnityEngine.UI.GraphicRaycaster>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (raycaster.gameObject.scene != gameObject.scene || IsMenuInput(raycaster.transform)) continue;
            if (!raycasterStates.ContainsKey(raycaster)) raycasterStates[raycaster] = raycaster.enabled;
            raycaster.enabled = false;
        }
    }

    private void RestoreSelectableStates()
    {
        foreach (var state in selectableStates)
            if (state.Key != null) state.Key.interactable = state.Value;
        selectableStates.Clear();
    }

    private void RestoreGameplayUI(bool restoreSelection)
    {
        RestoreSelectableStates();
        foreach (var state in raycasterStates)
            if (state.Key != null) state.Key.enabled = state.Value;
        raycasterStates.Clear();
        if (restoreSelection && previousSelection != null && previousSelection.activeInHierarchy &&
            EventSystem.current != null && previousSelection.TryGetComponent<UnityEngine.UI.Selectable>(out var selected) &&
            selected.IsActive() && selected.IsInteractable())
            EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection = null;
    }

    private void OnDestroy() => OnDisable();
}
