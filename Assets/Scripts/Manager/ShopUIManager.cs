using System.Collections.Generic;
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
    public List<Button> cardButtons = new List<Button>();
    public List<Image> cardVisuals = new List<Image>();
    
    [Header("Reroll Settings")]
    public Button rerollButton;
    public int rerollCost = 1;
    
    [Header("Shop Settings")]
    public int costScalingPerRound = 3;
    [Min(0.01f)]
    [SerializeField] private float shopCardVisualScale = 0.5f;
    public ShopCostSO shopCost;
    
    [Header("Grid Expansion")]
    public HexGrid targetGrid;
    public Button buyColumnButton;
    public Button buyRowButton;
    
    public int baseGridCost = 5;
    public float gridCostScaling = 2;
    private int totalGridUpgrades = 0;

    private RoundStateManager stateManager;
    private List<CardDataSO> currentShopCards = new List<CardDataSO>();
    private List<GameObject> spawnedShopCardVisuals = new List<GameObject>();
    
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
        currentShopCards.Clear();

        while (spawnedShopCardVisuals.Count < cardButtons.Count)
        {
            spawnedShopCardVisuals.Add(null);
        }

        for (int i = 0; i < cardButtons.Count; i++)
        {
            int randomIndex = Random.Range(0, stateManager.shopCardsToPickFrom.Count);
            currentShopCards.Add(stateManager.shopCardsToPickFrom[randomIndex]);
            
            UpdateButtonText(i);
            UpdateCardVisual(i, cardVisuals[i], currentShopCards[i]);
            SetCardAlpha(i, cardButtons[i], cardVisuals[i], 1f);
        }
    }

    private void UpdateCardVisual(int index, Image cardImage, CardDataSO cardData)
    {
        ClearCardVisual(index, cardImage);

        if (cardImage == null || cardData == null || cardData.cardVisualPrefab == null)
        {
            Debug.LogWarning($"Cannot display shop card at index {index}: card visual data is incomplete.");
            return;
        }

        GameObject card = Instantiate(cardData.cardVisualPrefab, cardImage.transform);
        spawnedShopCardVisuals[index] = card;

        RectTransform cardRect = card.GetComponent<RectTransform>();
        if (cardRect != null)
        {
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
        }

        card.transform.localRotation = Quaternion.identity;
        card.transform.localScale = Vector3.one * shopCardVisualScale;

        CardVisualPresenter presenter = card.GetComponentInChildren<CardVisualPresenter>(true);
        if (presenter != null)
        {
            presenter.Bind(cardData);
        }
        else
        {
            Debug.LogWarning($"Shop card visual '{card.name}' has no CardVisualPresenter.");
        }

        CanvasGroup cardGroup = card.GetComponent<CanvasGroup>();
        if (cardGroup != null)
        {
            cardGroup.enabled = true;
            cardGroup.alpha = 1f;
            cardGroup.interactable = false;
            cardGroup.blocksRaycasts = false;
        }

        cardImage.enabled = false;
    }

    private void ClearCardVisual(int index, Image cardImage)
    {
        GameObject trackedVisual = spawnedShopCardVisuals[index];
        if (trackedVisual != null)
        {
            trackedVisual.SetActive(false);
            Destroy(trackedVisual);
            spawnedShopCardVisuals[index] = null;
        }

        if (cardImage == null)
        {
            return;
        }

        cardImage.enabled = true;

        for (int childIndex = cardImage.transform.childCount - 1; childIndex >= 0; childIndex--)
        {
            Transform child = cardImage.transform.GetChild(childIndex);
            if (child.gameObject == trackedVisual)
            {
                continue;
            }

            if (child.GetComponent<CardVisualPresenter>() != null || child.GetComponent<CardAnimator>() != null)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }
    }
    
    private void UpdateButtonText(int index)
    {
        Button targetButton = cardButtons[index];

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
        if (index < currentShopCards.Count && currentShopCards[index] != null)
        {
            if (SessionData.Instance.SpendTickets(_cardCost))
            {
                DeckManager.Instance.AddCardToDeck(currentShopCards[index]);
                
                // Remove from the array so it can't be bought again
                currentShopCards[index] = null; 

                SetCardAlpha(index, cardButtons[index], cardVisuals[index], 0.15f);
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
    private void SetCardAlpha(int index, Button button, Image visual, float alpha)
    {
        // Ensure they remain active in the hierarchy
        button.gameObject.SetActive(true);
        visual.gameObject.SetActive(true);
        
        // Disable interaction if transparent so it can't be clicked
        button.interactable = alpha >= 0.99f;

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

        GameObject spawnedVisual = spawnedShopCardVisuals[index];
        if (spawnedVisual != null)
        {
            CanvasGroup cardGroup = spawnedVisual.GetComponent<CanvasGroup>();
            if (cardGroup == null)
            {
                cardGroup = spawnedVisual.AddComponent<CanvasGroup>();
            }

            cardGroup.enabled = true;
            cardGroup.alpha = alpha;
            cardGroup.interactable = false;
            cardGroup.blocksRaycasts = false;
        }
    }
}