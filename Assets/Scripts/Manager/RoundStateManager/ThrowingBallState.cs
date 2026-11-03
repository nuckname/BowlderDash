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
    [SerializeField, Min(0f)] private float allPinsDownConfirmationTime = 0.35f;
    [SerializeField, Min(0f)] private float fastScoringLeadIn = 0.2f;

    public bool CompletedEarly { get; private set; }
    public float FastScoringLeadIn => fastScoringLeadIn;
    public int LaunchVersion { get; private set; }
    private float allPinsDownTimer;
    private bool primaryFlightEnded;
    private PinBase[] confirmationPins;
    private int confirmationMembership;

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
    [SerializeField] private float autoRethrowDelay = 5f;
    
    private Coroutine autoRethrowRoutine;
    
    private bool _hasTutorialTextPlayed = false;

    public override void EnterState(RoundStateManager manager)
    {
        LaunchVersion++;
        CompletedEarly = false;
        allPinsDownTimer = 0f;
        primaryFlightEnded = false;
        confirmationPins = null;
        confirmationMembership = 0;
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
        
        manager.spawnBossObjectsState.SetBossObjectActive(true);

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
        if (!manager.HasSelectedBallForCurrentAttempt && !manager.CheckKeepClickingTheBall())
        {
            manager.ClickButtonToGetBall();

            // so we dont give the player an extra throw.
            return;
        }
        
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

        if (autoRethrowRoutine != null)
        {
            manager.StopCoroutine(autoRethrowRoutine);
        }

        autoRethrowRoutine = manager.StartCoroutine(AutoRethrowRoutine(manager));
    }

    public override void UpdateState(RoundStateManager manager)
    {
        if ((Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.E)) && !ballHasBeenReleased)
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

        if (TryCompleteAllPinsDown(manager)) return;

        if (waitingForPinsToSettle)
        {
            pinSettleTimer += Time.deltaTime;
            bool allSettled = true;

            foreach (PinBase pin in manager.allActivePinsInTheFrame ?? System.Array.Empty<PinBase>())
            {
                if (pin != null && pin.IsActiveGameplayPin && (pin.HasPendingGameplay || !pin.IsSettled))
                {
                    allSettled = false;
                    break;
                }
            }

            if ((allSettled && allPinsDownTimer <= 0f) || pinSettleTimer >= maxPinSettleWaitTime)
            {
                CompleteThrowFinal(manager);
            }

            return;
        }

        flightTimer += Time.deltaTime;

        if (maximumFlightTime > 0f && flightTimer >= maximumFlightTime)
        {
            RequestCompletion(manager, "maximum flight time reached");
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
            RequestCompletion(manager, "primary ball stalled");
        }
    }
    
    private System.Collections.IEnumerator AutoRethrowRoutine(RoundStateManager manager)
    {
        while (GameplayPause.IsPaused(manager)) yield return null;
        yield return new WaitForSeconds(autoRethrowDelay);
        while (GameplayPause.IsPaused(manager)) yield return null;

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
        if (currentManager == null || currentManager.IsGameplayPaused) return;
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
            BallController ball = other.GetComponentInParent<BallController>();
            if (ball != null) ball.MarkFlightEnded();
            RequestCompletion(manager, $"{other.gameObject.name} entered a completion trigger", ball);
        }
    }

    public void RequestCompletion(RoundStateManager manager, string reason, BallController source = null)
    {
        if (manager.IsGameplayPaused || !ballHasBeenReleased || !manager.ballController.IsLaunched ||
            throwCompletionRequested || manager.currentState != this) return;
        if (source == null || source == manager.ballController) primaryFlightEnded = true;
        if (waitingForPinsToSettle || throwCompletionRequested || manager.currentState != this) return;

        Debug.Log($"Throw completed: {reason}. Waiting for pins to settle.");
        waitingForPinsToSettle = true;
        pinSettleTimer = 0f;
    }

    private bool TryCompleteAllPinsDown(RoundStateManager manager)
    {
        PinBase[] pins = manager.allActivePinsInTheFrame;
        if (confirmationPins != pins)
        {
            confirmationPins = pins;
            allPinsDownTimer = 0f;
        }

        bool anyPin = false;
        bool ready = pins != null;
        int membership = 0;
        foreach (PinBase pin in pins ?? System.Array.Empty<PinBase>())
        {
            if (pin == null || !pin.IsActiveGameplayPin) continue;
            anyPin = true;
            membership = unchecked(membership * 31 + pin.GetInstanceID());
            if (!pin.CanFinishFallenPinEarly) ready = false;
        }
        if (membership != confirmationMembership) allPinsDownTimer = 0f;
        confirmationMembership = membership;

        if (manager.onLaneSquareGrid != null && manager.onLaneSquareGrid.HasUnclaimedTickets &&
            (!primaryFlightEnded || BallController.HasUnfinishedSecondaryBalls(manager))) ready = false;

        if (!ready || !anyPin)
        {
            allPinsDownTimer = 0f;
            return false;
        }

        allPinsDownTimer += Time.deltaTime;
        if (allPinsDownTimer < allPinsDownConfirmationTime) return false;

        CompletedEarly = true;
        FinalizePinResults(manager);
        CompleteThrowFinal(manager);
        return true;
    }

    public void FinalizePinResults(RoundStateManager manager)
    {
        foreach (PinBase pin in manager.allActivePinsInTheFrame ?? System.Array.Empty<PinBase>())
            if (pin != null && pin.IsActiveGameplayPin) pin.FinalizeAttempt();
        BallController.FinishAttemptBalls(manager);
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
