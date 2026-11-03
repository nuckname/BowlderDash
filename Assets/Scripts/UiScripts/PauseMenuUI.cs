using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private GamePauseController pauseController;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private UnityEngine.UI.Button resumeButton;
    [SerializeField] private UnityEngine.UI.Button optionsButton;
    [SerializeField] private UnityEngine.UI.Button resetRunButton;
    [SerializeField] private UnityEngine.UI.Button mainMenuButton;
    private int openedFrame;
    private int releasedFrame;
    private bool menuInputReady;

    public bool IsVisible => panelRoot != null && panelRoot.activeInHierarchy;

    private void Awake() => Hide();

    public bool Initialize(GamePauseController owner)
    {
        if (owner == null || !isActiveAndEnabled || owner.gameObject.scene != gameObject.scene ||
            panelRoot == null || panelRoot == gameObject || !panelRoot.transform.IsChildOf(transform)) return false;

        UnityEngine.UI.Button[] buttons = { resumeButton, optionsButton, resetRunButton, mainMenuButton };
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null || !buttons[i].transform.IsChildOf(panelRoot.transform)) return false;
            for (int j = 0; j < i; j++) if (buttons[i] == buttons[j]) return false;
        }

        pauseController = owner;
        pauseController.RegisterMenuInputRoot(transform);
        resumeButton.interactable = true;
        optionsButton.interactable = true;
        var navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
            selectOnUp = optionsButton, selectOnDown = optionsButton,
            selectOnLeft = resumeButton, selectOnRight = resumeButton };
        resumeButton.navigation = navigation;
        optionsButton.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
            selectOnUp = resumeButton, selectOnDown = resumeButton,
            selectOnLeft = optionsButton, selectOnRight = optionsButton };
        for (int i = 2; i < buttons.Length; i++)
        {
            buttons[i].interactable = false;
            buttons[i].navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        }
        return true;
    }

    public void Show()
    {
        openedFrame = Time.frameCount;
        releasedFrame = -1;
        menuInputReady = false;
        resumeButton.interactable = false;
        optionsButton.interactable = false;
        panelRoot.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
    }

    public void Hide()
    {
        if (panelRoot == null) return;
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.transform.IsChildOf(panelRoot.transform))
            EventSystem.current.SetSelectedGameObject(null);
        panelRoot.SetActive(false);
    }

    public void EnsureSelection()
    {
        if (!IsVisible) return;
        if (!menuInputReady && Time.frameCount > openedFrame)
        {
            if (releasedFrame >= 0 && Time.frameCount > releasedFrame)
            {
                menuInputReady = true;
                resumeButton.interactable = true;
                optionsButton.interactable = true;
            }
            else if (!Input.anyKey && !Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2))
                releasedFrame = Time.frameCount;
        }
        if (EventSystem.current == null) return;
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || !selected.transform.IsChildOf(panelRoot.transform) ||
            !selected.TryGetComponent<UnityEngine.UI.Selectable>(out var control) ||
            !control.IsActive() || !control.IsInteractable())
            EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
    }

    public void OnResumeClicked()
    {
        if (menuInputReady && IsVisible && pauseController != null && pauseController.IsPaused)
            pauseController.Resume();
    }

    public void OnOptionsClicked()
    {
        if (menuInputReady && IsVisible && pauseController != null && pauseController.IsPaused)
            pauseController.TryOpenOptions();
    }
}
