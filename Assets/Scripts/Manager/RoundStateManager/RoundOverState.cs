using UnityEngine;

public class RoundOverState : RoundBaseState
{
    public override void EnterState(RoundStateManager manager)
    {
        manager.UpdateInstructionText("Round Over! Click to continue.");

        // We are getting these twice, maybe can store them later in the manager
        foreach (Pin pin in manager.allActivePinsInTheFrame)
        {
            if (pin != null && pin.IsFallen)
            {
                pin.gameObject.SetActive(false);
            }
        }

        manager.MoveCamera(CameraLocation.BallThrowingCameraLocation);

        if (manager.uiManager != null && manager.uiManager.totalScoreText != null)
        {
            manager.uiManager.totalScoreText.text = PlayerSheet.Instance.GetScore().ToString();
        }
    }

    public override void UpdateState(RoundStateManager manager)
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Transitions to the next appropriate state, e.g., pin placement or round start
            manager.SwitchState(manager.roundStartState);
        }
    }

    public override void FixedUpdateState(RoundStateManager manager)
    {
    }

    public override void OnCollisionEnter(RoundStateManager manager, Collision collision)
    {
    }

    public override void OnCollisionExit(RoundStateManager manager, Collision collision)
    {
    }

    public override void OnTriggerEnter(RoundStateManager manager, Collider other)
    {
    }

    public override void OnTriggerExit(RoundStateManager manager, Collider other)
    {
    }

    public override void ExitState(RoundStateManager manager)
    {
    }
}