using UnityEngine;

[System.Serializable]
public class RoundStartState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        // Reset frame-specific variables via the Scriptable Objects
        manager.AllowPinsToFallOver(false);
        
        GetAllPinsInCurrentFrame(manager);

        SetTickets(manager);

        manager.scoreCalculator.ResetFrameScore();

        manager.currentRound++;
        RoundScore roundInfo = manager.roundData.GetRoundInfo(manager.currentRound);
        
        
        HandleMap(manager);

        HandleGrid(manager);

        // Need to refactor UI
        //manager.uiController.UpdateStandardUI(manager); 
            
        // manager.pinHandInventory?.PrepareForPlacementPhase();

        manager.SwitchState(manager.PinPlacement);
    }

    private void GetAllPinsInCurrentFrame(RoundStateManager manager)
    {
        manager.allActivePinsInTheFrame = null;
        manager.allActivePinsInTheFrame = GameObject.FindGameObjectsWithTag("Pin");
    }


    private void HandleGrid(RoundStateManager manager)
    {
        manager.grid?.ClearAllOccupants();
        manager.grid?.SpawnTicketsForFrame();
    }

    private void HandleMap(RoundStateManager manager)
    {
        manager.ResetLaneExploders();
        //manager.pinHandInventory?.ResetInventoryAndPins();
        manager.RefreshRuntimePools();
    }

    private void SetTickets(RoundStateManager manager)
    {
        //manager.ticketEconomy.gridTicketsEarnedThisFrame = 0;
       // manager.ticketEconomy.scoreTargetTicketsEarnedThisFrame = 0;
        //manager.ticketEconomy.ticketsEarnedLastFrame = 0;
        //manager.ticketEconomy.lastFrameUsedTicketBonus = false;
        //manager.ticketEconomy.lastFrameUsedStrikeTicketBonus = false;
    }

    public override void UpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    public override void ExitState(RoundStateManager manager) { }
}