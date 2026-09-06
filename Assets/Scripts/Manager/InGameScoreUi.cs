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
    
    [Header("Tv Text Reference")]
    public TextMeshProUGUI tvScoreAdd;
    public TextMeshProUGUI tvScoreMulti;
    
    private float _currentTvScoreAdd = 0f;
    private float _currentTvScoreMulti = 1f;
    
    private int _currentDisplayedTickets = 0;
    private Coroutine _ticketAnimateRoutine;
    
    // Tickets
    public TextMeshProUGUI inGameUiTicketsText;
    public Text shopUiTicketsText;

    public static InGameScoreUi Instance;

    private int _currentDisplayedScore = 0;
    private Coroutine _scoreAnimateRoutine;

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
    
    public void AddToTvTotals(int scoreToAdd, float multiToAdd)
    {
        if (scoreToAdd > 0)
        {
            _currentTvScoreAdd += scoreToAdd;
            tvScoreAdd.text = _currentTvScoreAdd.ToString("0");
        }

        if (multiToAdd > 0)
        {
            _currentTvScoreMulti += multiToAdd;
            tvScoreMulti.text = $"{_currentTvScoreMulti:0.##}x";
        }
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
        _flashRoutine = StartCoroutine(FlashWinText());
    }

    public void TriggerGameLostText()
    {
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        totalScoreText.color = Color.red;
        totalScoreText.text = "Game\nLost :(";
    }

    private IEnumerator FlashWinText()
    {
        totalScoreText.text = "Win\nRound!";
        
        while (true)
        {
            totalScoreText.color = Color.yellow;
            yield return new WaitForSeconds(0.2f);
            totalScoreText.color = Color.white;
            yield return new WaitForSeconds(0.2f);
        }
    }
    
    public void ResetTvScores()
    {
        _currentTvScoreAdd = 0f;
        _currentTvScoreMulti = 1f;
        
        tvScoreAdd.text = "0";
        tvScoreMulti.text = "1";
    }
}
