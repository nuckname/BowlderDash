// ShopUIManager.cs

using System.Collections.Generic;
using TMPro;
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
    
    [Header("Shop Settings")]
    public int costScalingPerRound = 3;
    
    private RoundStateManager stateManager;
    private CardDataSO[] currentShopCards = new CardDataSO[3];
    
    //Continue button goes to next state.

    public void ShowShop(RoundStateManager manager)
    {
        shopGameObject.SetActive(true);
        
        stateManager = manager;
        GenerateShopCards();
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
        SetCardAlpha(Card1Button, CardVisual1, 1f);
        SetCardAlpha(Card2Button, CardVisual2, 1f);
        SetCardAlpha(Card3Button, CardVisual3, 1f);
        
        for (int i = 0; i < 3; i++)
        {
            int randomIndex = Random.Range(0, stateManager.masterCardList.Count);
            currentShopCards[i] = stateManager.masterCardList[randomIndex];
            
            UpdateButtonText(i);
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
    
    private void UpdateButtonText(int index)
    {
        Button targetButton = null;
        
        if (index == 0) targetButton = Card1Button;
        else if (index == 1) targetButton = Card2Button;
        else if (index == 2) targetButton = Card3Button;

        TextMeshProUGUI buttonText = targetButton.GetComponentInChildren<TextMeshProUGUI>();
        
        if (buttonText != null)
        {
            int cardCost = currentShopCards[index].baseCost + (stateManager.currentRound * costScalingPerRound);
            buttonText.text = $"Buy ({cardCost})";
        }
       
    }

    public void OnContinueClicked()
    {
        stateManager.SwitchState(stateManager.roundStartState);
    }

    public void BuyCard(int index)
    {
        if (currentShopCards[index] != null)
        {
            int cardCost = currentShopCards[index].baseCost + (stateManager.currentRound * costScalingPerRound);
            
            DeckManager.Instance.AddCardToDeck(currentShopCards[index]);
            
            if (SessionData.Instance.SpendTickets(cardCost))
            {
                DeckManager.Instance.AddCardToDeck(currentShopCards[index]);
                
                // Remove from the array so it can't be bought again
                currentShopCards[index] = null; 

                if (index == 0)
                {
                    SetCardAlpha(Card1Button, CardVisual1, 0.25f);
                }
                else if (index == 1)
                {
                    SetCardAlpha(Card2Button, CardVisual2, 0.25f);
                }
                else if (index == 2)
                {
                    SetCardAlpha(Card3Button, CardVisual3, 0.25f);
                }
            }
            else
            {
                Debug.LogWarning("Not enough tickets to buy this card!");
            }
        }
    }
    
    // AI https://share.gemini.google/lIdrnSIvV63b
    private void SetCardAlpha(Button button, Image visual, float alpha)
    {
        // Ensure they remain active in the hierarchy
        button.gameObject.SetActive(true);
        visual.gameObject.SetActive(true);
        
        // Disable interaction if transparent so it can't be clicked
        button.interactable = (alpha > 0f);

        // Update visual image alpha
        Color visualColor = visual.color;
        visualColor.a = alpha;
        visual.color = visualColor;

        // Update button background alpha (if it has an Image component)
        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage != null)
        {
            Color buttonColor = buttonImage.color;
            buttonColor.a = alpha;
            buttonImage.color = buttonColor;
        }
    }
}