using UnityEngine;

[System.Serializable]
public class GameCompleteState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        manager.spawnBossObjectsState.DespawnActiveBoss();
        manager.SwitchState(new LeaderboardState(true));
    }

    public override void UpdateState(RoundStateManager manager) { }
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    public override void ExitState(RoundStateManager manager) { }
}
