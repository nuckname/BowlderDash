using UnityEngine;

public class PinPlacementState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        Debug.Log("Entering Pin Placement State");
        manager.MoveCamera(CameraLocation.PinCameraLocation);
        manager.TurnOnCardHand(true);
        
        PinOnPinCollisionLayer(true);
    }

    public override void UpdateState(RoundStateManager manager)
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            manager.SwitchState(manager.throwingBallState);
        }
        
    }
    public override void FixedUpdateState(RoundStateManager manager) { }

    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }

    public override void ExitState(RoundStateManager manager)
    {
        PinOnPinCollisionLayer(false);
        
        manager.GetAllPinsInCurrentFrame(manager);

        manager.AllowPinsToFallOver(false);
    }

    private void PinOnPinCollisionLayer(bool turnOn)
    {
        int pinLayer = LayerMask.NameToLayer("Pin");

        // Turn collision on and off for pin layer basically.
        Physics.IgnoreLayerCollision(pinLayer, pinLayer, turnOn);
    }

}