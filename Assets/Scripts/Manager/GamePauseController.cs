using UnityEngine;

[DisallowMultipleComponent]
public sealed class GamePauseController : MonoBehaviour
{
    [SerializeField] private RoundStateManager roundStateManager;
    private GameAudio audioService;
    private float timeScaleBeforePause;

    public bool IsPaused { get; private set; }
    public bool IsGameplayInputBlocked => IsPaused;
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
        }
    }

    public bool TryPause()
    {
        if (IsPaused) return true;
        if (!CanPause) return false;

        audioService = GameAudio.Instance;
        if (audioService == null || !audioService.TrySuspendGameplay(this)) return false;

        timeScaleBeforePause = Time.timeScale;
        IsPaused = true;
        Time.timeScale = 0f;
        return true;
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
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
        IsPaused = false;
        if (audioService != null) audioService.RestoreGameplaySuspension(this);
        audioService = null;
    }

    private void OnDisable()
    {
        Resume();
        if (roundStateManager != null) roundStateManager.UnregisterPauseController(this);
    }

    private void OnDestroy() => OnDisable();
}
