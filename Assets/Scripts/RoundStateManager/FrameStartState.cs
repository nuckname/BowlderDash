using UnityEngine;

[System.Serializable]
public class FrameStartState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
    }

    public override void UpdateState(RoundStateManager manager)
    {
        // manager.SwitchState(manager.ThrowingBall);
    }

    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }

    public override void FixedUpdateState(RoundStateManager manager) { }

    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }

    public override void OnTriggerEnter(RoundStateManager manager, Collider other)
    {

    }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    
    public override void ExitState(RoundStateManager manager) {}
    
}