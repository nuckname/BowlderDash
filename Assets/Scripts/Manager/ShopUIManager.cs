using System.Linq;
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
    
    [Header("Reroll Settings")]
    public Button rerollButton;
    public int rerollCost = 1;
    
    [Header("Shop Settings")]
    public int costScalingPerRound = 3;
    public ShopCostSO shopCost;
    
    [Header("Grid Expansion")]
    public HexGrid targetGrid;
    public Button buyColumnButton;
    public Button buyRowButton;
    
    public int baseGridCost = 5;
    public float gridCostScaling = 2;
    private int totalGridUpgrades = 0;

    private RoundStateManager stateManager;
    private CardDataSO[] currentShopCards = new CardDataSO[3];
    
    private int _cardCost = 0;
    
    [Header("Remove Pin Settings")]
    public Button removePinButton;
    public string singlePinCardID = "SingleNormalPin";
    public int removePinCost = 1;
    
    public void ToggleShop(bool show, RoundStateManager manager = null)
    {
        shopGameObject.SetActive(show);
        
        if (show)
        {
            if (manager != null)
            {
                stateManager = manager;
            }
            
            GenerateShopCards();
            UpdateGridButtonsUI();
            UpdateRemovePinUI();
            UpdateRerollUI();
        }
    }

    
    private void UpdateRerollUI()
    {
        if (rerollButton != null)
        {
            TextMeshProUGUI btnText = rerollButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = $"Reroll ({rerollCost})";
            }
        }
    }

    public void RerollShop()
    {
        if (SessionData.Instance.SpendTickets(rerollCost))
        {
            GenerateShopCards();
        }
        else
        {
            Debug.LogWarning("Not enough tickets to reroll!");
        }
    }

    private void GenerateShopCards()
    {
        SetCardAlpha(Card1Button, CardVisual1, 1f);
        SetCardAlpha(Card2Button, CardVisual2, 1f);
        SetCardAlpha(Card3Button, CardVisual3, 1f);
        
        for (int i = 0; i < 3; i++)
        {
            int randomIndex = Random.Range(0, stateManager.shopCardsToPickFrom.Count);
            currentShopCards[i] = stateManager.shopCardsToPickFrom[randomIndex];
            
            UpdateButtonText(i);
        }

        UpdateCardVisual(CardVisual1, currentShopCards[0]);
        UpdateCardVisual(CardVisual2, currentShopCards[1]);
        UpdateCardVisual(CardVisual3, currentShopCards[2]);
    }

    private void UpdateCardVisual(Image cardImage, CardDataSO cardData)
    {
        if (cardData.cardVisualPrefab != null)
        { 
            GameObject card = Instantiate(cardData.cardVisualPrefab, cardImage.transform);
            card.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            cardImage.enabled = false;
        }
    }
    
    private void UpdateButtonText(int index)
    {
        Button targetButton = null;
        
        if (index == 0) targetButton = Card1Button;
        else if (index == 1) targetButton = Card2Button;
        else if (index == 2) targetButton = Card3Button;

        TextMeshProUGUI buttonText = targetButton.GetComponentInChildren<TextMeshProUGUI>();

        int currentRound = stateManager.sessionData.currentRound;
        int costModifier = 1;

        costModifier = GetShopCostModifier(currentRound, costModifier);

        // we first see shop on round 1 ?
        _cardCost = currentShopCards[index].baseCost + costModifier;
        buttonText.text = $"Buy ({_cardCost})";
    }

    private int GetShopCostModifier(int currentRound, int costModifier)
    {
        if (shopCost != null && shopCost.roundCostModifiers != null && shopCost.roundCostModifiers.Length > 0)
        {
            if (currentRound >= 0 && currentRound < shopCost.roundCostModifiers.Length)
            {
                costModifier = shopCost.roundCostModifiers[currentRound].costModifier;
            }
            else
            {
                costModifier = shopCost.roundCostModifiers[shopCost.roundCostModifiers.Length - 1].costModifier;
            }
        }

        return costModifier;
    }

    public void OnContinueClicked()
    {
        stateManager.SwitchState(stateManager.roundStartState);
    }

    public void BuyCard(int index)
    {
        if (currentShopCards[index] != null)
        {
            if (SessionData.Instance.SpendTickets(_cardCost))
            {
                DeckManager.Instance.AddCardToDeck(currentShopCards[index]);
                
                // Remove from the array so it can't be bought again
                currentShopCards[index] = null; 

                if (index == 0)
                {
                    SetCardAlpha(Card1Button, CardVisual1, 0.15f);
                }
                else if (index == 1)
                {
                    SetCardAlpha(Card2Button, CardVisual2, 0.15f);
                }
                else if (index == 2)
                {
                    SetCardAlpha(Card3Button, CardVisual3, 0.15f);
                }
            }
            else
            {
                Debug.LogWarning("Not enough tickets to buy this card!");
            }
        }
    }

    private void UpdateRemovePinUI()
    {
        if (removePinButton != null)
        {
            // Checks if there are any cards in the total deck that match the single pin ID
            bool hasSinglePin = DeckManager.Instance.totalDeck.Any(card => card.cardID == singlePinCardID);
            
            // Only allow clicking if a pin actually exists in the deck
            removePinButton.interactable = hasSinglePin;

            TextMeshProUGUI btnText = removePinButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = $"Remove Pin ({removePinCost})";
            }
        }
    }

    // Call from Button click in shop
    public void BuyRemovePin()
    {
        // Loops through deck to see if there is a single pin card in the deck
        bool hasSinglePin = DeckManager.Instance.totalDeck.Any(card => card.cardID == singlePinCardID);
        
        if (hasSinglePin && SessionData.Instance.SpendTickets(removePinCost))
        {
            DeckManager.Instance.RemoveCardByID(singlePinCardID);
            UpdateRemovePinUI();
        }
        else if (!hasSinglePin)
        {
            Debug.LogWarning("No single pins left to remove!");
        }
    }

    private int GetGridUpgradeCost()
    {
        return Mathf.RoundToInt(baseGridCost + (totalGridUpgrades * gridCostScaling));
    }

    private void UpdateGridButtonsUI()
    {
        int cost = GetGridUpgradeCost();

        if (buyColumnButton != null)
        {
            TextMeshProUGUI colText = buyColumnButton.GetComponentInChildren<TextMeshProUGUI>();
            if (colText != null) colText.text = $"+1 Column ({cost})";
        }

        if (buyRowButton != null)
        {
            TextMeshProUGUI rowText = buyRowButton.GetComponentInChildren<TextMeshProUGUI>();
            if (rowText != null) rowText.text = $"+1 Row ({cost})";
        }
    }

    public void BuyColumn()
    {
        int cost = GetGridUpgradeCost();
        if (targetGrid != null && SessionData.Instance.SpendTickets(cost))
        {
            targetGrid.columns++;
            targetGrid.GenerateGrid();
            
            HexGridVisuals gridVisuals = targetGrid.GetComponent<HexGridVisuals>();
            if (gridVisuals != null)
            {
                gridVisuals.RebuildVisuals();
                gridVisuals.RefreshColors();
            }

            totalGridUpgrades++;
            UpdateGridButtonsUI();
        }
    }

    public void BuyRow()
    {
        int cost = GetGridUpgradeCost();
        if (targetGrid != null && SessionData.Instance.SpendTickets(cost))
        {
            targetGrid.rows++;
            targetGrid.GenerateGrid();
            
            HexGridVisuals gridVisuals = targetGrid.GetComponent<HexGridVisuals>();
            if (gridVisuals != null)
            {
                gridVisuals.RebuildVisuals();
                gridVisuals.RefreshColors();
            }

            totalGridUpgrades++;
            UpdateGridButtonsUI();
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