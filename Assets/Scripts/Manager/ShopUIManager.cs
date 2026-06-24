using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class ShopUIManager : MonoBehaviour
{
    [Header("UI References")]
    public CanvasGroup canvasGroup;
    public Button continueButton;
    public Text ticketText;

    private RoundStateManager stateManager;

    // Called once by the RoundStateManager to link them together
    public void Initialize(RoundStateManager manager)
    {
        stateManager = manager;

        // Ensure the continue button tells the state machine to move to the next frame
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        Hide(); // Make sure it starts hidden
    }

    public void Show()
    {
        SetVisible(true);
        RefreshUI();
    }

    public void Hide()
    {
        SetVisible(false);
    }

    private void RefreshUI()
    {
        if (ticketText != null && stateManager != null)
        {
            // ticketText.text = $"Tickets: {stateManager.ticketEconomy.currentTickets}";
        }
    }

    private void OnContinueClicked()
    {
        if (stateManager != null)
        {
            stateManager.SwitchState(stateManager.roundStartState);
        }
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;
        
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
    }
}