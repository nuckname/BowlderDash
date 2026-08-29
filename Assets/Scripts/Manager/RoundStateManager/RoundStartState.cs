using UnityEngine;

// Called when we start a new round / frame
[System.Serializable]
public class RoundStartState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        manager.scoreCalculator.ResetFrameScore();

        manager.pinsToScore.Clear();

        UpdateTicketAmountPerRound(manager);
        
        manager.ResetHandDrawState();

        HandleMap(manager);

        HandleGrid(manager);

        manager.SwitchState(manager.pinPlacementState);
    }

    private void HandleGrid(RoundStateManager manager)
    {
        manager.gridManager?.ClearAllOccupants();
        manager.gridManager?.SpawnTicketsForFrame();
    }

    private void HandleMap(RoundStateManager manager)
    {
        //manager.ClearPlacedPinsAndReturnCardsToDeck();
        manager.ResetLaneExploders();
        //manager.pinHandInventory?.ResetInventoryAndPins();
        manager.RefreshRuntimePools();
    }
    
    private static void UpdateTicketAmountPerRound(RoundStateManager manager)
    {
        switch (manager.sessionData.currentRound)
        {
            case 1:
                manager.gridManager.gridTicketManager.ticketsPerFrame = 1;
                break;
            case 2:
                manager.gridManager.gridTicketManager.ticketsPerFrame = 2;
                break;
            case 3:
                manager.gridManager.gridTicketManager.ticketsPerFrame = 3;
                break;
            case 4:
                manager.gridManager.gridTicketManager.ticketsPerFrame = 3;
                break;
            case 5:
                manager.gridManager.gridTicketManager.ticketsPerFrame = 3;
                break;
            case 6:
                manager.gridManager.gridTicketManager.ticketsPerFrame = 4;
                break;
            case 7:
                manager.gridManager.gridTicketManager.ticketsPerFrame = 4;
                break;
            case 8:
                manager.gridManager.gridTicketManager.ticketsPerFrame = 4;
                break;
        }
    }

    public override void UpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    public override void ExitState(RoundStateManager manager) { }
}
