using UnityEngine;
using TMPro;
using UnityEngine.UI;

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

    private void Awake()
    {
        Instance = this;
    }
    
    // Update score UI with default values, for starting a new round when we win the frame/round
    public void RefreshHUD(int totalScore, int currentRound, int currentBalls, RoundScore roundData, int maxRounds)
    {
        UpdateScoreText(totalScore);
        UpdateRoundText(currentRound, maxRounds);
        UpdateRoundToBeatText(roundData.scoreNeededToBeat);
        UpdateBallsText(currentBalls, roundData.maxBallsAllowedToThrow);
    }
    
    public void UpdateScoreText(int newScore)
    {
        totalScoreText.text = $"Total Score: {newScore}";
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

    public void UpdateRoundToBeatText(int scoreToBeat)
    {
        scoreToBeatText.text = $"Score To Beat: {scoreToBeat}";
    }
}