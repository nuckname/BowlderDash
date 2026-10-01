using System;
using System.Collections.Generic;
using UnityEngine;

// ai https://share.gemini.google/JAjIhzZsHznq

[Serializable]
public class AdjacentPinEffect : PinEffect
{
    [SerializeField] private int scorePerAdjacentPin = 5;

    private int _lastEarnedScore = 0;

    public override void Execute(PinBase pinBase, Collider target = null)
    {
        _lastEarnedScore = 0;

        if (pinBase == null || pinBase.FrameScoreCalculator == null)
            return;

        GameObject hexManagerObj = GameObject.FindGameObjectWithTag("HexManager");
        if (hexManagerObj == null || !hexManagerObj.TryGetComponent(out GridManager grid))
            return;

        int adjacentPinCount = grid.GetAdjacentPinCount(pinBase);
        Debug.Log("adjacentPinCount " + adjacentPinCount);
        
        if (adjacentPinCount > 0)
        {
            _lastEarnedScore = adjacentPinCount * scorePerAdjacentPin;
            pinBase.FrameScoreCalculator.QueueScoreModification(_lastEarnedScore, pinBase.transform.position);
        }
    }
    
    public override void ResetEffect()
    {
        _lastEarnedScore = 0;
    }

    public override string GetEffectText()
    {
        return _lastEarnedScore > 0 ? $"+{_lastEarnedScore}" : string.Empty;
    }
}