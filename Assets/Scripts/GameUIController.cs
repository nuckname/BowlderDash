using UnityEngine;
using UnityEngine.UI;

public class GameUIController : MonoBehaviour
{
    private string placementWarningMessage;

    [Header("Frame Sheet Components")]
    public Text frameText;
    public Text totalScoreText;
    public Text ticketText;
    
    [Header("Real-Time Math Engine Visuals")]
    public Text currentMathText; 

    public void UpdateStandardUI(GrandGameManager gm)
    {
        int throwDisplay = gm.GetCurrentThrowDisplayNumber();
        string progressHeader = $"Frame: {gm.currentFrame} / {gm.maxFrames}\nThrow: {throwDisplay} / {Mathf.Max(1, gm.maxThrowsPerFrame)}\nScore To Beat: {gm.currentFrameScoreTarget}";

        if (frameText) 
        {
            if (gm.GetCurrentState() == GameState.PinPlacement)
            {
                string placementCopy = "Place pins from your hand, then press TAB to bowl.";
                if (!string.IsNullOrEmpty(placementWarningMessage))
                {
                    placementCopy += $"\n<color=#ff8a65>{placementWarningMessage}</color>";
                }

                frameText.text = $"{progressHeader}\n<color=yellow>{placementCopy}</color>";
            }
            else if (gm.GetCurrentState() == GameState.Shop)
            {
                frameText.text = $"{progressHeader}\n<color=cyan>Shop Phase. Spend tickets on the TV.</color>";
            }
            else if (gm.GetCurrentState() == GameState.GameOver)
            {
                frameText.text = $"Game Complete\n{progressHeader}";
            }
            else
            {
                frameText.text = progressHeader;
            }
        }
        if (totalScoreText) totalScoreText.text = $"Total Score: {gm.totalScore}";
        if (ticketText)
        {
            string bonusSuffix = gm.GetLastFrameTicketBonusSummary();
            ticketText.text = $"Tickets: {gm.currentTickets}\nLast Frame: +{gm.ticketsEarnedLastFrame}{bonusSuffix}";
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
            int score = gm.scoreCalculator.currentFrameScore;
            float mult = gm.scoreCalculator.currentFrameMult;
            int finalScore = gm.scoreCalculator.GetFinalScore();
            int targetScore = Mathf.Max(1, gm.currentFrameScoreTarget);
            int targetMultiplier = finalScore >= targetScore ? Mathf.Max(1, finalScore / targetScore) : 0;
            
            if (currentMathText) 
            {
                currentMathText.text =
                    $"Round Score: {finalScore} / {targetScore}\n" +
                    $"Base {score} x {mult:0.##} Mult\n" +
                    $"Target Tickets: +{targetMultiplier}";
            }
        }
    }
}
