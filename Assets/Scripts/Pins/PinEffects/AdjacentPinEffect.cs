using System;
using System.Collections.Generic;
using UnityEngine;

// ai https://share.gemini.google/JAjIhzZsHznq

[Serializable]
public class AdjacentPinEffect : PinEffect
{
    [SerializeField] private int scorePerAdjacentPin = 5;

    [SerializeField] private List<Vector2Int> adjacentOffsets = new List<Vector2Int>
    {
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(0, 1),
        new Vector2Int(-1, 1),
        new Vector2Int(1, -1)
    };

    private int _lastEarnedScore = 0;

    public override void Execute(PinBase pinBase, Collider target = null)
    {
        _lastEarnedScore = 0;

        if (pinBase == null || pinBase.FrameScoreCalculator == null)
            return;

        GameObject hexManagerObj = GameObject.FindGameObjectWithTag("HexManager");
        if (hexManagerObj == null || !hexManagerObj.TryGetComponent(out GridManager grid))
            return;

        HexCell originCell = grid.GetNearestCell(pinBase.transform.position);
        if (originCell == null)
            return;

        int adjacentPinCount = 0;

        foreach (Vector2Int offset in adjacentOffsets)
        {
            HexCell neighborCell = grid.GetCellWithOffset(originCell, offset);
            if (neighborCell != null && neighborCell.occupant != null && neighborCell.occupant != pinBase.transform)
            {
                if (neighborCell.occupant.GetComponent<PinBase>() != null)
                {
                    adjacentPinCount++;
                }
            }
        }

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