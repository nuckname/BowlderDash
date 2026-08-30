using System;
using System.Collections.Generic;
using UnityEngine;

// https://share.gemini.google/oyy5yFMYdH97

[Serializable]
public class ClonePinEffect : PinEffect
{
    [SerializeField] private float velocityMultiplier = 1f;
    [SerializeField] private float heightOffset = 0.5f;
    
    [SerializeField] private List<Vector2Int> adjacentOffsets = new List<Vector2Int>
    {
        new Vector2Int(1, 0),  
        new Vector2Int(1, -1), 
        new Vector2Int(0, -1), 
        new Vector2Int(-1, 0), 
        new Vector2Int(-1, 1), 
        new Vector2Int(0, 1)   
    };

// ClonePinEffect.cs
// Update your Execute method to find the RoundStateManager and add the new pin
    public override void Execute(PinBase pinBase, Collider target = null)
    {
        if (pinBase == null || target == null) return;

        GameObject hexManagerObj = GameObject.FindGameObjectWithTag("HexManager");
        if (hexManagerObj == null) return;

        GridManager gridManager = hexManagerObj.GetComponent<GridManager>();
        if (gridManager == null) return;

        HexCell originCell = gridManager.GetNearestCell(pinBase.transform.position);
        if (originCell == null) return;

        Rigidbody targetRb = target.attachedRigidbody;

        foreach (Vector2Int offset in adjacentOffsets)
        {
            HexCell targetCell = gridManager.GetCellWithOffset(originCell, offset);
            
            if (targetCell != null)
            {
                Vector3 spawnPosition = targetCell.center + new Vector3(0, heightOffset, 0);

                GameObject clone = UnityEngine.Object.Instantiate(
                    pinBase.gameObject, 
                    spawnPosition, 
                    pinBase.transform.rotation
                );
                
                // Remove the cloning effect from the newly spawned clone to prevent infinite loops
                PinBase clonePinBase = clone.GetComponent<PinBase>();
                if (clonePinBase != null)
                {
                    clonePinBase.onHitEffects.RemoveAll(effect => effect is ClonePinEffect);

                    // Can also use tag?
                    RoundStateManager roundStateManager = UnityEngine.Object.FindObjectOfType<RoundStateManager>();
                    if (roundStateManager != null)
                    {
                        roundStateManager.AddPinToActiveList(clonePinBase);
                    }
                }
                
                Rigidbody cloneRb = clone.GetComponent<Rigidbody>();

                if (cloneRb != null && targetRb != null)
                {
                    // Add velocity in the direction the pin base was hit
                    cloneRb.linearVelocity = targetRb.linearVelocity * velocityMultiplier;
                }
            }
        }
    }
}