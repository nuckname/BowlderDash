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
    public TextMeshProUGUI currentTicketsText;

    public void RefreshHUD(int currentTotalScore, int currentRound, int currentBalls, RoundScore roundData, int maxRounds)
    {
        UpdateRoundToBeatText(currentTotalScore);
        UpdateRoundText(currentRound, maxRounds);
        UpdateRoundToBeatText(roundData.scoreNeededToBeat);
        UpdateBallsText(currentBalls, roundData.maxBallsAllowedToThrow);
    }
    
    public void UpdateScoreText(int newScore)
    {
        totalScoreText.text = $"Total Score: {newScore}";
    }
    
    public void UpdateRoundText(int currentRound, int maxRounds)
    {
        roundText.text = $"Rounds: {currentRound} / {maxRounds}";
    }

    
    public void UpdateBallsText(int newBalls, int maxBalls)
    {
        ballsText.text = $"Balls Thrown: {newBalls} / {maxBalls}";
    }

    public void UpdateRoundToBeatText(int newRound)
    {
        scoreToBeatText.text = $"Score To Beat: {newRound}";
    }
}