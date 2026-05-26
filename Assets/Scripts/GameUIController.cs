using UnityEngine;
using UnityEngine.UI;

public class GameUIController : MonoBehaviour
{
    private string placementWarningMessage;

    [Header("HUD Components")]
    public Text frameText;
    public Text ballText;
    public Text scoreToBeatText;
    public Text totalScoreText;
    public Text ticketText;
    
    [Header("Legacy / Optional")]
    public Text currentMathText;

    public void UpdateStandardUI(GrandGameManager gm)
    {
        int throwDisplay = gm.GetCurrentThrowDisplayNumber();

        if (frameText) frameText.text = $"{gm.currentFrame}/{gm.maxFrames}";
        if (ballText) ballText.text = $"{throwDisplay}/{Mathf.Max(1, gm.maxThrowsPerFrame)}";
        if (scoreToBeatText) scoreToBeatText.text = Mathf.Max(0, gm.currentFrameScoreTarget).ToString();
        if (ticketText) ticketText.text = gm.currentTickets.ToString();

        if (currentMathText)
        {
            currentMathText.text = string.Empty;
        }
    }

    public void SetPlacementWarning(string warningMessage)
    {
        placementWarningMessage = warningMessage;
    }

    void Update()
    {
        GrandGameManager gm = GrandGameManager.Instance;
        if (gm != null && gm.scoreCalculator != null)
        {
            int finalScore = gm.scoreCalculator.GetFinalScore();
            if (totalScoreText) totalScoreText.text = finalScore.ToString();
        }
    }
}
