using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(-950)]
[DisallowMultipleComponent]
public sealed class GamePauseController : MonoBehaviour
{
    [SerializeField] private RoundStateManager roundStateManager;
    [SerializeField] private PauseMenuUI pauseMenu;
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
    }

    public bool TryOpenPauseMenu()
    {
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
        if (!IsPaused) return;
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
        if (!IsPaused) UpdateFreshInputGuard();
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsPaused) Resume();
            else if (!waitingForFreshInput) TryOpenPauseMenu();
            return;
        }
        if (IsPaused)
        {
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
