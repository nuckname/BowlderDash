using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("UI Text References")]
    public Text totalScoreText;
    public Text scoreToBeatText;
    public Text roundText;
    public Text ballsText;

    public void RefreshHUD(int currentTotalScore, int currentRound, int currentBalls, RoundScore roundData, int maxRounds)
    {
        totalScoreText.text = "{currentTotalScore}";

        roundText.text = $"{currentRound} / {maxRounds}";

        scoreToBeatText.text = $"{roundData.scoreNeededToBeat}";

        ballsText.text = $"{currentBalls} / {roundData.maxBallsAllowedToThrow}";
    }
}