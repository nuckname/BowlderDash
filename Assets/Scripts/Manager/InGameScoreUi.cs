using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class InGameScoreUi : MonoBehaviour
{
    [Header("UI Text References")]
    public TextMeshProUGUI totalScoreText;
    public TextMeshProUGUI scoreToBeatText;
    public TextMeshProUGUI roundText;
    public TextMeshProUGUI ballsText;
    
    private int _currentDisplayedTickets = 0;
    private Coroutine _ticketAnimateRoutine;
    
    // Tickets
    public TextMeshProUGUI inGameUiTicketsText;
    public Text shopUiTicketsText;

    public static InGameScoreUi Instance;

    private int _currentDisplayedScore = 0;
    private Coroutine _scoreAnimateRoutine;

    private Coroutine _roundTextFlashRoutine;
    
    private void Awake()
    {
        Instance = this;
    }
    
    [Header("Count Audio")]
    // Keep saved cue references while playing the supplied impact clips once per step.
    [SerializeField] private SoundCueSO scoreCountLoopCue;
    [SerializeField] private SoundCueSO ticketCountLoopCue;
    [SerializeField, Range(0.1f, 3f)] private float scoreCountBasePitch = 1f;
    [SerializeField, Range(0.1f, 3f)] private float scoreCountMaxPitch = 2f;
    [SerializeField, Range(0.1f, 3f)] private float ticketCountBasePitch = 1f;
    [SerializeField, Range(0.1f, 3f)] private float ticketCountMaxPitch = 2f;

    [Header("Count Timing")]
    [SerializeField, Min(0.02f)] private float scoreCountStepInterval = 0.075f;

    private GameAudio _countAudioService;
    private SessionData _countSession;
    private bool _countAnimating;
    private uint _countPresentationVersion;
    public uint CountPresentationVersion => _countPresentationVersion;

    private void OnEnable() => BindCountLifecycle();

    private void OnDisable()
    {
        CancelCountAnimations();
        UnbindCountLifecycle();
    }

    private void OnDestroy()
    {
        CancelCountAnimations();
        UnbindCountLifecycle();
        if (Instance == this) Instance = null;
    }

    // Session/world cleanup must cancel the UI coroutine too, so it cannot restart old audio.
    private void BindCountLifecycle()
    {
        if (_countSession == null)
        {
            _countSession = SessionData.Instance;
            _countSession.SessionResetting += CancelCountAnimations;
        }
        if (_countAudioService == GameAudio.Instance) return;
        if (_countAudioService != null) _countAudioService.WorldAudioStopped -= CancelCountAnimations;
        _countAudioService = GameAudio.Instance;
        if (_countAudioService != null) _countAudioService.WorldAudioStopped += CancelCountAnimations;
    }

    private void UnbindCountLifecycle()
    {
        if (_countSession != null) _countSession.SessionResetting -= CancelCountAnimations;
        if (_countAudioService != null) _countAudioService.WorldAudioStopped -= CancelCountAnimations;
        _countSession = null;
        _countAudioService = null;
    }

    public void CancelCountAnimations()
    {
        unchecked { _countPresentationVersion++; }
        if (_scoreAnimateRoutine != null) StopCoroutine(_scoreAnimateRoutine);
        if (_ticketAnimateRoutine != null) StopCoroutine(_ticketAnimateRoutine);
        _scoreAnimateRoutine = null;
        _ticketAnimateRoutine = null;
        _countAnimating = false;
        StopCountAudio();
    }

    private void StopCountAudio()
    {
        _countAudioService?.StopOwnedTransients(this);
    }

    public void RefreshHUD(int totalScore, int currentRound, int currentBalls, RoundScore roundData, int maxRounds)
    {
        if (GameplayPause.IsPaused(this)) return;
        CancelCountAnimations();
        _currentDisplayedScore = totalScore;
        RenderScoreText(_currentDisplayedScore);
        UpdateRoundText(currentRound, maxRounds);
        UpdateScoreToBeatText(roundData.scoreNeededToBeat);
        UpdateBallsText(currentBalls, roundData.maxBallsAllowedToThrow);
    }

    public Coroutine UpdateScoreText(int newScore) => StartCountAnimation(newScore, true);
    public Coroutine UpdateTicketTextAnimated(int newTickets) => StartCountAnimation(newTickets, false);

    public void UpdateTicketText(int newTickets)
    {
        if (GameplayPause.IsPaused(this)) return;
        CancelCountAnimations();
        _currentDisplayedTickets = newTickets;
        RenderTicketText();
    }

    private Coroutine StartCountAnimation(int target, bool isScore)
    {
        if (GameplayPause.IsPaused(this)) return null;
        CancelCountAnimations();
        int current = isScore ? _currentDisplayedScore : _currentDisplayedTickets;
        bool hasText = isScore ? totalScoreText != null : inGameUiTicketsText != null || shopUiTicketsText != null;
        if (!isActiveAndEnabled || !hasText || current == target)
        {
            SetDisplayedCount(target, isScore);
            return null;
        }

        BindCountLifecycle();
        uint version = _countPresentationVersion;
        _countAnimating = true;
        Coroutine routine = StartCoroutine(AnimateCount(target, isScore, version));
        // StartCoroutine may finish synchronously when the current click skips the count.
        if (!_countAnimating || version != _countPresentationVersion) return null;
        if (isScore) _scoreAnimateRoutine = routine;
        else _ticketAnimateRoutine = routine;
        return routine;
    }

    private IEnumerator AnimateCount(int target, bool isScore, uint version)
    {
        while (GameplayPause.IsPaused(this)) yield return null;
        int start = isScore ? _currentDisplayedScore : _currentDisplayedTickets;
        float basePitch = Mathf.Clamp(FiniteCountValue(isScore ? scoreCountBasePitch : ticketCountBasePitch, 1f), 0.1f, 3f);
        float maxPitch = Mathf.Clamp(FiniteCountValue(isScore ? scoreCountMaxPitch : ticketCountMaxPitch, 2f), basePitch, 3f);
        float stepInterval = isScore ? Mathf.Max(0.02f, FiniteCountValue(scoreCountStepInterval, 0.075f)) : 0.05f;
        bool skipAtStart = CountSkipRequested();

        int current = start;
        while (current != target)
        {
            if (version != _countPresentationVersion) yield break;
            if (skipAtStart || CountSkipRequested())
            {
                SetDisplayedCount(target, isScore);
                StopCountAudio();
                break;
            }
            // Use a wider delta to keep the final value correct at integer boundaries.
            long delta = (long)target - current;
            int step = (int)System.Math.Min(System.Math.Abs(delta), isScore ? 2L : 1L);
            current = (int)((long)current + (delta > 0 ? step : -step));
            SetDisplayedCount(current, isScore);
            if (target > start)
            {
                float progress = (float)(((double)current - start) / ((double)target - start));
                _countAudioService?.PlayCountTick(isScore ? scoreCountLoopCue : ticketCountLoopCue,
                    this, Mathf.Lerp(basePitch, maxPitch, progress));
            }

            float timer = 0f;
            bool skip = false;
            while (timer < stepInterval)
            {
                if (version != _countPresentationVersion) yield break;
                if (CountSkipRequested()) { skip = true; break; }
                timer += Time.deltaTime;
                yield return null;
                while (GameplayPause.IsPaused(this)) yield return null;
            }
            if (version != _countPresentationVersion) yield break;
            if (skip)
            {
                SetDisplayedCount(target, isScore);
                StopCountAudio();
                break;
            }
        }

        // A completed count leaves the final hit's tail; skips and cancellation stop it immediately.
        _countAnimating = false;
        _scoreAnimateRoutine = null;
        _ticketAnimateRoutine = null;
    }

    private bool CountSkipRequested() => !GameplayPause.IsInputBlocked(this) &&
        (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1));
    private static float FiniteCountValue(float value, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;

    private void SetDisplayedCount(int value, bool isScore)
    {
        if (isScore)
        {
            _currentDisplayedScore = value;
            RenderScoreText(value);
        }
        else
        {
            _currentDisplayedTickets = value;
            RenderTicketText();
        }
    }

    private void RenderTicketText()
    {
        if (inGameUiTicketsText != null) inGameUiTicketsText.text = $"Tickets: {_currentDisplayedTickets}";
        if (shopUiTicketsText != null) shopUiTicketsText.text = $"Tickets: {_currentDisplayedTickets}";
    }

    public void UpdateRoundText(int currentRound, int maxRounds)
    {
        if (GameplayPause.IsPaused(this)) return;
        roundText.text = $"Rounds:\n{currentRound} / {maxRounds + 1}";
    }
    
    public void UpdateBallsText(int ball, int maxBalls)
    {
        if (GameplayPause.IsPaused(this)) return;
        ballsText.text = $"Balls Thrown:\n{ball} / {maxBalls}";
    }

    public void UpdateScoreToBeatText(int scoreToBeat)
    {
        if (GameplayPause.IsPaused(this)) return;
        scoreToBeatText.text = $"Quota:\n{scoreToBeat}";
    }
    
    private Coroutine _flashRoutine;

    public void UpdateYourScoreText(int currentScore)
    {
        if (GameplayPause.IsPaused(this)) return;
        CancelCountAnimations();
        _currentDisplayedScore = currentScore;
        RenderScoreText(currentScore);
    }

    private void RenderScoreText(int currentScore)
    {
        if (totalScoreText == null) return;
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }
        
        totalScoreText.color = Color.white; // reset color
        totalScoreText.text = $"Your\nScore:\n{currentScore}";
    }

    public void TriggerWinRoundText()
    {
        if (GameplayPause.IsPaused(this)) return;
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashTextRoutine(totalScoreText, "Win\nRound!", Color.black, Color.white));
    }

    public void TriggerGameLostText()
    {
        if (GameplayPause.IsPaused(this)) return;
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        totalScoreText.color = Color.red;
        totalScoreText.text = "Game\nLost :(";
    }
    
    public void TriggerLastThrowText()
    {
        if (GameplayPause.IsPaused(this)) return;
        if (_roundTextFlashRoutine != null) StopCoroutine(_roundTextFlashRoutine);
        
        _roundTextFlashRoutine = StartCoroutine(FlashTextRoutine(ballsText, "LAST\nTHROW", Color.black, Color.white));
    }
    
    public void StopAllFlashing()
    {
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
            totalScoreText.color = Color.white;
        }

        if (_roundTextFlashRoutine != null)
        {
            StopCoroutine(_roundTextFlashRoutine);
            _roundTextFlashRoutine = null;
            ballsText.color = Color.white;
        }
        
        
    }

    private IEnumerator FlashTextRoutine(TextMeshProUGUI textComponent, string textToDisplay, Color flashColor, Color baseColor, float flashInterval = 0.5f)
    {
        while (GameplayPause.IsPaused(this)) yield return null;
        textComponent.text = textToDisplay;
        
        while (true)
        {
            textComponent.color = baseColor;
            yield return new WaitForSeconds(flashInterval);
            while (GameplayPause.IsPaused(this)) yield return null;
            textComponent.color = flashColor;
            yield return new WaitForSeconds(flashInterval);
            while (GameplayPause.IsPaused(this)) yield return null;
        }
    }
    
    // Ai 
    // https://share.gemini.google/AeJz4o3qmN3J
    public Coroutine ResetBallsTextAnimated(int currentBalls, int maxBalls)
    {
        return StartCoroutine(AnimateBallsToZero(currentBalls, maxBalls));
    }

    private IEnumerator AnimateBallsToZero(int currentBalls, int maxBalls)
    {
        while (GameplayPause.IsPaused(this)) yield return null;
        while (currentBalls > 0)
        {
            currentBalls--;
            UpdateBallsText(currentBalls, maxBalls);

            float timer = 0f;
            bool skipTriggered = false;

            while (timer < 0.05f)
            {
                if (CountSkipRequested())
                {
                    skipTriggered = true;
                    break;
                }
                timer += Time.deltaTime;
                yield return null;
                while (GameplayPause.IsPaused(this)) yield return null;
            }

            if (skipTriggered)
            {
                currentBalls = 0;
                UpdateBallsText(currentBalls, maxBalls);
                break;
            }
        }
    }
}
