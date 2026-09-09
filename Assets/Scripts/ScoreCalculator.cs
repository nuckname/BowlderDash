using UnityEngine;

// does this need mono behaviour?
// Do I need this?
public class ScoreCalculator : MonoBehaviour
{
    [Header("Current Frame Data")]
    private int queuedScore = 0;
    private float totalMultiplier = 1.0f; 

    // Called by scoring PinEffects when their configured trigger occurs.
    public void QueueScoreModification(int scoreValue, Vector3 position)
    {
        queuedScore += scoreValue;
    }

    public void QueueScoreMulti(float multValue, Vector3 position)
    {
        AddMultiplier(multValue);
    }

    public void AddMultiplier(float multiplierBonus)
    {
        totalMultiplier += multiplierBonus;
    }

    // Called by ScoringState to get the final result
    public int CalculateFinalScore()
    {
        float rawScore = queuedScore * totalMultiplier;
        
        int finalScore = Mathf.RoundToInt(rawScore);

        Debug.Log($"Scoring Math: {queuedScore} * {totalMultiplier}x = {finalScore}");
        
        return finalScore;
    }

    // Called by RoundStartState to clean the slate for the next throw
    public void ResetFrameScore()
    {
        queuedScore = 0;
        totalMultiplier = 1.0f;
    }
}
