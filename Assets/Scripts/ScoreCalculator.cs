using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ScoreCalculator : MonoBehaviour
{
    [Header("Current Frame Math (Read Only)")]
    public int currentFrameScore = 0;
    public float currentFrameMult = 1f;

    [Header("UI Preferences")]
    public GameObject floatingTextPrefab;
    [SerializeField] private Vector3 scoreFloatingTextOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Vector3 multFloatingTextOffset = new Vector3(0f, 2.1f, 0f);
    
    private HashSet<Pin> scoredPinsThisFrame = new HashSet<Pin>();
    
    // We unified all scoring into DeferredScore, removing the need for pendingScoredPins.
    public struct DeferredScore
    {
        public int score;
        public float mult;
        public Vector3 position;
    }
    private List<DeferredScore> pendingDeferredScores = new List<DeferredScore>();

    void OnEnable()
    {
        //Pin.OnPinFallenGlobal += HandlePinFallen;
    }

    void OnDisable()
    {
        //Pin.OnPinFallenGlobal -= HandlePinFallen;
    }

    public void ResetFrameScore()
    {
        currentFrameScore = 0;
        currentFrameMult = 1f;
        scoredPinsThisFrame.Clear();
        pendingDeferredScores.Clear();
    }

    private void HandlePinFallen(Pin fallenPin)
    {
        if (scoredPinsThisFrame.Contains(fallenPin)) return;
        scoredPinsThisFrame.Add(fallenPin);
        
        // Note: We no longer queue the physical pin here. 
        // The Pin's "ScoreEffect" will handle queuing the actual math.
    }

    public IEnumerator CalculateScoringSequence()
    {
        // Score
        foreach (var def in pendingDeferredScores)
        {
            if (def.score != 0)
            {
                currentFrameScore += def.score;
                SpawnFloatingText(def.position, $"+{def.score}", Color.white, false);
                yield return new WaitForSeconds(0.3f);
            }
        }

        // Multiplier
        foreach (var def in pendingDeferredScores)
        {
            if (def.mult != 0)
            {
                currentFrameMult += def.mult;
                SpawnFloatingText(def.position, $"+{def.mult} Mult", Color.red, true);
                yield return new WaitForSeconds(0.3f);
            }
        }

        pendingDeferredScores.Clear();
    }

    public Vector3 GetFloatingTextSpawnPosition(Vector3 worldPosition, bool isMultiplierText)
    {
        return worldPosition + (isMultiplierText ? multFloatingTextOffset : scoreFloatingTextOffset);
    }

    private void SpawnFloatingText(Vector3 position, string text, Color color, bool isMultiplierText)
    {
        if (floatingTextPrefab == null) return;

        GameObject textObj = Instantiate(floatingTextPrefab, GetFloatingTextSpawnPosition(position, isMultiplierText), Quaternion.identity);
        TMP_Text tmpText = textObj.GetComponent<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = text;
            tmpText.color = color;
        }
        
        Destroy(textObj, 2f); // cleanup
    }

    public void ApplyStrikeBonus(float strikeMultiplier = 1.5f)
    {
        currentFrameMult = Mathf.Max(currentFrameMult, strikeMultiplier);
    }

    public int GetFinalScore()
    {
        return Mathf.RoundToInt(currentFrameScore * currentFrameMult);
    }

    public void QueueScoreModification(int scoreDelta, float multDelta, Vector3 position)
    {
        pendingDeferredScores.Add(new DeferredScore { score = scoreDelta, mult = multDelta, position = position });
        Debug.Log($"Score Modification Queued! {scoreDelta} Score / {multDelta} Mult");
    }

    public void ModifyScore(int scoreDelta, float multDelta = 0f)
    {
        currentFrameScore += scoreDelta;
        currentFrameMult += multDelta;
        Debug.Log($"Score Modified! New Score: {GetFinalScore()}");
    }
}