using UnityEngine;

[System.Serializable]
public class FrameStartState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        // Reset frame-specific variables via the Scriptable Objects
        HandleGameState(manager);

        manager.AllowPinsToFallOver(false);
        
        GetAllPinsInCurrentFrame(manager);

        SetTickets(manager);

        manager.scoreCalculator.ResetFrameScore();

        HandleMap(manager);

        HandleGrid(manager);

        // Need to refactor UI
        //manager.uiController.UpdateStandardUI(manager); 
            
        manager.pinHandInventory?.PrepareForPlacementPhase();

        manager.SwitchState(manager.PinPlacement);
    }

    private void GetAllPinsInCurrentFrame(RoundStateManager manager)
    {
        manager.allActivePinsInTheFrame = null;
        manager.allActivePinsInTheFrame = GameObject.FindGameObjectsWithTag("Pin");
    }

    private void HandleGameState(RoundStateManager manager)
    {
        manager.gameState.throwsThisFrame = 0;
        manager.gameState.scoreAppliedToTotalThisFrame = 0;
        manager.gameState.currentFrameScoreTarget = manager.CalculateFrameScoreTarget(manager.gameState.currentFrame);
    }

    private void HandleGrid(RoundStateManager manager)
    {
        manager.grid?.ClearAllOccupants();
        manager.grid?.SpawnTicketsForFrame();
    }

    private void HandleMap(RoundStateManager manager)
    {
        manager.ResetLaneExploders();
        manager.pinHandInventory?.ResetInventoryAndPins();
        manager.shopController?.HideShop();
        manager.RefreshRuntimePools();
    }

    private void SetTickets(RoundStateManager manager)
    {
        manager.ticketEconomy.gridTicketsEarnedThisFrame = 0;
        manager.ticketEconomy.scoreTargetTicketsEarnedThisFrame = 0;
        manager.ticketEconomy.ticketsEarnedLastFrame = 0;
        manager.ticketEconomy.lastFrameUsedTicketBonus = false;
        manager.ticketEconomy.lastFrameUsedStrikeTicketBonus = false;
    }

    public override void UpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    public override void ExitState(RoundStateManager manager) { }
}