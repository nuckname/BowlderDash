using UnityEngine;

// does this need mono behaviour?
// Do I need this?
public class ScoreCalculator : MonoBehaviour
{
    [Header("Current Frame Data")]
    private int standardPinsFallen = 0;
    private int bonusScore = 0;
    private float totalMultiplier = 1.0f; 

    // Called by PinEffects when a special pin falls
    public void QueueScoreModification(int scoreValue, float multValue, Vector3 position)
    {
        bonusScore += scoreValue;
    }

    public void QueueScoreMulti(int scoreValue, float multValue, Vector3 position)
    {
        AddMultiplier(multValue);
    }

    public void AddMultiplier(float multiplierBonus)
    {
        totalMultiplier += multiplierBonus;
    }

    // Called by your ScoringState for every standard pin that fell
    public void AddStandardPinFallen()
    {
        standardPinsFallen++;
    }

    // Called by ScoringState to get the final result
    public int CalculateFinalScore()
    {
        float rawScore = (standardPinsFallen + bonusScore) * totalMultiplier;
        
        int finalScore = Mathf.RoundToInt(rawScore);

        Debug.Log($"Scoring Math: ({standardPinsFallen} + {bonusScore}) * {totalMultiplier}x = {finalScore}");
        
        return finalScore;
    }

    // Called by RoundStartState to clean the slate for the next throw
    public void ResetFrameScore()
    {
        standardPinsFallen = 0;
        bonusScore = 0;
        totalMultiplier = 1.0f;
    }
}
