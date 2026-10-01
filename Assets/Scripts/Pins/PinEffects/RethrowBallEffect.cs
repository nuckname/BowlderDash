using System.Collections.Generic;
using UnityEngine;

public class RethrowBallEffect : PinEffect
{
    private float _rethrowCameraSpeed = 0.75f; 
    private float zOffset;
    
    public override void Execute(PinBase pinBase, Collider target = null)
    {
        RoundStateManager stateManager = GameObject.FindObjectOfType<RoundStateManager>();
        
        float zOffset = GetARandomPinThatHasntFallenOver(stateManager, pinBase);

        stateManager.TryRethrowCurrentBall(_rethrowCameraSpeed, zOffset);
        
        
    }

    private float GetARandomPinThatHasntFallenOver(RoundStateManager stateManager, PinBase pinBase)
    {
        if (stateManager != null)
        {
            List<PinBase> standingPins = new List<PinBase>();

            if (stateManager.allActivePinsInTheFrame != null)
            {
                foreach (PinBase pin in stateManager.allActivePinsInTheFrame)
                {
                    if (pin != null && !pin.IsFallen && pin != pinBase)
                    {
                        standingPins.Add(pin);
                    }
                }
            }

            if (standingPins.Count == 0 && pinBase != null && !pinBase.IsFallen)
            {
                standingPins.Add(pinBase);
            }

            if (standingPins.Count > 0)
            {
                PinBase targetPin = standingPins[Random.Range(0, standingPins.Count)];
                float startX = stateManager.ballController != null ? stateManager.ballController.StartPos.x : 0f;
                zOffset = targetPin.transform.position.x - startX;
            }

        }
        
        return zOffset;
    }
}