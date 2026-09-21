using UnityEngine;

public class RethrowBallEffect : PinEffect
{
    private float _rethrowCameraSpeed = 1f;
    
    public override void Execute(PinBase pinBase, Collider target = null)
    {
        RoundStateManager stateManager = GameObject.FindObjectOfType<RoundStateManager>();
        
        if (stateManager != null)
        {
            stateManager.nextCameraSpeedOverride = _rethrowCameraSpeed;
            stateManager.SwitchState(stateManager.throwingBallState);
        }

    }
}
