using UnityEngine;

public class RoundWinState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        // We have won the frame so lets reset UI values and increase round number etc.
        
        RoundScore roundInfo = manager.roundData.GetRoundInfo(manager.sessionData.currentRound);
        
        if (roundInfo != null)
        {
            manager.inGameScoreUi.RefreshHUD(0, roundInfo.roundNumber, 0, roundInfo, manager.roundData.GetMaxRoundNumber());
        }
        else
        {
            Debug.LogWarning("RoundScore is null");
        }
        
        manager.SwitchState(manager.shopState);
    }

    public override void UpdateState(RoundStateManager manager) { }
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    public override void ExitState(RoundStateManager manager) { }
}