using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class PinPlacementState : RoundBaseState
{
    [SerializeField] private Button throwBallButton;
    private RoundStateManager _manager;
    
    public override void EnterState(RoundStateManager manager)
    {
        Debug.Log("Entering Pin Placement State");

        _manager = manager;
        
        throwBallButton.gameObject.SetActive(true);

        if (manager.sessionData.currentRound == 0 && manager.sessionData.ballsThrown == 0)
        {
            manager.tutorialManager.PlayPinPlacementTutorial();
        }

        manager.PinPlacementVisuals(true);
        
        manager.spawnBossObjectsState.SetBossObjectActive(false);
        
        manager.MoveCamera(CameraLocation.PinCameraLocation);
        manager.TurnOnCardHand(true);
        
        TurnOnPinSelect(true);

        manager.GetAllPinsInCurrentFrame(manager);
        manager.AllowPinCollisionEffects(false);
        manager.AllowPinsToFallOver(false);
        manager.SetPinPlacementPhysics(true);
        manager.SetPinPlacementInteraction(true);

        manager.DrawHandForCurrentTurn();

        //SnapPinToClosestHexCell(manager);
    }

    // Some pins can move but not fall over, so lets just snap them to the nearest hex cell to make sure they are aligned properly.
    private static void SnapPinToClosestHexCell(RoundStateManager manager)
    {
        foreach (PinBase pin in manager.allActivePinsInTheFrame)
        {
            if (pin != null && !pin._isFallen)
            {
                HexCell nearestCell = manager.gridManager.GetNearestCell(pin.transform.position);
                if (nearestCell != null)
                {
                    pin.transform.position =
                        new Vector3(nearestCell.center.x, pin.transform.position.y, nearestCell.center.z);
                    manager.gridManager.TryReserveCell(nearestCell, pin.transform);
                }
            }
        }
    }

    public override void UpdateState(RoundStateManager manager)
    {
        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Tab))
        {
            if (!manager.isHandDrawReady)
            {
                manager.UpdateInstructionText("Drawing cards...");
                return;
            }

            manager.SwitchState(manager.throwingBallState);
        }
    }

    private void TurnOnPinSelect(bool turnOn)
    {
        foreach (PinBase pinBase in _manager.allActivePinsInTheFrame)
        {
            pinBase.allowPinSelection = turnOn;
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
        manager.SetPinPlacementInteraction(false);

        throwBallButton.gameObject.SetActive(false);
        
        manager.AllowPinsToFallOver(false);
        
        TurnOnPinSelect(false);

        manager.PinPlacementVisuals(false);
    }
}