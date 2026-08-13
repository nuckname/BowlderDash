using UnityEngine;

[System.Serializable]
public class ShopState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        manager.MoveCamera(CameraLocation.ShopCameraLocation);
        manager.UpdateInstructionText("Shop phase. Spend tickets, then click Continue.");

        manager.shopUI.Show();
        
        // Reset UI
        RoundScore roundInfo = manager.roundData.GetRoundInfo(manager.currentRound);
        
        // We need to pick 3 cards from
        // assign from round manager
        
/*        
        manager.inGameScoreUi.RefreshHUD(
            PlayerSheet.Instance.playerCurrentTicket,
            manager.currentRound,
            PlayerSheet.Instance.GetAmountOfBallsThrown(),
            roundInfo,
            manager.roundData.GetMaxRoundNumber()
        );*/
    }

    public override void UpdateState(RoundStateManager manager) { }
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    
    public override void ExitState(RoundStateManager manager)
    {
        if (manager.shopUI != null)
        {
            manager.shopUI.Hide();
        }
    }
}