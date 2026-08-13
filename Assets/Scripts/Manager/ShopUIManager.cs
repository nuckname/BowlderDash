// ShopUIManager.cs

using System.Collections.Generic;
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
    private CardDataSO[] currentShopCards = new CardDataSO[3];
    
    //Continue button goes to next state.

    public void ShowShop(RoundStateManager manager)
    {
        shopGameObject.SetActive(true);
        
        stateManager = manager;
        GenerateShopCards();
        RefreshUI();
    }

    public void Hide()
    {
        if (shopGameObject != null)
        {
            shopGameObject.SetActive(false);
        }
    }

    private void GenerateShopCards()
    {
        for (int i = 0; i < 3; i++)
        {
            int randomIndex = Random.Range(0, stateManager.masterCardList.Count);
            currentShopCards[i] = stateManager.masterCardList[randomIndex];
        }

        UpdateCardVisual(CardVisual1, currentShopCards[0]);
        UpdateCardVisual(CardVisual2, currentShopCards[1]);
        UpdateCardVisual(CardVisual3, currentShopCards[2]);
    }

    private void UpdateCardVisual(Image cardImage, CardDataSO cardData)
    {
        Image prefabImage = cardData.cardVisualPrefab.GetComponentInChildren<Image>();
        
        if (prefabImage != null)
        {
            cardImage.sprite = prefabImage.sprite;
        }
    }

    private void RefreshUI()
    {
        if (ticketText != null && stateManager != null)
        {
            // ticketText.text = $"Tickets: {stateManager.ticketEconomy.currentTickets}";
        }
    }

    public void OnContinueClicked()
    {
        if (stateManager != null)
        {
            stateManager.SwitchState(stateManager.roundStartState);
        }
    }
}