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

    public NavigationPhase Phase { get; private set; }
    public bool IsOpen => Phase != NavigationPhase.Closed;

    private RoundStateManager manager;
    private CameraPanToLocation cameraPan;
    private CameraPanToLocation.MoveRequest cameraMove;
    private CameraLocation colourView;
    private int navigationVersion;
    private Vector3 shopCameraPosition;
    private Quaternion shopCameraRotation;

    private void OnEnable() => RefreshControls();

    public void OpenFromShop()
    {
        if (!isActiveAndEnabled || IsOpen || shopUI == null || !shopUI.CanUseShopControls) return;
        manager = shopUI.RoundManager;
        cameraPan = manager.cameraPanToLocation;
        if (!IsNavigationReady() || !shopUI.TrySuspendForRoulette(this))
        {
            Debug.LogWarning("Roulette navigation is unavailable. Check TV groups, camera mappings and shop suspension assignments.", this);
            return;
        }

        cameraPan.TryGetCameraTarget(CameraLocation.ShopCameraLocation, out Transform shopTarget);
        shopCameraPosition = shopTarget.position;
        shopCameraRotation = shopTarget.rotation;
        if (!Navigate(CameraLocation.RouletteOverview, NavigationPhase.MovingToTable, NavigationPhase.Overview))
            Close(false);
    }

    public void SelectRed() => SelectColour(CameraLocation.RouletteRed);
    public void SelectBlack() => SelectColour(CameraLocation.RouletteBlack);
    public void SelectGreen() => SelectColour(CameraLocation.RouletteGreen);

    private void SelectColour(CameraLocation destination)
    {
        if (!CanNavigate(NavigationPhase.Overview)) return;
        colourView = destination;
        Navigate(destination, NavigationPhase.MovingToColour, NavigationPhase.ColourView);
    }

    public void BackToTable()
    {
        if (!CanNavigate(NavigationPhase.ColourView)) return;
        Navigate(CameraLocation.RouletteOverview, NavigationPhase.MovingToOverview, NavigationPhase.Overview);
    }

    public void BackToShop()
    {
        if (!CanNavigate(NavigationPhase.Overview)) return;
        Navigate(CameraLocation.ShopCameraLocation, NavigationPhase.ReturningToShop, NavigationPhase.Closed);
    }

    private bool CanNavigate(NavigationPhase expected)
    {
        return isActiveAndEnabled && Phase == expected && shopUI != null && shopUI.IsShopOpen;
    }

    private bool IsNavigationReady()
    {
        if (cameraPan == null || overviewControls == null || redCloseControls == null ||
            blackCloseControls == null || greenCloseControls == null) return false;
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
        bool overview = Phase == NavigationPhase.Overview;
        SetControls(overviewControls, IsOpen && Phase != NavigationPhase.ColourView, overview);
        bool close = Phase == NavigationPhase.ColourView;
        SetControls(redCloseControls, close && colourView == CameraLocation.RouletteRed,
            close && colourView == CameraLocation.RouletteRed);
        SetControls(blackCloseControls, close && colourView == CameraLocation.RouletteBlack,
            close && colourView == CameraLocation.RouletteBlack);
        SetControls(greenCloseControls, close && colourView == CameraLocation.RouletteGreen,
            close && colourView == CameraLocation.RouletteGreen);
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
        if (cameraPan != null) cameraPan.CancelMove(cameraMove);
        cameraMove = null;
        RefreshControls();
        if (wasOpen && restoreShopCamera && shopUI != null && shopUI.IsShopOpen &&
            cameraPan != null && cameraPan.mainCamera != null)
            cameraPan.mainCamera.transform.SetPositionAndRotation(shopCameraPosition, shopCameraRotation);
        if (shopUI != null) shopUI.ResumeFromRoulette(this);
    }

    private void LateUpdate()
    {
        if (IsOpen && (shopUI == null || !shopUI.IsShopOpen)) Close(false);
    }

    private void OnDisable() => Close(true);
}
