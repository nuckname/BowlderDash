using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ThrowingBallState : RoundBaseState, IPathDrawListener
{
    public PathDrawer pathDrawer;
    public ThrowTuningDataSO tuningData;

    [Header("Throw Completion")]
    [SerializeField, Min(0f)] private float minimumFlightTime = 1f;
    [SerializeField, Min(0f)] private float stallSpeedThreshold = 0.35f;
    [SerializeField, Min(0f)] private float stallGracePeriod = 1.5f;
    [SerializeField, Min(0f)] private float maximumFlightTime = 15f;

    private RoundStateManager currentManager;
    private bool ballHasBeenReleased;
    private bool throwCompletionRequested;
    private float flightTimer;
    private float stallTimer;

    public override void EnterState(RoundStateManager manager)
    {
        currentManager = manager;
        ballHasBeenReleased = false;
        throwCompletionRequested = false;
        flightTimer = 0f;
        stallTimer = 0f;

        manager.CaptureStrikeTargetsForCurrentThrow();
        
        manager.ballController.ResetBall();
        manager.MoveCamera(CameraLocation.BallThrowingCameraLocation);
        
        manager.TurnOnCardHand(false);
        
        if (manager.sessionData.currentRound == 0 && manager.sessionData.ballsThrown == 0)
        {
            manager.tutorialManager.PlayThrowTutorial();
        }

        manager.SetPinPlacementPhysics(false);
        manager.AllowPinCollisionEffects(true);
        
        manager.AllowPinsToFallOver(true);
        
        manager.UpdateInstructionText("Draw the ball's path! Start from the ball.");
        
        pathDrawer.Begin(this);
    }

    public override void UpdateState(RoundStateManager manager)
    {
        if (Input.GetKeyDown(KeyCode.Tab) && !ballHasBeenReleased)
        {
            manager.SwitchState(manager.pinPlacementState);
            return;
        }

        if (Input.GetMouseButtonDown(1) && !ballHasBeenReleased)
        {
            OnDrawCancelled();
            return;
        }

        if (!ballHasBeenReleased || throwCompletionRequested || !manager.ballController.IsLaunched)
        {
            return;
        }

        flightTimer += Time.deltaTime;

        if (maximumFlightTime > 0f && flightTimer >= maximumFlightTime)
        {
            CompleteThrow(manager, "maximum flight time reached");
            return;
        }

        if (flightTimer < minimumFlightTime)
        {
            stallTimer = 0f;
            return;
        }

        float stallSpeedSquared = stallSpeedThreshold * stallSpeedThreshold;
        if (manager.ballController.LinearSpeedSquared <= stallSpeedSquared)
        {
            stallTimer += Time.deltaTime;
        }
        else
        {
            stallTimer = 0f;
        }

        if (stallGracePeriod > 0f && stallTimer >= stallGracePeriod)
        {
            CompleteThrow(manager, "primary ball stalled");
        }
    }

    public override void FixedUpdateState(RoundStateManager manager) { }

    public void OnDrawCompleted(List<CurvePoint> path, float drawDuration)
    {
        if (!currentManager.isHandDrawReady)
        {
            OnDrawCancelled();
            return;
        }

        pathDrawer.Stop();
        ballHasBeenReleased = true;
        
        // Legacy Code idk, ball speed
        float taperStartTime = tuningData.totalMinTime + Mathf.Max(0f, tuningData.powerFalloffDelay);
        float taperEndTime = Mathf.Max(taperStartTime + 0.0001f, tuningData.totalMaxTime);
        float t = (drawDuration <= taperStartTime) ? 1f : 1f - Mathf.InverseLerp(taperStartTime, taperEndTime, drawDuration);
        
        float launchSpeed = Mathf.Lerp(tuningData.forceMinSpeed, tuningData.forceMaxSpeed, t);
        
        OnInstructionUpdated("Ball Released!");
        
        // Execute throw
        currentManager.ballController.ExecuteThrow(path, launchSpeed);

        HandManager.Instance.DiscardHand();

        // Camera
        currentManager.StartBallCameraTracking();
    }

    public void OnDrawCancelled()
    {
        pathDrawer.ResetDrawing();
        currentManager.ballController.ResetBall();
        
        currentManager.cameraFollowScript.StopTracking();
        
        OnInstructionUpdated("Draw the ball's path! Start from the ball.");

        ballHasBeenReleased = false;
    }

    public void OnInstructionUpdated(string message)
    {
        currentManager.UpdateInstructionText(message);
    }
    
    public override void ExitState(RoundStateManager manager)
    {
        pathDrawer.Stop();
        
        manager.cameraFollowScript.StopTracking();
        
        currentManager = null;
    }

    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }

    public override void OnTriggerEnter(RoundStateManager manager, Collider other)
    {
        if (ballHasBeenReleased && other.gameObject.CompareTag("Ball"))
        {
            CompleteThrow(manager, $"{other.gameObject.name} entered a completion trigger");
        }
    }

    private void CompleteThrow(RoundStateManager manager, string reason)
    {
        if (throwCompletionRequested || manager.currentState != this) return;

        throwCompletionRequested = true;
        Debug.Log($"Throw completed: {reason}.");
        manager.SwitchState(manager.roundOverState);
    }

    public override void OnTriggerExit(RoundStateManager manager, Collider other)
    {
        
    }
}
