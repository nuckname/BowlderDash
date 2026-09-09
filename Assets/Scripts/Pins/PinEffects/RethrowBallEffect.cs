using UnityEngine;

public class RethrowBallEffect : PinEffect
{
    public override void Execute(PinBase pinBase, Collider target = null)
    {
        RoundStateManager stateManager = GameObject.FindObjectOfType<RoundStateManager>();
        if (stateManager != null)
        {
            stateManager.SwitchState(stateManager.throwingBallState);
        }

    }
}
