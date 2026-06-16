using UnityEngine;

[System.Serializable]
public class FrameStartState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
    }

    public override void UpdateState(RoundStateManager manager)
    {
        // manager.SwitchState(manager.ThrowingBall);
    }

    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }

    public override void OnTriggerEnter(RoundStateManager manager, Collider other)
    {
        // Collision tag like ball end point or something.
        Debug.unityLogger.Log("Round Over Switch States.");

        if (other.gameObject.CompareTag("Ball"))
        {
            // Switch to score state actully?
            manager.SwitchState(manager.Shop);
        }
    }
    public override void OnTriggerExit(RoundStateManager manager, Collider other) { }
}