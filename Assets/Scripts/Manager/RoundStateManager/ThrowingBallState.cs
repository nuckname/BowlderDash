using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ThrowingBallState : RoundBaseState, IPathDrawListener
{
    public PathDrawer pathDrawer;
    public ThrowTuningDataSO tuningData;
    public bool cannotThrowBall = false;

    [Header("Throw Completion")]
    [SerializeField, Min(0f)] private float minimumFlightTime = 1f;
    [SerializeField, Min(0f)] private float stallSpeedThreshold = 0.35f;
    [SerializeField, Min(0f)] private float stallGracePeriod = 1.5f;
    [SerializeField, Min(0f)] private float maximumFlightTime = 15f;
    [SerializeField, Min(0f)] private float maxPinSettleWaitTime;

    private RoundStateManager currentManager;
    private bool ballHasBeenReleased;
    private bool throwCompletionRequested;
    private bool waitingForPinsToSettle;
    private float flightTimer;
    private float stallTimer;
    private float pinSettleTimer;
    private bool rethrowPending;

    private float rethrowZOffset;
    
    private float lastLaunchSpeed;
    
    [Header("Auto Rethrow")]
    [SerializeField] private float minRethrowZOffset = -1.5f;
    [SerializeField] private float maxRethrowZOffset = 1.5f;
    [SerializeField] private float autoRethrowDelay = 2f;
    
    private Coroutine autoRethrowRoutine;
    
    private bool _hasTutorialTextPlayed = false;

    public override void EnterState(RoundStateManager manager)
    {
        bool isRethrow = rethrowPending;
        rethrowPending = false;
        PhysicsImpactAudio.SetWorldEligible(false);
        currentManager = manager;
        ballHasBeenReleased = false;
        throwCompletionRequested = false;
        waitingForPinsToSettle = false;
        flightTimer = 0f;
        stallTimer = 0f;
        pinSettleTimer = 0f;

        if (!isRethrow)
        {
            manager.CaptureStrikeTargetsForCurrentThrow();
        }
    
        manager.ballController.ResetBall();
        manager.MoveCamera(CameraLocation.BallThrowingCameraLocation);
    
        manager.TurnOnCardHand(false);
    
        if (manager.sessionData.currentRound == 0 && manager.sessionData.ballsThrown == 0 && !_hasTutorialTextPlayed)
        {
            _hasTutorialTextPlayed = true;
            manager.tutorialManager.PlayPickUpBallTutorial();
        }
    
        if (!isRethrow)
        {
            manager.SetPinPlacementPhysics(false);
        }
        manager.AllowPinCollisionEffects(true);
    
        manager.AllowPinsToFallOver(true);

        if (isRethrow)
        {
            StartAutoRethrow(manager);
        }
        else
        {
            RefreshBallSelection(manager);
        }
    }
    
    public void PrepareForRethrow(float zOffset = 0f)
    {
        rethrowPending = true;
        rethrowZOffset = zOffset;
    }

    public void RefreshBallSelection(RoundStateManager manager)
    {
        bool hasSelectedBall = manager.HasSelectedBallForCurrentAttempt;
        cannotThrowBall = !hasSelectedBall;
        manager.TurnOnBallMesh(hasSelectedBall);
        manager.TurnOnButtonToClickToGetBall(!hasSelectedBall);

        if (hasSelectedBall)
        {
            pathDrawer.Begin(this);
        }
        else
        {
            pathDrawer.Stop();
        }
    }
    
    private void StartAutoRethrow(RoundStateManager manager)
    {
        cannotThrowBall = false;
        ballHasBeenReleased = true;
        manager.TurnOnBallMesh(true);
        manager.TurnOnButtonToClickToGetBall(false);
        pathDrawer.Stop();

        OnInstructionUpdated("Auto Throw!");

        if (autoRethrowRoutine != null)
        {
            manager.StopCoroutine(autoRethrowRoutine);
        }

        autoRethrowRoutine = manager.StartCoroutine(AutoRethrowRoutine(manager));
    }

    public override void UpdateState(RoundStateManager manager)
    {
        if (Input.GetKeyDown(KeyCode.Tab) && !ballHasBeenReleased)
        {
            manager.SwitchState(manager.pinPlacementState);
            return;
        }

        if (cannotThrowBall)
        {
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

        if (waitingForPinsToSettle)
        {
            pinSettleTimer += Time.deltaTime;
            bool allSettled = true;

            foreach (PinBase pin in manager.allActivePinsInTheFrame)
            {
                if (!pin.IsSettled)
                {
                    allSettled = false;
                    break;
                }
            }

            if (allSettled || pinSettleTimer >= maxPinSettleWaitTime)
            {
                CompleteThrowFinal(manager);
            }

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
    
    private System.Collections.IEnumerator AutoRethrowRoutine(RoundStateManager manager)
    {
        yield return new WaitForSeconds(autoRethrowDelay);

        float launchSpeed = lastLaunchSpeed > 0f
            ? lastLaunchSpeed
            : (tuningData != null ? tuningData.forceMaxSpeed : 15f);

        manager.ballController.ExecuteAutoThrow(launchSpeed, rethrowZOffset);
        manager.StartBallCameraTracking();
        autoRethrowRoutine = null;
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
        lastLaunchSpeed = launchSpeed;
        
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
        if (autoRethrowRoutine != null)
        {
            manager.StopCoroutine(autoRethrowRoutine);
            autoRethrowRoutine = null;
        }

        pathDrawer.Stop();
        
        manager.TurnOnButtonToClickToGetBall(false);
        
        manager.cameraFollowScript.StopTracking();
        
        currentManager = null;
    }

    public override void OnCollisionEnter(RoundStateManager manager, Collision collision) { }
    public override void OnCollisionExit(RoundStateManager manager, Collision collision) { }

    // There are trigger in the map like in the gutter, so when the balls enters the gutter we stop the throw
    // its called BallCollisionTrigger.cs
    public override void OnTriggerEnter(RoundStateManager manager, Collider other)
    {
        if (ballHasBeenReleased && other.gameObject.CompareTag("Ball"))
        {
            CompleteThrow(manager, $"{other.gameObject.name} entered a completion trigger");
        }
    }

    private void CompleteThrow(RoundStateManager manager, string reason)
    {
        if (waitingForPinsToSettle || throwCompletionRequested || manager.currentState != this) return;

        Debug.Log($"Throw completed: {reason}. Waiting for pins to settle.");
        waitingForPinsToSettle = true;
        pinSettleTimer = 0f;
    }

    private void CompleteThrowFinal(RoundStateManager manager)
    {
        if (throwCompletionRequested || manager.currentState != this) return;

        throwCompletionRequested = true;
        manager.SwitchState(manager.roundOverState);
    }

    public override void OnTriggerExit(RoundStateManager manager, Collider other)
    {
        
    }
}
