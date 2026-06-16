using UnityEngine;

public class PinPlacementState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        Debug.Log("Entering Pin Placement State");
    }

    public override void UpdateState(RoundStateManager manager)
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            manager.SwitchState(manager.ThrowingBall);
        }
        
    }
    public override void FixedUpdateState(RoundStateManager manager) { }

    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    public override void ExitState(RoundStateManager manager) {}
    
}