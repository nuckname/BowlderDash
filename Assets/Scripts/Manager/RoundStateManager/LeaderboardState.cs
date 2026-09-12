using UnityEngine;

// Terminal state: gameplay resumes only through a fresh scene load.
public class LeaderboardState : RoundBaseState
{
    private readonly bool victory;

    public LeaderboardState(bool victory)
    {
        this.victory = victory;
    }

    public override void EnterState(RoundStateManager manager)
    {
        manager.ShowLeaderboardScreen(victory);
    }

    public override void UpdateState(RoundStateManager manager) { }
    public override void FixedUpdateState(RoundStateManager manager) { }
    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }
    public override void OnTriggerEnter(RoundStateManager manager, Collider other) { }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
    public override void ExitState(RoundStateManager manager) { }
}
