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
    
    // Update score UI with default values, for starting a new round when we win the frame/round
    public void RefreshHUD(int totalScore, int currentRound, int currentBalls, RoundScore roundData, int maxRounds)
    {
        _currentDisplayedScore = totalScore;
        UpdateYourScoreText(_currentDisplayedScore);
        UpdateRoundText(currentRound, maxRounds);
        UpdateScoreToBeatText(roundData.scoreNeededToBeat);
        UpdateBallsText(currentBalls, roundData.maxBallsAllowedToThrow);
    }
    
    public Coroutine UpdateScoreText(int newScore)
    {
        if (_scoreAnimateRoutine != null)
        {
            StopCoroutine(_scoreAnimateRoutine);
            _scoreAnimateRoutine = null;
        }

        if (_currentDisplayedScore == newScore)
        {
            UpdateYourScoreText(_currentDisplayedScore);
            return null;
        }

        _scoreAnimateRoutine = StartCoroutine(AnimateScoreByTwos(newScore));
        return _scoreAnimateRoutine;
    }

    private IEnumerator AnimateScoreByTwos(int targetScore)
    {
        while (_currentDisplayedScore != targetScore)
        {
            if (_currentDisplayedScore < targetScore)
            {
                _currentDisplayedScore += 2;
                if (_currentDisplayedScore > targetScore)
                {
                    _currentDisplayedScore = targetScore;
                }
            }
            else
            {
                _currentDisplayedScore -= 2;
                if (_currentDisplayedScore < targetScore)
                {
                    _currentDisplayedScore = targetScore;
                }
            }
            
            UpdateYourScoreText(_currentDisplayedScore);

            float timer = 0f;
            bool skipTriggered = false;
            
            while (timer < 0.05f)
            {
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                {
                    skipTriggered = true;
                    break;
                }
                timer += Time.deltaTime;
                yield return null;
            }

            if (skipTriggered)
            {
                _currentDisplayedScore = targetScore;
                UpdateYourScoreText(_currentDisplayedScore);
                break;
            }
        }

        _scoreAnimateRoutine = null;
    }
    
    // Might need to extract this to a new class
    private IEnumerator AnimateTicketsByOne(int targetTickets)
    {
        while (_currentDisplayedTickets != targetTickets)
        {
            if (_currentDisplayedTickets < targetTickets)
            {
                _currentDisplayedTickets += 1;
            }
            else
            {
                _currentDisplayedTickets -= 1;
            }
            
            // In Game
            inGameUiTicketsText.text = $"Tickets: {_currentDisplayedTickets}";
            
            // In Shop
            if (shopUiTicketsText != null)
            {
                shopUiTicketsText.text = $"Tickets: {_currentDisplayedTickets}";
            }
            
            float timer = 0f;
            bool skipTriggered = false;
            
            while (timer < 0.05f)
            {
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                {
                    skipTriggered = true;
                    break;
                }
                timer += Time.deltaTime;
                yield return null;
            }

            if (skipTriggered)
            {
                _currentDisplayedTickets = targetTickets;
                
                // In Game
                inGameUiTicketsText.text = $"Tickets: {_currentDisplayedTickets}";
                
                // In Shop
                if (shopUiTicketsText != null)
                {
                    shopUiTicketsText.text = $"Tickets: {_currentDisplayedTickets}";
                }
                
                break;
            }
        }

        _ticketAnimateRoutine = null;
    }
    
    public void UpdateTicketText(int newTickets)
    {
        _currentDisplayedTickets = newTickets;
        
        // In Game
        inGameUiTicketsText.text = $"Tickets: {_currentDisplayedTickets}";
        
        // In Shop
        if (shopUiTicketsText != null)
        {
            shopUiTicketsText.text = $"Tickets: {_currentDisplayedTickets}";
        }
    }
    
    public Coroutine UpdateTicketTextAnimated(int newTickets)
    {
        if (_ticketAnimateRoutine != null)
        {
            StopCoroutine(_ticketAnimateRoutine);
            _ticketAnimateRoutine = null;
        }

        if (_currentDisplayedTickets == newTickets)
        {
            UpdateTicketText(_currentDisplayedTickets);
            return null;
        }

        _ticketAnimateRoutine = StartCoroutine(AnimateTicketsByOne(newTickets));
        return _ticketAnimateRoutine;
    }
    
    public void UpdateRoundText(int currentRound, int maxRounds)
    {
        roundText.text = $"Rounds:\n{currentRound} / {maxRounds}";
    }
    
    public void UpdateBallsText(int ball, int maxBalls)
    {
        ballsText.text = $"Balls Thrown:\n{ball} / {maxBalls}";
    }

    public void UpdateScoreToBeatText(int scoreToBeat)
    {
        scoreToBeatText.text = $"Score To Beat:\n{scoreToBeat}";
    }
    
    private Coroutine _flashRoutine;

    public void UpdateYourScoreText(int currentScore)
    {
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
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashTextRoutine(totalScoreText, "Win\nRound!", Color.black, Color.white));
    }

    public void TriggerGameLostText()
    {
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        totalScoreText.color = Color.red;
        totalScoreText.text = "Game\nLost :(";
    }
    
    public void TriggerLastThrowText()
    {
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
        textComponent.text = textToDisplay;
        
        while (true)
        {
            textComponent.color = baseColor;
            yield return new WaitForSeconds(flashInterval);
            textComponent.color = flashColor;
            yield return new WaitForSeconds(flashInterval);
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
        while (currentBalls > 0)
        {
            currentBalls--;
            UpdateBallsText(currentBalls, maxBalls);

            float timer = 0f;
            bool skipTriggered = false;

            while (timer < 0.05f)
            {
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
                {
                    skipTriggered = true;
                    break;
                }
                timer += Time.deltaTime;
                yield return null;
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
