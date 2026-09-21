using UnityEngine;

public class RoundWinState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        // We have won the frame so lets reset UI values and increase round number etc.

        // Clear the grid
        manager.gridManager.ClearAllOccupants();
        
        RoundScore roundInfo = manager.roundData.GetRoundInfo(manager.sessionData.currentRound);
        
        if (roundInfo != null)
        {
            manager.inGameScoreUi.RefreshHUD(0, roundInfo.roundNumber, 0, roundInfo, manager.roundData.GetMaxRoundNumber());
        }
        else
        {
            Debug.LogWarning("RoundScore is null");
        }
        
        HandleBallsOnRampVisual(manager);

        if (SessionData.Instance.currentRound > 2)
        {
            manager.gridManager.grid.MoveGridTransform();
        }
        
        manager.SwitchState(manager.shopState);
    }

    private void HandleBallsOnRampVisual(RoundStateManager manager)
    {
        manager.ballRampSpawner.RemoveBallFromRamp();
        manager.ballRampSpawner.SpawnBallsForRound(SessionData.Instance.currentRound);
    }

    public override void UpdateState(RoundStateManager manager) { }
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    public override void ExitState(RoundStateManager manager) { }
}