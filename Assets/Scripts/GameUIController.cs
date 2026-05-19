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
        if (frameText) 
        {
            if (gm.GetCurrentState() == GameState.PinPlacement)
            {
                string placementCopy = "Place pins from your hand, then press TAB to bowl.";
                if (!string.IsNullOrEmpty(placementWarningMessage))
                {
                    placementCopy += $"\n<color=#ff8a65>{placementWarningMessage}</color>";
                }

                frameText.text = $"Frame: {gm.currentFrame} / {gm.maxFrames}\n<color=yellow>{placementCopy}</color>";
            }
            else if (gm.GetCurrentState() == GameState.Shop)
            {
                frameText.text = $"Frame: {gm.currentFrame} / {gm.maxFrames}\n<color=cyan>Shop Phase. Spend tickets on the TV.</color>";
            }
            else if (gm.GetCurrentState() == GameState.GameOver)
            {
                frameText.text = $"Game Complete\nFinal Frame: {gm.currentFrame} / {gm.maxFrames}";
            }
            else
            {
                frameText.text = $"Frame: {gm.currentFrame} / {gm.maxFrames}";
            }
        }
        if (totalScoreText) totalScoreText.text = $"Total Score: {gm.totalScore}";
        if (ticketText)
        {
            string bonusSuffix = gm.lastFrameUsedTicketBonus ? " (2x clear bonus)" : string.Empty;
            ticketText.text = $"Tickets: {gm.currentTickets}\nLast Frame: +{gm.ticketsEarnedLastFrame}{bonusSuffix}";
        }
    }

    public void SetPlacementWarning(string warningMessage)
    {
        placementWarningMessage = warningMessage;
    }

    void Update()
    {
        if (GrandGameManager.Instance != null && GrandGameManager.Instance.scoreCalculator != null)
        {
            int score = GrandGameManager.Instance.scoreCalculator.currentFrameScore;
            float mult = GrandGameManager.Instance.scoreCalculator.currentFrameMult;
            int finalScore = GrandGameManager.Instance.scoreCalculator.GetFinalScore();
            
            if (currentMathText) 
            {
                currentMathText.text = $"{score} Score X {mult:0.##} Mult\nRunning Frame Score: {finalScore}";
            }
        }
    }
}
