using System;
using UnityEngine;

[Serializable]
public class RepeatRightPinEffect : PinEffect
{
    private bool hasTriggered = false;
    
    [SerializeField] private Vector2Int rightOffset = new Vector2Int(1, 0);

    public override void Execute(PinBase pinBase, Collider target = null)
    {
        if (hasTriggered) return; 
        if (pinBase == null) return;
        
        hasTriggered = true;

        GameObject hexManagerObj = GameObject.FindGameObjectWithTag("HexManager");
        if (hexManagerObj == null) return;

        HexGrid gridManager = hexManagerObj.GetComponent<HexGrid>();
        if (gridManager == null) return;

        HexCell originCell = gridManager.GetNearestCell(pinBase.transform.position);
        if (originCell == null) return;

        RoundStateManager roundStateManager = UnityEngine.Object.FindObjectOfType<RoundStateManager>();
        if (roundStateManager == null) return;

        HexCell targetCell = gridManager.GetCellWithOffset(originCell, rightOffset);
        
        if (targetCell != null && targetCell.occupant != null)
        {
            PinBase rightPin = targetCell.occupant.GetComponentInParent<PinBase>();
            
            if (rightPin != null && rightPin != pinBase)
            {
                roundStateManager.pinsToScore.Add(rightPin);
            }
        }
    }

    public override void ResetEffect()
    {
        hasTriggered = false;
    }

    public override string GetEffectText()
    {
        return "Repeated Right Pin!";
    }
}