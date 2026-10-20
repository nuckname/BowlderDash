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
            Debug.LogWarning("Roulette is unavailable. Check TV editor bindings, control groups, camera mappings and shop suspension assignments.", this);
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
        if (!CanNavigate(NavigationPhase.Overview) || !IsRegisteredView(source)) return;
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
        else draft = edited;
        RefreshControls();
    }

    private bool CanEditDraft(RouletteTVView source)
    {
        return CanNavigate(NavigationPhase.ColourView) && hasDraftColour &&
            IsRegisteredView(source) && source.Colour == selectedColour;
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
    }

    private void ClearDraft()
    {
        draft = default;
        hasDraftColour = false;
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
        return isActiveAndEnabled && Phase == expected && shopUI != null && shopUI.IsShopOpen;
    }

    private bool IsNavigationReady()
    {
        if (cameraPan == null || overviewControls == null || redCloseControls == null ||
            blackCloseControls == null || greenCloseControls == null || manager.sessionData == null ||
            redTV == null || !redTV.IsConfiguredFor(this, RouletteColour.Red) ||
            blackTV == null || !blackTV.IsConfiguredFor(this, RouletteColour.Black) ||
            greenTV == null || !greenTV.IsConfiguredFor(this, RouletteColour.Green)) return false;
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
        NavigationPhase previous = Phase;
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
        }, out cameraMove);
        if (!started)
        {
            Phase = previous;
            RefreshControls();
        }
        return started;
    }

    private void RefreshControls()
    {
        // Show the destination immediately; unlock its controls only on arrival.
        bool showOverview = Phase == NavigationPhase.MovingToTable ||
            Phase == NavigationPhase.MovingToOverview || Phase == NavigationPhase.Overview;
        bool showColour = Phase == NavigationPhase.MovingToColour || Phase == NavigationPhase.ColourView;
        bool overviewInput = Phase == NavigationPhase.Overview;
        bool colourInput = Phase == NavigationPhase.ColourView;
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
            if (!IsRegisteredView(GetView(selectedColour))) Close(true);
            else if (observedWallet != Wallet)
            {
                RevalidateDraft();
                RefreshControls();
            }
        }
    }

    private void OnDisable() => Close(true);
}
