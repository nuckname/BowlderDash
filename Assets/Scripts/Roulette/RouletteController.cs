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
        ReturningToShop
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

    [Header("Result Tolerances (Used From Pass 6)")]
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

    private void OnEnable() => RefreshControls();

    public void OpenFromShop()
    {
        if (!isActiveAndEnabled || IsOpen || shopUI == null || !shopUI.CanUseShopControls) return;
        manager = shopUI.RoundManager;
        cameraPan = manager.cameraPanToLocation;
        if (!IsNavigationReady() || !shopUI.TrySuspendForRoulette(this))
        {
            Debug.LogWarning("Roulette is unavailable. Check ticket presenter, TV bindings, camera mappings and shop suspension assignments.", this);
            return;
        }

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
        return isActiveAndEnabled && acceptsInput && shopUI != null && shopUI.IsShopOpen;
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
            cameraPan.CanMoveTo(CameraLocation.RouletteGreen);
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
            if (!success || !isActiveAndEnabled || shopUI == null || !shopUI.IsShopOpen)
            {
                Close(true);
                return;
            }
            if (arrived == NavigationPhase.Closed) Close(false);
            else
            {
                Phase = arrived;
                if (arrived == NavigationPhase.ColourView) RevalidateDraft();
                RefreshControls();
            }
        }, out cameraMove, moving == NavigationPhase.MovingToTable ||
            moving == NavigationPhase.ReturningToShop ? 0.65f : 0.4f);
        if (!started) Close(true);
        return started;
    }

    private void RefreshControls()
    {
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
        RenderTV(redTV, showOverview, showColour, overviewInput || colourInput);
        RenderTV(blackTV, showOverview, showColour, overviewInput || colourInput);
        RenderTV(greenTV, showOverview, showColour, overviewInput || colourInput);
    }

    private void RenderTV(RouletteTVView view, bool selecting, bool close, bool interactive)
    {
        if (view == null) return;
        bool editing = close && hasDraftColour && view == GetView(selectedColour);
        view.Render(selecting, editing, draft.Stake, Wallet,
            RouletteWager.GetMaximumStake(view.Colour, Wallet), interactive);
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
        ClearDraft();
        if (wheelMotor != null) wheelMotor.StopAndClear();
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
        if (IsOpen && (shopUI == null || !shopUI.IsShopOpen)) Close(false);
        else if (Phase == NavigationPhase.MovingToColour || Phase == NavigationPhase.ColourView)
        {
            if (!IsRegisteredView(GetView(selectedColour)) || ticketPresenter == null ||
                !ticketPresenter.IsPresentingFor(this)) Close(true);
            else if (observedWallet != Wallet)
            {
                RevalidateDraft();
                RefreshControls();
            }
        }
    }

    private void OnDisable() => Close(true);
}
