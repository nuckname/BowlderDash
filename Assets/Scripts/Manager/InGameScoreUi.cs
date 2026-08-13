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

    public void RefreshHUD(int currentTotalScore, int currentRound, int currentBalls, RoundScore roundData, int maxRounds)
    {
        totalScoreText.text = $"{currentTotalScore}";

        roundText.text = $"{currentRound} / {maxRounds}";

        scoreToBeatText.text = $"{roundData.scoreNeededToBeat}";

        ballsText.text = $"{currentBalls} / {roundData.maxBallsAllowedToThrow}";
    }
    
    public void UpdateScoreText(int newScore)
    {
        totalScoreText.text = $"{newScore}";
    }
    
    public void UpdateBallsText(int newBalls, int maxBalls)
    {
        ballsText.text = $"{newBalls} / {maxBalls}";
    }

    public void UpdateRoundToBeatText(int newRound)
    {
        scoreToBeatText.text = $"{newRound}";
    }
}