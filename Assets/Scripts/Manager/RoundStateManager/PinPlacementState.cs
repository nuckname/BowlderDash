using UnityEngine;

public class PinPlacementState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        Debug.Log("Entering Pin Placement State");
        
        manager.MoveCamera(CameraLocation.PinCameraLocation);
        manager.TurnOnCardHand(true);
        
        PinOnPinCollisionLayer(false);
        
        manager.AllowPinCollisionEffects(false);
        manager.AllowPinsToFallOver(false);
        
        manager.DrawHandForCurrentTurn();
    }

    public override void UpdateState(RoundStateManager manager)
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (!manager.isHandDrawReady)
            {
                manager.UpdateInstructionText("Drawing cards...");
                return;
            }

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
        manager.GetAllPinsInCurrentFrame(manager);

        PinOnPinCollisionLayer(true);
        
        manager.AllowPinsToFallOver(false);
    }
    
    private void PinOnPinCollisionLayer(bool turnOn)
    {
        int pinLayer = LayerMask.NameToLayer("Pin");

        // Turn collision on and off for pin layer.
        Physics.IgnoreLayerCollision(pinLayer, pinLayer, turnOn);
    }
}
