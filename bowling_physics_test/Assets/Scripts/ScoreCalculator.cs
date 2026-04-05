using UnityEngine;
using System.Collections.Generic;

public class ScoreCalculator : MonoBehaviour
{
    [Header("Current Frame Math (Read Only)")]
    public int currentFrameScore = 0;
    public int currentFrameMult = 1;
    
    private HashSet<Pin> scoredPinsThisFrame = new HashSet<Pin>();

    void OnEnable()
    {
        Pin.OnPinFallenGlobal += HandlePinFallen;
    }

    void OnDisable()
    {
        Pin.OnPinFallenGlobal -= HandlePinFallen;
    }

    public void ResetFrameScore()
    {
        currentFrameScore = 0;
        currentFrameMult = 1;
        scoredPinsThisFrame.Clear();
    }

    private void HandlePinFallen(Pin fallenPin)
    {
        if (scoredPinsThisFrame.Contains(fallenPin)) return;
        scoredPinsThisFrame.Add(fallenPin);

        currentFrameScore += fallenPin.score;
        currentFrameMult += fallenPin.mult;

        Debug.Log($"Pin Scored! Added {fallenPin.score} Score / {fallenPin.mult} Mult. New Total: {GetFinalScore()}");
    }

    public int GetFinalScore()
    {
        return currentFrameScore * currentFrameMult;
    }
}
