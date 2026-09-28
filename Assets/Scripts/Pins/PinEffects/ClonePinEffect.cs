using System;
using System.Collections.Generic;
using UnityEngine;

// https://share.gemini.google/oyy5yFMYdH97

[Serializable]
public class ClonePinEffect : PinEffect
{
    [SerializeField] private float velocityMultiplier = 1f;
    [SerializeField] private float heightOffset = 0.5f;
    
    [SerializeField] private Material cloneMaterial;
    
    private bool hasTriggered = false;
    
    [SerializeField] private List<Vector2Int> adjacentOffsets = new List<Vector2Int>
    {
        new Vector2Int(1, 0),  
        new Vector2Int(0, -1), 
    };

    public override void Execute(PinBase pinBase, Collider target = null)
    {
        if (hasTriggered) return; 
        
        if (pinBase == null || target == null) return;
        
        hasTriggered = true;

        GameObject hexManagerObj = GameObject.FindGameObjectWithTag("HexManager");
        if (hexManagerObj == null) return;

        HexGrid gridManager = hexManagerObj.GetComponent<HexGrid>();
        if (gridManager == null) return;

        HexCell originCell = gridManager.GetNearestCell(pinBase.transform.position);
        if (originCell == null) return;

        Rigidbody targetRb = target.attachedRigidbody;
        
        RoundStateManager roundStateManager = UnityEngine.Object.FindObjectOfType<RoundStateManager>();

        foreach (Vector2Int offset in adjacentOffsets)
        {
            HexCell targetCell = gridManager.GetCellWithOffset(originCell, offset);
            
            // Check if the adjacent cell exists and has an occupant
            if (targetCell != null && targetCell.occupant != null)
            {
                PinBase pinToClone = targetCell.occupant.GetComponentInParent<PinBase>();
                
                if (pinToClone != null && pinToClone != pinBase)
                {
                    Vector3 spawnPosition = targetCell.center + new Vector3(0, heightOffset, 0);

                    GameObject clone = UnityEngine.Object.Instantiate(
                        pinToClone.gameObject, 
                        spawnPosition, 
                        Quaternion.identity
                    );
                    
                    if (cloneMaterial != null)
                    {
                        Renderer[] renderers = clone.GetComponentsInChildren<Renderer>();
                        
                        foreach (Renderer rend in renderers)
                        {
                            rend.material = cloneMaterial;
                        }
                    }
                    
                    // Remove the cloning effect from the newly spawned clone to prevent infinite loops
                    PinBase clonePinBase = clone.GetComponent<PinBase>();
                    if (clonePinBase != null)
                    {
                        // A live pin is the clone source, so explicitly clear copied audio eligibility
                        // before registering this new instance with the active gameplay world.
                        clonePinBase.SetImpactAudioEligible(false);
                        // Ensure cloned pins cannot further split balls or clone to prevent exponential crashes
                        clonePinBase.onHitEffects.RemoveAll(effect => effect is ClonePinEffect || effect is SplitBallPinEffect);
                        clonePinBase.isClone = true;
                        
                        if (roundStateManager != null)
                        {
                            roundStateManager.AddPinToActiveList(clonePinBase);
                        }
                    }
                    
                    WorldObjectToCard worldObjectToCard = clone.GetComponent<WorldObjectToCard>();
                    if (worldObjectToCard != null)
                    { 
                        worldObjectToCard.cannotBeReturnedToDeck = true;
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
}
