using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class RouletteController : MonoBehaviour
{
    public enum NavigationPhase
    {
        Closed,
        MovingToTable,
        Overview,
        MovingToColour,
        ColourView,
        MovingToOverview,
        ReturningToShop,
        Committing,
        MovingToWheel,
        SpinningUp,
        AwaitingDraw,
        BallInPlay,
        PresentingResult,
        MovingToDraw
    }

    [SerializeField] private ShopUIManager shopUI;
    [Header("TV Control Groups")]
    [SerializeField] private CanvasGroup overviewControls;
    [SerializeField] private CanvasGroup redCloseControls;
    [SerializeField] private CanvasGroup blackCloseControls;
    [SerializeField] private CanvasGroup greenCloseControls;

    [Header("Wager Editors")]
    [SerializeField] private RouletteTVView redTV;
    [SerializeField] private RouletteTVView blackTV;
    [SerializeField] private RouletteTVView greenTV;
    [SerializeField] private SoundCueSO deniedCue;

    [Header("Wager Tickets")]
    [SerializeField] private RouletteTicketPresenter ticketPresenter;

    [Header("Wheel Physics")]
    [SerializeField] private RouletteWheelMotor wheelMotor;

    [Header("Ball Throwing")]
    [SerializeField] private RoulettePathDrawer pathDrawer;
    [SerializeField] private RouletteBallLauncher ballPrefab;
    [SerializeField] private Transform ballStart;
    [SerializeField] private Transform ballRuntimeRoot;

    public bool TryValidateThrowSetup(out string reason)
    {
        if (!TryValidateWheelSetup(out reason)) return false;
        if (pathDrawer == null || ballPrefab == null || ballStart == null || ballRuntimeRoot == null ||
            !ballRuntimeRoot.gameObject.activeInHierarchy || wheelMotor == null ||
            ballPrefab.gameObject.scene.IsValid() || IsInsideControls(ballRuntimeRoot) ||
            ballRuntimeRoot.IsChildOf(wheelMotor.transform) ||
            ballRuntimeRoot.GetComponentInParent<Rigidbody>() != null || HasControlsUnder(ballRuntimeRoot) ||
            pathDrawer.transform.IsChildOf(wheelMotor.transform) ||
            !RouletteWheelMotor.IsFinite(ballRuntimeRoot.lossyScale) ||
            (ballRuntimeRoot.lossyScale - Vector3.one).sqrMagnitude > 0.0001f ||
            !ballPrefab.HasAuthoredSetup(LayerMask.NameToLayer("RouletteBall")))
        {
            reason = "Assign the dedicated ball prefab drawer start and stationary unit scale runtime root";
            return false;
        }
        if (!pathDrawer.TryValidateSetup(ballStart, out reason)) return false;
        if (Mathf.Abs(Vector3.Dot(pathDrawer.DrawFrame.up, wheelMotor.WorldAxis)) < 0.99f)
        {
            reason = "Align the draw frame up axis with the wheel axis";
            return false;
        }
        return true;
    }

    [Header("Paid Spin")]
    [SerializeField, Tooltip("Stationary trigger enclosing the rim wheel and ball travel")]
    private BoxCollider ballBounds;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private SoundCueSO winCue;
    [SerializeField] private SoundCueSO lossCue;
    [SerializeField, Min(0.1f), Tooltip("Maximum seconds from payment until drawing is ready")]
    private float preparationTimeoutSeconds = 15f;
    [SerializeField, Min(0.1f), Tooltip("Maximum seconds from physical release until a result")]
    private float inPlayTimeoutSeconds = 45f;
    [SerializeField, Min(0.1f)] private float resultDisplaySeconds = 3f;

    [Header("Result Tolerances")]
    [SerializeField, Min(0.01f), Tooltip("Seconds the ball must stay in a single pocket")]
    private float pocketDwellSeconds = 1f;
    [SerializeField, Min(0f), Tooltip("Ball speed limit relative to the pocket in world units per second")]
    private float maximumRelativeBallSpeed = 0.15f;
    [SerializeField, Min(0f), Tooltip("Ball spin limit in radians per second")]
    private float maximumBallAngularSpeed = 2f;
    [SerializeField, Min(0f), Tooltip("Wheel speed limit in degrees per second with zero for a full stop")]
    private float maximumResultWheelSpeed = 0f;

    public float PocketDwellSeconds => pocketDwellSeconds;
    public float MaximumRelativeBallSpeed => maximumRelativeBallSpeed;
    public float MaximumBallAngularSpeed => maximumBallAngularSpeed;
    public float MaximumResultWheelSpeed => maximumResultWheelSpeed;

    public bool TryValidateWheelSetup(out string reason)
    {
        if (wheelMotor == null)
        {
            reason = "Assign the roulette wheel motor.";
            return false;
        }
        if (!RouletteWheelMotor.IsFinite(pocketDwellSeconds) || pocketDwellSeconds <= 0f ||
            !RouletteWheelMotor.IsFinite(maximumRelativeBallSpeed) || maximumRelativeBallSpeed < 0f ||
            !RouletteWheelMotor.IsFinite(maximumBallAngularSpeed) || maximumBallAngularSpeed < 0f ||
            !RouletteWheelMotor.IsFinite(maximumResultWheelSpeed) || maximumResultWheelSpeed < 0f)
        {
            reason = "Result tolerances must be finite and non-negative, with a positive pocket dwell.";
            return false;
        }
        return wheelMotor.TryValidateSetup(out reason);
    }

    public NavigationPhase Phase { get; private set; }
    public bool IsOpen => Phase != NavigationPhase.Closed;
    public int DraftStake => draft.Stake;
    public RouletteColour? DraftColour => hasDraftColour ? selectedColour : (RouletteColour?)null;

    private RoundStateManager manager;
    private CameraPanToLocation cameraPan;
    private CameraPanToLocation.MoveRequest cameraMove;
    private CameraLocation colourView;
    private int navigationVersion;
    private Vector3 shopCameraPosition;
    private Quaternion shopCameraRotation;
    private RouletteColour selectedColour;
    private bool hasDraftColour;
    private RouletteWager draft;
    private int observedWallet;
    private SessionData runSession;
    private ulong runVersion;
    private RouletteWager acceptedWager;
    private RouletteBallLauncher currentBall;
    private GameObject ownedBallObject;
    private RoulettePocket candidatePocket;
    private int nextSpinId;
    private int activeSpinId;
    private int settledSpinId;
    private int settledReturn;
    private bool resultPresentationStarted;
    private bool stakePaid;
    private bool resultSettled;
    private bool stakeRefunded;
    private float preparationAge;
    private float inPlayAge;
    private float candidateAge;
    private float resultAge;
    private float setupCheckAge;
    private string goUnavailableReason;
    private GameAudio audioService;

    private bool IsSameRun => runSession != null && manager != null &&
        ReferenceEquals(manager.sessionData, runSession) && runSession.RunVersion == runVersion;
    private bool HasUnresolvedStake => stakePaid && !resultSettled && !stakeRefunded;
    private bool IsSpinActive => activeSpinId > 0 && HasUnresolvedStake;

    private void OnEnable() => RefreshControls();

    public void OpenFromShop()
    {
        if (GameplayPause.IsInputBlocked(this)) return;
        if (!isActiveAndEnabled || IsOpen || shopUI == null || !shopUI.CanUseShopControls) return;
        manager = shopUI.RoundManager;
        cameraPan = manager.cameraPanToLocation;
        if (!IsNavigationReady() || !shopUI.TrySuspendForRoulette(this))
        {
            Debug.LogWarning("Roulette is unavailable Check navigation ticket presenter and shop suspension assignments", this);
            return;
        }
        if (!TryValidateSpinSetup(out string reason))
        {
            shopUI.ResumeFromRoulette(this);
            Debug.LogWarning($"Roulette is unavailable {reason}", this);
            return;
        }

        runSession = manager.sessionData;
        runVersion = runSession.RunVersion;
        runSession.SessionResetting += OnSessionResetting;
        audioService = GameAudio.Instance;
        if (audioService != null) audioService.WorldAudioStopped += OnWorldAudioStopped;
        SetInstruction("");

        cameraPan.TryGetCameraTarget(CameraLocation.ShopCameraLocation, out Transform shopTarget);
        shopCameraPosition = shopTarget.position;
        shopCameraRotation = shopTarget.rotation;
        if (Navigate(CameraLocation.RouletteOverview, NavigationPhase.MovingToTable, NavigationPhase.Overview))
            SetPostProcessing(true);
        else
            Close(false);
    }

    public void SelectRed() => SelectColour(redTV);
    public void SelectBlack() => SelectColour(blackTV);
    public void SelectGreen() => SelectColour(greenTV);

    public void SelectColour(RouletteTVView source)
    {
        if (!CanNavigate(NavigationPhase.Overview) || !IsRegisteredView(source) ||
            ticketPresenter == null || !ticketPresenter.IsReady) return;
        selectedColour = source.Colour;
        SetInstruction("");
        hasDraftColour = true;
        draft = default;
        RevalidateDraft();
        colourView = selectedColour == RouletteColour.Red ? CameraLocation.RouletteRed :
            selectedColour == RouletteColour.Black ? CameraLocation.RouletteBlack : CameraLocation.RouletteGreen;
        if (!Navigate(colourView, NavigationPhase.MovingToColour, NavigationPhase.ColourView))
        {
            ClearDraft();
            RefreshControls();
        }
    }

    public void BackToTable()
    {
        CancelDraft(GetView(selectedColour));
    }

    public void CancelDraft(RouletteTVView source)
    {
        if (!CanEditDraft(source)) return;
        if (!Navigate(CameraLocation.RouletteOverview, NavigationPhase.MovingToOverview, NavigationPhase.Overview)) return;
        ClearDraft();
        RefreshControls();
    }

    public void ChangeWager(RouletteTVView source, int change)
    {
        if (!CanEditDraft(source) || (change != 1 && change != -1)) return;
        RevalidateDraft();
        int maximum = RouletteWager.GetMaximumStake(selectedColour, observedWallet);
        int minimum = maximum > 0 ? 1 : 0;
        int stake = Mathf.Clamp(draft.Stake + change, minimum, maximum);
        if (stake == draft.Stake || !RouletteWager.TryCreate(selectedColour, stake, observedWallet, out RouletteWager edited))
        {
            if (deniedCue != null && deniedCue.isUI) GameAudio.Instance?.Play(deniedCue);
        }
        else
        {
            draft = edited;
            ticketPresenter.SyncDraft(this);
        }
        RefreshControls();
    }

    private bool CanEditDraft(RouletteTVView source)
    {
        return CanNavigate(NavigationPhase.ColourView) && hasDraftColour &&
            IsRegisteredView(source) && source.Colour == selectedColour && ticketPresenter != null &&
            ticketPresenter.IsPresentingFor(this);
    }

    private RouletteTVView GetView(RouletteColour colour)
    {
        switch (colour)
        {
            case RouletteColour.Red: return redTV;
            case RouletteColour.Black: return blackTV;
            case RouletteColour.Green: return greenTV;
            default: return null;
        }
    }

    private bool IsRegisteredView(RouletteTVView source)
    {
        return source != null && GetView(source.Colour) == source && source.IsConfiguredFor(this, source.Colour);
    }

    private int Wallet => manager != null && manager.sessionData != null ? manager.sessionData.tickets : 0;

    private void RevalidateDraft()
    {
        observedWallet = Wallet;
        int maximum = RouletteWager.GetMaximumStake(selectedColour, observedWallet);
        int stake = maximum > 0 ? Mathf.Clamp(draft.Stake, 1, maximum) : 0;
        RouletteWager.TryCreate(selectedColour, stake, observedWallet, out draft);
        if (ticketPresenter != null) ticketPresenter.SyncDraft(this);
    }

    private void ClearDraft()
    {
        draft = default;
        hasDraftColour = false;
        if (ticketPresenter != null) ticketPresenter.CancelDraft(this);
    }

    public void BackToShop()
    {
        if (!CanNavigate(NavigationPhase.Overview)) return;
        if (!Navigate(CameraLocation.ShopCameraLocation, NavigationPhase.ReturningToShop, NavigationPhase.Closed)) return;
        SetPostProcessing(false);
        shopUI.ShowShopForRouletteReturn(this);
    }

    private bool CanNavigate(NavigationPhase expected)
    {
        bool acceptsInput = Phase == expected ||
            (expected == NavigationPhase.Overview &&
                (Phase == NavigationPhase.MovingToTable || Phase == NavigationPhase.MovingToOverview)) ||
            (expected == NavigationPhase.ColourView && Phase == NavigationPhase.MovingToColour);
        return !GameplayPause.IsInputBlocked(this) && isActiveAndEnabled && acceptsInput && IsSameRun && shopUI != null &&
            shopUI.IsSuspendedForRoulette(this);
    }

    private bool IsNavigationReady()
    {
        if (cameraPan == null || overviewControls == null || redCloseControls == null ||
            blackCloseControls == null || greenCloseControls == null || manager.sessionData == null ||
            redTV == null || !redTV.IsConfiguredFor(this, RouletteColour.Red) ||
            blackTV == null || !blackTV.IsConfiguredFor(this, RouletteColour.Black) ||
            greenTV == null || !greenTV.IsConfiguredFor(this, RouletteColour.Green) ||
            ticketPresenter == null || !ticketPresenter.IsReady) return false;
        CanvasGroup[] groups = { overviewControls, redCloseControls, blackCloseControls, greenCloseControls };
        for (int i = 0; i < groups.Length; i++)
        {
            if (!groups[i].isActiveAndEnabled) return false;
            for (int j = 0; j < i; j++)
            {
                if (groups[i].transform.IsChildOf(groups[j].transform) ||
                    groups[j].transform.IsChildOf(groups[i].transform)) return false;
            }
        }
        return cameraPan.CanMoveTo(CameraLocation.ShopCameraLocation) &&
            cameraPan.CanMoveTo(CameraLocation.RouletteOverview) &&
            cameraPan.CanMoveTo(CameraLocation.RouletteRed) &&
            cameraPan.CanMoveTo(CameraLocation.RouletteBlack) &&
            cameraPan.CanMoveTo(CameraLocation.RouletteGreen) &&
            cameraPan.CanMoveTo(CameraLocation.RouletteDraw) &&
            cameraPan.CanMoveTo(CameraLocation.RouletteSpin);
    }

    private bool Navigate(CameraLocation destination, NavigationPhase moving, NavigationPhase arrived)
    {
        if (cameraPan == null || !cameraPan.CanMoveTo(destination)) return false;
        int version = ++navigationVersion;
        Phase = moving;
        RefreshControls();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        bool started = manager.TryMoveCamera(destination, success =>
        {
            if (version != navigationVersion || !IsOpen) return;
            cameraMove = null;
            if (!success || !isActiveAndEnabled || !IsSameRun || shopUI == null || !shopUI.IsShopOpen)
            {
                Close(true);
                return;
            }
            if (arrived == NavigationPhase.Closed) Close(false);
            else
            {
                Phase = arrived;
                if (arrived == NavigationPhase.ColourView) RevalidateDraft();
                if (arrived == NavigationPhase.AwaitingDraw) PrepareBall();
                RefreshControls();
            }
        }, out cameraMove, moving == NavigationPhase.MovingToTable ||
            moving == NavigationPhase.ReturningToShop ? 0.65f : 0.4f);
        if (!started) Close(true);
        return started;
    }

    private void RefreshControls()
    {
        bool canGo = CanCommitWager(GetView(selectedColour), out goUnavailableReason);
        // Keep controls usable while the camera moves
        bool showOverview = Phase == NavigationPhase.MovingToTable ||
            Phase == NavigationPhase.MovingToOverview || Phase == NavigationPhase.Overview;
        bool showColour = Phase == NavigationPhase.MovingToColour || Phase == NavigationPhase.ColourView;
        bool overviewInput = showOverview;
        bool colourInput = showColour;
        SetControls(overviewControls, showOverview, overviewInput);
        SetControls(redCloseControls, showColour && colourView == CameraLocation.RouletteRed,
            colourInput && colourView == CameraLocation.RouletteRed);
        SetControls(blackCloseControls, showColour && colourView == CameraLocation.RouletteBlack,
            colourInput && colourView == CameraLocation.RouletteBlack);
        SetControls(greenCloseControls, showColour && colourView == CameraLocation.RouletteGreen,
            colourInput && colourView == CameraLocation.RouletteGreen);
        RenderTV(redTV, showOverview, showColour, overviewInput || colourInput, canGo);
        RenderTV(blackTV, showOverview, showColour, overviewInput || colourInput, canGo);
        RenderTV(greenTV, showOverview, showColour, overviewInput || colourInput, canGo);
    }

    private void RenderTV(RouletteTVView view, bool selecting, bool close, bool interactive, bool canGo)
    {
        if (view == null) return;
        bool editing = close && hasDraftColour && view == GetView(selectedColour);
        view.Render(selecting, editing, draft.Stake, Wallet,
            RouletteWager.GetMaximumStake(view.Colour, Wallet), interactive, editing && canGo, goUnavailableReason);
    }

    private static void SetControls(CanvasGroup group, bool visible, bool interactive)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = interactive;
        group.blocksRaycasts = interactive;
    }

    public void CloseForShopExit() => Close(false);

    public bool UsesTicketPresenter(RouletteTicketPresenter presenter) => ticketPresenter == presenter;
    public bool IsPresentingResultFor(RouletteTicketPresenter presenter, int spinId) =>
        isActiveAndEnabled && IsSameRun && Phase == NavigationPhase.PresentingResult && resultSettled &&
        spinId > 0 && spinId == settledSpinId && ticketPresenter == presenter;

    public bool HasControlsUnder(Transform root)
    {
        return (overviewControls != null && overviewControls.transform.IsChildOf(root)) ||
            (redCloseControls != null && redCloseControls.transform.IsChildOf(root)) ||
            (blackCloseControls != null && blackCloseControls.transform.IsChildOf(root)) ||
            (greenCloseControls != null && greenCloseControls.transform.IsChildOf(root));
    }

    private void Close(bool restoreShopCamera)
    {
        bool wasOpen = IsOpen;
        ++navigationVersion;
        Phase = NavigationPhase.Closed;
        activeSpinId = 0;
        RefundUnresolvedStake();
        CleanupSpin();
        ResetTransaction();
        if (runSession != null) runSession.SessionResetting -= OnSessionResetting;
        if (audioService != null) audioService.StopOwnedTransients(this);
        if (audioService != null) audioService.WorldAudioStopped -= OnWorldAudioStopped;
        runSession = null;
        audioService = null;
        SetInstruction("");
        ClearDraft();
        if (cameraPan != null) cameraPan.CancelMove(cameraMove);
        cameraMove = null;
        RefreshControls();
        if (wasOpen && restoreShopCamera && shopUI != null && shopUI.IsShopOpen &&
            cameraPan != null && cameraPan.mainCamera != null)
            cameraPan.mainCamera.transform.SetPositionAndRotation(shopCameraPosition, shopCameraRotation);
        if (wasOpen && manager != null && manager.currentState == manager.shopState)
            SetPostProcessing(false);
        if (shopUI != null) shopUI.ResumeFromRoulette(this);
    }

    private void SetPostProcessing(bool enabled)
    {
        if (manager != null && manager.postProcessingEffects != null)
            manager.postProcessingEffects.SetActive(enabled);
    }

    private void LateUpdate()
    {
        if (GameplayPause.IsPaused(this)) return;
        if (IsOpen && (!IsSameRun || shopUI == null || !shopUI.IsSuspendedForRoulette(this))) Close(false);
        else if (IsSpinActive) UpdateSpinPreparation();
        else if (Phase == NavigationPhase.PresentingResult)
        {
            if (cameraMove != null && cameraMove.IsActive) return;
            if (resultSettled)
            {
                if (!resultPresentationStarted)
                {
                    resultPresentationStarted = true;
                    if (ticketPresenter != null)
                        ticketPresenter.BeginResult(this, settledSpinId, acceptedWager.Stake, settledReturn);
                }
                if (ticketPresenter != null && ticketPresenter.IsResultPlayingFor(this, settledSpinId)) return;
            }
            resultAge += Time.deltaTime;
            if (resultAge >= resultDisplaySeconds)
            {
                if (audioService != null) audioService.StopOwnedTransients(this);
                CleanupSpin();
                ClearDraft();
                ResetTransaction();
                Phase = NavigationPhase.Overview;
                RefreshControls();
            }
        }
        else if (Phase == NavigationPhase.MovingToColour || Phase == NavigationPhase.ColourView)
        {
            if (!IsRegisteredView(GetView(selectedColour)) || ticketPresenter == null ||
                !ticketPresenter.IsPresentingFor(this)) Close(true);
            else if (observedWallet != Wallet)
            {
                RevalidateDraft();
                RefreshControls();
            }
            else
            {
                setupCheckAge += Time.deltaTime;
                if (setupCheckAge >= 0.25f) { setupCheckAge = 0f; RefreshControls(); }
            }
        }
    }

    public bool TryValidateSpinSetup(out string reason)
    {
        if (!TryValidateThrowSetup(out reason)) return false;
        int boundsLayer = LayerMask.NameToLayer("RouletteBounds");
        int ballLayer = LayerMask.NameToLayer("RouletteBall");
        int ticketLayer = LayerMask.NameToLayer("RouletteTicket");
        if (ballBounds == null || !ballBounds.enabled || !ballBounds.gameObject.activeInHierarchy ||
            !ballBounds.isTrigger || ballBounds.attachedRigidbody != null ||
            ballBounds.GetComponentInParent<RouletteWheelMotor>() != null ||
            ballBounds.transform.IsChildOf(ballRuntimeRoot) || IsInsideControls(ballBounds.transform) ||
            boundsLayer < 0 || ballLayer < 0 || ticketLayer < 0 || ballBounds.gameObject.layer != boundsLayer ||
            ballBounds.includeLayers.value != 0 || ballBounds.excludeLayers.value != 0 ||
            Physics.GetIgnoreLayerCollision(boundsLayer, ballLayer) ||
            !Physics.GetIgnoreLayerCollision(boundsLayer, ticketLayer) ||
            !RouletteWheelMotor.IsFinite(ballBounds.transform.lossyScale) ||
            (ballBounds.transform.lossyScale - Vector3.one).sqrMagnitude > 0.0001f ||
            !RouletteWheelMotor.IsFinite(ballBounds.size) || ballBounds.size.x <= 0f ||
            ballBounds.size.y <= 0f || ballBounds.size.z <= 0f || !ContainsInBounds(ballStart.position))
        {
            reason = "Assign a stationary unit scale RouletteBounds trigger containing BallStart with ball collisions on and ticket collisions off";
            return false;
        }
        if (instructionText == null || !instructionText.isActiveAndEnabled ||
            IsInsideControls(instructionText.transform) || instructionText.transform.IsChildOf(ballRuntimeRoot) ||
            instructionText.GetComponentInParent<RouletteWheelMotor>() != null)
        {
            reason = "Assign active instruction text outside the TV control groups wheel and runtime ball root";
            return false;
        }
        if (!Positive(preparationTimeoutSeconds) || !Positive(inPlayTimeoutSeconds) || !Positive(resultDisplaySeconds))
        {
            reason = "Preparation timeout in play timeout and result duration must be finite and positive";
            return false;
        }
        return true;
    }

    private bool IsInsideControls(Transform item)
    {
        return (overviewControls != null && item.IsChildOf(overviewControls.transform)) ||
            (redCloseControls != null && item.IsChildOf(redCloseControls.transform)) ||
            (blackCloseControls != null && item.IsChildOf(blackCloseControls.transform)) ||
            (greenCloseControls != null && item.IsChildOf(greenCloseControls.transform));
    }

    private bool CanCommitWager(RouletteTVView source, out string reason)
    {
        reason = "Waiting for the camera";
        if (Phase != NavigationPhase.ColourView || !CanEditDraft(source) || !IsSameRun ||
            manager.currentState != manager.shopState || !ReferenceEquals(runSession, SessionData.Instance) ||
            IsSpinActive || currentBall != null) return false;
        if (!IsNavigationReady() || InGameScoreUi.Instance == null ||
            InGameScoreUi.Instance.inGameUiTicketsText == null)
        {
            reason = "Check roulette navigation and shared ticket display";
            return false;
        }
        if (!RouletteWager.TryCreate(selectedColour, draft.Stake, Wallet, out _))
        {
            reason = "Choose an affordable safe wager";
            return false;
        }
        if (!TryValidateSpinSetup(out reason)) return false;
        if (!wheelMotor.IsStopped)
        {
            reason = "Wait for the wheel to stop";
            return false;
        }
        return true;
    }

    public void CommitWager(RouletteTVView source)
    {
        if (!CanEditDraft(source)) return;
        RevalidateDraft();
        if (!CanCommitWager(source, out string reason))
        {
            if (deniedCue != null && deniedCue.isUI) GameAudio.Instance?.Play(deniedCue);
            SetInstruction(reason);
            RefreshControls();
            return;
        }
        ResetTransaction();
        RouletteWager.TryCreate(selectedColour, draft.Stake, Wallet, out acceptedWager);
        unchecked { nextSpinId++; }
        if (nextSpinId <= 0) nextSpinId = 1;
        activeSpinId = nextSpinId;
        Phase = NavigationPhase.Committing;
        RefreshControls();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        // Lock the accepted wager before the wallet refresh can call back
        stakePaid = true;
        if (!runSession.SpendTickets(acceptedWager.Stake))
        {
            stakePaid = false;
            activeSpinId = 0;
            Phase = NavigationPhase.ColourView;
            RevalidateDraft();
            RefreshControls();
            return;
        }
        if (!IsSameRun || Phase != NavigationPhase.Committing || !IsSpinActive) { Close(true); return; }
        SetInstruction("Spinning up the wheel");
        if (!wheelMotor.BeginSpin()) { VoidSpin("Wheel unavailable Try again"); return; }
        Navigate(CameraLocation.RouletteOverview, NavigationPhase.MovingToWheel, NavigationPhase.SpinningUp);
    }

    private bool MatchesBall(RouletteBallLauncher ball, int spinId)
    {
        return isActiveAndEnabled && IsSameRun && IsSpinActive && spinId == activeSpinId &&
            ReferenceEquals(ball, currentBall) && ball != null && IsOpen &&
            shopUI != null && shopUI.IsSuspendedForRoulette(this);
    }

    private void UpdateSpinPreparation()
    {
        setupCheckAge += Time.deltaTime;
        if (setupCheckAge >= 0.25f)
        {
            setupCheckAge = 0f;
            if (!TryValidateSpinSetup(out _) || ticketPresenter == null || !ticketPresenter.IsPresentingFor(this))
            {
                VoidSpin("Roulette setup changed Try again");
                return;
            }
        }
        if (Phase == NavigationPhase.MovingToWheel || Phase == NavigationPhase.SpinningUp ||
            Phase == NavigationPhase.MovingToDraw)
        {
            preparationAge += Time.deltaTime;
            if (preparationAge >= preparationTimeoutSeconds || wheelMotor == null || !wheelMotor.isActiveAndEnabled)
            {
                VoidSpin("Wheel preparation timed out Try again");
                return;
            }
            if (Phase == NavigationPhase.SpinningUp && wheelMotor.IsAtFullSpeed)
            {
                SetInstruction("Moving closer to draw");
                if (!Navigate(CameraLocation.RouletteDraw, NavigationPhase.MovingToDraw, NavigationPhase.AwaitingDraw))
                    Close(true);
            }
        }
        else if (Phase == NavigationPhase.AwaitingDraw || Phase == NavigationPhase.BallInPlay)
        {
            if (currentBall == null || !currentBall.isActiveAndEnabled || currentBall.Body == null ||
                !RouletteWheelMotor.IsFinite(currentBall.Body.position) ||
                !FiniteRotation(currentBall.Body.rotation) ||
                !RouletteWheelMotor.IsFinite(currentBall.Body.linearVelocity) ||
                !RouletteWheelMotor.IsFinite(currentBall.Body.angularVelocity) ||
                wheelMotor == null || !wheelMotor.isActiveAndEnabled ||
                (Phase == NavigationPhase.AwaitingDraw && !wheelMotor.IsAtFullSpeed))
                VoidSpin("Ball or wheel unavailable Try again");
        }
    }

    private void PrepareBall()
    {
        if (!IsSpinActive || Phase != NavigationPhase.AwaitingDraw || !TryValidateSpinSetup(out _) ||
            !wheelMotor.IsAtFullSpeed)
        {
            VoidSpin("Throw setup unavailable Try again");
            return;
        }
        Phase = NavigationPhase.AwaitingDraw;
        currentBall = Instantiate(ballPrefab, ballStart.position, ballStart.rotation, ballRuntimeRoot);
        ownedBallObject = currentBall.gameObject;
        RouletteBallLauncher ball = currentBall;
        int spinId = activeSpinId;
        ball.Released += OnBallReleased;
        ball.Invalidated += OnBallInvalidated;
        ball.InstructionChanged += OnBallInstruction;
        SetInstruction("Draw from the ball then release or cross the edge to throw");
        if (!ball.Prepare(this, spinId, ballStart, pathDrawer, wheelMotor, () => MatchesBall(ball, spinId)))
            VoidSpin("Ball preparation failed Try again");
    }

    private void OnBallReleased(RouletteBallLauncher ball, int spinId)
    {
        if (!MatchesBall(ball, spinId) || Phase != NavigationPhase.AwaitingDraw ||
            ball.Phase != RouletteBallLauncher.LaunchPhase.Released) return;
        Phase = NavigationPhase.BallInPlay;
        inPlayAge = 0f;
        candidateAge = 0f;
        candidatePocket = null;
        wheelMotor.RequestSlowdown();
        SetInstruction("Waiting for the ball to settle");
        if (!Navigate(CameraLocation.RouletteSpin, NavigationPhase.BallInPlay, NavigationPhase.BallInPlay))
            Close(true);
    }

    private void OnBallInvalidated(RouletteBallLauncher ball, int spinId)
    {
        if (MatchesBall(ball, spinId)) VoidSpin("Throw interrupted Try again");
    }

    private void OnBallInstruction(RouletteBallLauncher ball, int spinId, string message)
    {
        if (MatchesBall(ball, spinId) && Phase == NavigationPhase.AwaitingDraw) SetInstruction(message);
    }

    private void FixedUpdate()
    {
        if (GameplayPause.IsPaused(this)) return;
        if (Phase != NavigationPhase.BallInPlay || !IsSpinActive) return;
        if (!IsSameRun || shopUI == null || !shopUI.IsSuspendedForRoulette(this)) { Close(false); return; }
        if (currentBall == null || !currentBall.isActiveAndEnabled || currentBall.Body == null ||
            currentBall.Phase != RouletteBallLauncher.LaunchPhase.Released || wheelMotor == null ||
            !wheelMotor.isActiveAndEnabled || ballBounds == null || !ballBounds.enabled ||
            !ballBounds.gameObject.activeInHierarchy)
        {
            VoidSpin("Ball or bounds unavailable Try again");
            return;
        }
        Rigidbody ball = currentBall.Body;
        if (ball.isKinematic || !ball.detectCollisions || !RouletteWheelMotor.IsFinite(ball.position) ||
            !FiniteRotation(ball.rotation) ||
            !RouletteWheelMotor.IsFinite(ball.linearVelocity) || !RouletteWheelMotor.IsFinite(ball.angularVelocity) ||
            !ContainsInBounds(ball.worldCenterOfMass))
        {
            VoidSpin("Ball left the table Try again");
            return;
        }
        inPlayAge += Time.fixedDeltaTime;
        if (inPlayAge >= inPlayTimeoutSeconds)
        {
            VoidSpin("Ball did not settle Try again");
            return;
        }
        Vector3 relativeVelocity = ball.linearVelocity - wheelMotor.GetPointVelocity(ball.worldCenterOfMass);
        bool stable = RouletteWheelMotor.IsFinite(relativeVelocity) &&
            RouletteWheelMotor.IsFinite(wheelMotor.SpeedDegreesPerSecond) &&
            wheelMotor.SpeedDegreesPerSecond <= maximumResultWheelSpeed &&
            relativeVelocity.magnitude <= maximumRelativeBallSpeed &&
            ball.angularVelocity.magnitude <= maximumBallAngularSpeed;
        if (!stable || !wheelMotor.TryGetPocketCandidate(ball, out RoulettePocket pocket))
        {
            candidatePocket = null;
            candidateAge = 0f;
            return;
        }
        if (candidatePocket != pocket)
        {
            candidatePocket = pocket;
            candidateAge = 0f;
            return;
        }
        candidateAge += Time.fixedDeltaTime;
        if (candidateAge >= pocketDwellSeconds) SettleSpin(pocket);
    }

    private void SettleSpin(RoulettePocket pocket)
    {
        if (!IsSpinActive || !IsSameRun || Phase != NavigationPhase.BallInPlay || pocket == null) return;
        if (!TryValidateSpinSetup(out _) || ticketPresenter == null || !ticketPresenter.IsPresentingFor(this))
        {
            VoidSpin("Result setup unavailable Try again");
            return;
        }
        bool won = pocket.Colour == acceptedWager.Colour;
        int award = won ? acceptedWager.TotalReturn : 0;
        if (!CanCredit(award)) { VoidSpin("Ticket balance changed Try again"); return; }

        // Final results survive all later cleanup
        settledSpinId = activeSpinId;
        settledReturn = award;
        resultSettled = true;
        activeSpinId = 0;
        if (award > 0) runSession.AddTickets(award);
        RefreshWallet();
        CleanupSpin(true);

        SetInstruction(won ? $"{pocket.Colour} Win {award} tickets total" : $"{pocket.Colour} No return");
        Phase = NavigationPhase.PresentingResult;
        resultAge = 0f;
        if (!Navigate(CameraLocation.RouletteOverview, NavigationPhase.PresentingResult, NavigationPhase.PresentingResult))
        {
            Close(true);
            return;
        }
        SoundCueSO cue = won ? winCue : lossCue;
        if (cue != null) audioService?.PlayOwned(cue, this, transform.position);
    }

    private bool CanCredit(int amount) => IsSameRun && amount >= 0 && runSession.tickets >= 0 &&
        (long)runSession.tickets + amount <= int.MaxValue;

    private bool RefundUnresolvedStake()
    {
        if (!HasUnresolvedStake) return stakeRefunded;
        if (!IsSameRun) return false;
        if (!CanCredit(acceptedWager.Stake))
        {
            Debug.LogError("Roulette refund exceeds the wallet capacity Check external ticket writes", this);
            return false;
        }
        stakeRefunded = true;
        runSession.AddTickets(acceptedWager.Stake);
        RefreshWallet();
        return true;
    }

    private void VoidSpin(string reason)
    {
        if (!IsSpinActive) return;
        if (!IsSameRun) { Close(false); return; }
        // Invalidate callbacks before refunding or retiring owned objects
        activeSpinId = 0;
        ++navigationVersion;
        Phase = NavigationPhase.PresentingResult;
        if (cameraPan != null) cameraPan.CancelMove(cameraMove);
        cameraMove = null;
        bool refunded = RefundUnresolvedStake();
        CleanupSpin();
        ClearDraft();
        resultAge = 0f;
        SetInstruction($"{reason}\n" + (refunded ? "Stake refunded" : "Refund unavailable Check ticket balance"));
        RefreshControls();
        if (cameraPan != null && cameraPan.CanMoveTo(CameraLocation.RouletteOverview))
            Navigate(CameraLocation.RouletteOverview, NavigationPhase.PresentingResult, NavigationPhase.PresentingResult);
        else Close(true);
    }

    private void CleanupSpin(bool keepResultBall = false)
    {
        RouletteBallLauncher ball = currentBall;
        candidatePocket = null;
        candidateAge = 0f;
        if (ball != null)
        {
            ball.Released -= OnBallReleased;
            ball.Invalidated -= OnBallInvalidated;
            ball.InstructionChanged -= OnBallInstruction;
            ball.ResetForCleanup();
        }
        if (!keepResultBall)
        {
            currentBall = null;
            if (ownedBallObject != null)
            {
                ownedBallObject.SetActive(false);
                Destroy(ownedBallObject);
            }
            ownedBallObject = null;
        }
        if (pathDrawer != null) pathDrawer.Stop();
        if (wheelMotor != null) wheelMotor.StopAndClear();
    }

    private void ResetTransaction()
    {
        settledSpinId = settledReturn = 0;
        resultPresentationStarted = false;
        acceptedWager = default;
        stakePaid = resultSettled = stakeRefunded = false;
        activeSpinId = 0;
        preparationAge = inPlayAge = resultAge = setupCheckAge = 0f;
    }

    private void RefreshWallet()
    {
        if (IsSameRun && InGameScoreUi.Instance != null) InGameScoreUi.Instance.UpdateTicketText(runSession.tickets);
    }

    private void SetInstruction(string message)
    {
        if (instructionText == null) return;
        instructionText.raycastTarget = false;
        instructionText.text = message;
    }

    private bool ContainsInBounds(Vector3 point)
    {
        if (ballBounds == null || !RouletteWheelMotor.IsFinite(point)) return false;
        Vector3 local = ballBounds.transform.InverseTransformPoint(point) - ballBounds.center;
        Vector3 half = ballBounds.size * 0.5f;
        return RouletteWheelMotor.IsFinite(local) && Mathf.Abs(local.x) <= half.x &&
            Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    private void OnSessionResetting() => Close(false);
    private void OnWorldAudioStopped()
    {
        if (audioService != null) audioService.StopOwnedTransients(this);
        if (IsSpinActive) VoidSpin("Spin interrupted Try again");
    }
    private static bool Positive(float value) => RouletteWheelMotor.IsFinite(value) && value > 0f;
    private static bool FiniteRotation(Quaternion value) => RouletteWheelMotor.IsFinite(value.x) &&
        RouletteWheelMotor.IsFinite(value.y) && RouletteWheelMotor.IsFinite(value.z) && RouletteWheelMotor.IsFinite(value.w);
    private void OnDisable() => Close(true);
}
