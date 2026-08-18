using UnityEngine;

// Called when we start a new round / frame
[System.Serializable]
public class RoundStartState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        manager.scoreCalculator.ResetFrameScore();
        SessionData.Instance.NewRoundReset();

        manager.ResetHandDrawState();
        
        manager.currentRound++;

        if (manager.currentRound == 1)
        {
            // set up start for a new game
        }
        
        // Reset Ui Score 
        // Reset SessionDataScore
        
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

        // Not ideal hard coding max rounds 8
        manager.inGameScoreUi.RefreshHUD(0, roundInfo.roundNumber, 0, roundInfo, 8);
        
        
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
        manager.ClearPlacedPinsAndReturnCardsToDeck();
        manager.ResetLaneExploders();
        //manager.pinHandInventory?.ResetInventoryAndPins();
        manager.RefreshRuntimePools();
    }

    public override void UpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    public override void ExitState(RoundStateManager manager) { }
}
