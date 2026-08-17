using UnityEngine;

// Call this on a new round start
[System.Serializable]
public class RoundStartState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        SetTickets(manager);

        manager.scoreCalculator.ResetFrameScore();

        manager.sessionData.ballsThrown = 0;
        manager.ResetHandDrawState();
        
        manager.currentRound++;

        if (manager.currentRound == 1)
        {
            // set up start for a new game
        }
        
        if (manager.currentRound == 1)
        {
            // check if we are in a boss frame
            // then go to boss frame state
        }
        else
        {
            // go to pin placement
        }
        
        
        
        RoundScore roundInfo = manager.roundData.GetRoundInfo(manager.currentRound);
        if (roundInfo != null)
        {
            manager.inGameScoreUi.UpdateRoundToBeatText(roundInfo.scoreNeededToBeat);
            manager.inGameScoreUi.UpdateBallsText(manager.sessionData.ballsThrown, roundInfo.maxBallsAllowedToThrow);
        }
        
        HandleMap(manager);

        HandleGrid(manager);

        manager.SwitchState(manager.pinPlacementState);
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
