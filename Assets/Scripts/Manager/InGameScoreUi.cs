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
        totalScoreText.text = $"Total Score: {_currentDisplayedScore}";
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
            totalScoreText.text = $"Total Score: {_currentDisplayedScore}";
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
            
            totalScoreText.text = $"Total Score: {_currentDisplayedScore}";
            
            yield return new WaitForSeconds(0.05f); 
        }

        _scoreAnimateRoutine = null;
    }
    
    public void UpdateTicketText(int newTickets)
    {
        // In Game
        inGameUiTicketsText.text = $"Tickets: {newTickets}";
        
        // In Shop
        if (shopUiTicketsText != null)
        {
            shopUiTicketsText.text = $"Tickets: {newTickets}";
        }
    }
    
    public void UpdateRoundText(int currentRound, int maxRounds)
    {
        roundText.text = $"Rounds: {currentRound} / {maxRounds}";
    }
    
    public void UpdateBallsText(int ball, int maxBalls)
    {
        ballsText.text = $"Balls Thrown: {ball} / {maxBalls}";
    }

    public void UpdateScoreToBeatText(int scoreToBeat)
    {
        scoreToBeatText.text = $"Score To Beat: {scoreToBeat}";
    }
}
