using UnityEngine;
using UnityEngine.UI;

public class GameUIController : MonoBehaviour
{
    [Header("Frame Sheet Components")]
    public Text frameText;
    public Text totalScoreText;
    
    [Header("Real-Time Math Engine Visuals")]
    public Text currentMathText; 

    public void UpdateStandardUI(GameManager gm)
    {
        if (frameText) frameText.text = $"Frame: {gm.currentFrame} / {gm.maxFrames}";
        if (totalScoreText) totalScoreText.text = $"Total Score: {gm.totalScore}";
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.scoreCalculator != null)
        {
            int score = GameManager.Instance.scoreCalculator.currentFrameScore;
            int mult = GameManager.Instance.scoreCalculator.currentFrameMult;
            
            if (currentMathText) 
            {
                currentMathText.text = $"{score} Score X {mult} Mult\nRunning Frame Score: {score * mult}";
            }
        }
    }
}
