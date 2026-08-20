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

        manager.GetAllPinsInCurrentFrame(manager);

        SnapPinToClosestHexCell(manager);
    }

    // Some pins can move but not fall over, so lets just snap them to the nearest hex cell to make sure they are aligned properly.
    private static void SnapPinToClosestHexCell(RoundStateManager manager)
    {
        foreach (PinBase pin in manager.allActivePinsInTheFrame)
        {
            if (pin != null && !pin._isFallen)
            {
                HexCell nearestCell = manager.grid.GetNearestCell(pin.transform.position);
                if (nearestCell != null)
                {
                    pin.transform.position = new Vector3(nearestCell.center.x, pin.transform.position.y, nearestCell.center.z);
                    manager.grid.TryReserveCell(nearestCell, pin.transform);
                }
            }
        }
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