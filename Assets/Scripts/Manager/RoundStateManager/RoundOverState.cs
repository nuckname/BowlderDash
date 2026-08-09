using UnityEngine;

public class RoundOverState : RoundBaseState
{
    private float waitTimer = 0f;
    private float waitTime = 2.5f;

    public override void EnterState(RoundStateManager manager)
    {
        manager.UpdateInstructionText("Round Over! Click to continue.");
        
        waitTimer = 0f;

        // We are getting these twice, maybe can store them later in the manager
        foreach (Pin pin in manager.allActivePinsInTheFrame)
        {
            if (pin != null && pin.IsFallen)
            {
                pin.gameObject.SetActive(false);
            }
        }

        manager.MoveCamera(CameraLocation.BallThrowingCameraLocation);

        if (manager.inGameScoreUi != null && manager.inGameScoreUi.totalScoreText != null)
        {
            manager.inGameScoreUi.totalScoreText.text = PlayerSheet.Instance.GetScore().ToString();
        }
    }

    public override void UpdateState(RoundStateManager manager)
    {
        waitTimer += Time.deltaTime;

        if (waitTimer >= waitTime || Input.GetMouseButtonDown(0))
        {
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