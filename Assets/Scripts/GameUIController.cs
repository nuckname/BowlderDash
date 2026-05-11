using UnityEngine;
using UnityEngine.UI;

public class GameUIController : MonoBehaviour
{
    [Header("Frame Sheet Components")]
    public Text frameText;
    public Text totalScoreText;
    
    [Header("Real-Time Math Engine Visuals")]
    public Text currentMathText; 

    public void UpdateStandardUI(GrandGameManager gm)
    {
        if (frameText) 
        {
            if (gm.GetCurrentState() == GameState.PinPlacement)
            {
                frameText.text = $"Frame: {gm.currentFrame} / {gm.maxFrames}\n<color=yellow>Review Pin Placement. Press TAB to Bowl!</color>";
            }
            else
            {
                frameText.text = $"Frame: {gm.currentFrame} / {gm.maxFrames}";
            }
        }
        if (totalScoreText) totalScoreText.text = $"Total Score: {gm.totalScore}";
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
