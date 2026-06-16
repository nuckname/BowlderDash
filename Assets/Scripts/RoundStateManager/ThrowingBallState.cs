using UnityEngine;

public class ThrowingBallState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        
    }

    public override void UpdateState(RoundStateManager manager)
    {
        
    }

    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
}