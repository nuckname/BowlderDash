using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class ShopUIManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject shopGameObject;
    public Button continueButton;
    public Text ticketText;
    
    [Header("Card References")]
    public Button Card1Button;
    public Button Card2Button;
    public Button Card3Button;
    
    public Image CardVisual1;
    public Image CardVisual2;
    public Image CardVisual3;
    
    private RoundStateManager stateManager;
    
    //Continue button goes to next state.

    public void Show()
    {
        RefreshUI();
    }

    public void Hide()
    {
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

}