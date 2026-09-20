using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

// Shop buttons wiggle
//https://share.gemini.google/d4vXsXZzI4Dg

// buttons / dotweens https://share.gemini.google/qaanLhiA9KYZ

[RequireComponent(typeof(CanvasGroup))]
public class ShopUIManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject shopGameObject;
    public Button continueButton;
    public Text ticketText;
    
    [Header("Card References")]
    public List<Button> cardButtons = new List<Button>();
    public List<TextMeshProUGUI> cardTextButtons = new List<TextMeshProUGUI>();
    public List<Image> cardVisuals = new List<Image>();
    
    [Header("Reroll Settings")]
    public Button rerollButton;
    public int rerollCost = 1;
    
    [Header("Shop Settings")]
    public int costScalingPerRound = 3;
    [Min(0.01f)]
    [SerializeField] private float shopCardVisualScale = 0.5f;
    public ShopCostSO shopCost;
    
    public Button expandGridButton;

    [Header("Grid Expansion TV Spawn")]
    public Transform tvSpawnPoint;
    public GameObject ticketCostTvPrefab;
    private GameObject _spawnedTicketCostTv;
    
    [Header("TV Game Objects")] 
    [SerializeField] private GameObject throwBallTv;
    [SerializeField] private GameObject goBackToShopTv;
    
    public int baseGridCost;
    public float gridCostScaling = 2;
    private int totalGridUpgrades = 0;

    private RoundStateManager _roundStateManager;
    private List<CardDataSO> _currentShopCards = new List<CardDataSO>();
    private List<GameObject> _spawnedShopCardVisuals = new List<GameObject>();
    
    private List<int> _currentCardCosts = new List<int>();
    
    [Header("Rarity Shop Settings. Its a percentage chance")]
    public float commonWeight = 40f;
    public float rareWeight = 35;
    public float legendaryWeight = 25f;
    
    public float commonCostMultiplier;
    public float rareCostMultiplier;
    public float legendaryCostMultiplier;
    
    [SerializeField] private HexGridExpander hexGridExpander;
    
    [Header("Remove Pin Settings")]
    public Button removePinButton;
    public string singlePinCardID = "SingleNormalPin";
    public int removePinCost = 1;

    private void Start()
    {
        AddBounceEffectToButton(rerollButton);
        AddBounceEffectToButton(continueButton);
        AddBounceEffectToButton(expandGridButton);
        AddBounceEffectToButton(removePinButton);
    }
    
    public void ToggleShop(bool show, RoundStateManager manager = null)
    {
        shopGameObject.SetActive(show);
        
        if (show)
        {
            if (manager != null)
            {
                _roundStateManager = manager;
            }
            
            GenerateShopCards();
            UpdateRemovePinUI();
            UpdateRerollUI();
            
            UpdateGridButtonText();
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
        _currentShopCards.Clear();
        _currentCardCosts.Clear();

        while (_spawnedShopCardVisuals.Count < cardButtons.Count)
        {
            _spawnedShopCardVisuals.Add(null);
        }

        for (int i = 0; i < cardButtons.Count; i++)
        {
            CardDataSO selectedCard = GetRandomCardByRarity();
            _currentShopCards.Add(selectedCard);

            UpdateButtonText(i);
            UpdateCardVisual(i, cardVisuals[i], _currentShopCards[i]);
            
            // turn of the parent image, as we see jumbo if we dont (a card background image)
            cardVisuals[i].enabled = false;
            // turn of alpha of the card we just want the hitbox
            Color tempColor = cardVisuals[i].color;
            tempColor.a = 0f;
            cardVisuals[i].color = tempColor;
            
            SetCardAlpha(i, cardButtons[i], cardVisuals[i], 1f);

            // Hook up our CardAnimator logic instead of WiggleRoutine
            GameObject spawnedCard = _spawnedShopCardVisuals[i];
            CardAnimator animator = spawnedCard != null ? spawnedCard.GetComponent<CardAnimator>() : null;
            AddCardHoverEvents(cardButtons[i].gameObject, animator);
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
        _spawnedShopCardVisuals[index] = card;

        RectTransform cardRect = card.GetComponent<RectTransform>();
        if (cardRect != null)
        {
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
        }

        card.transform.localRotation = Quaternion.identity;

        CardAnimator cardAnimator = card.GetComponent<CardAnimator>();
        if (cardAnimator != null)
        {
            // Initialize Shop Mode - Pass the target button transform and base scale
            cardAnimator.InitializeShopMode(cardImage.transform, cardData, shopCardVisualScale);
        }
        else
        {
            // Fallback scaling just in case the prefab is missing the animator
            card.transform.localScale = Vector3.one * shopCardVisualScale;
            CardVisualPresenter presenter = card.GetComponentInChildren<CardVisualPresenter>(true);
            if (presenter != null)
            {
                presenter.Bind(cardData);
            }
        }

        CanvasGroup cardGroup = card.GetComponent<CanvasGroup>();
        if (cardGroup != null)
        {
            cardGroup.enabled = true;
            cardGroup.alpha = 1f;
            cardGroup.interactable = false;
            cardGroup.blocksRaycasts = false;
        }

        cardImage.enabled = true;
    }
    
    private CardDataSO GetRandomCardByRarity()
    {
        List<CardDataSO> pool = _roundStateManager.shopCardsToPickFrom;
        if (pool == null || pool.Count == 0) return null;

        bool hasCommon = pool.Any(c => c.rarity == CardRarity.Common);
        bool hasRare = pool.Any(c => c.rarity == CardRarity.Rare);
        bool hasLegendary = pool.Any(c => c.rarity == CardRarity.Legendary);

        float currentCommonWeight = hasCommon ? commonWeight : 0f;
        float currentRareWeight = hasRare ? rareWeight : 0f;
        float currentLegendWeight = hasLegendary ? legendaryWeight : 0f;

        float totalWeight = currentCommonWeight + currentRareWeight + currentLegendWeight;

        if (totalWeight <= 0f) 
        {
            return pool[Random.Range(0, pool.Count)];
        }

        float randomVal = Random.Range(0f, totalWeight);
        CardRarity selectedRarity;

        if (randomVal <= currentCommonWeight)
        {
            selectedRarity = CardRarity.Common;
        }
        else if (randomVal <= currentCommonWeight + currentRareWeight)
        {
            selectedRarity = CardRarity.Rare;
        }
        else
        {
            selectedRarity = CardRarity.Legendary;
        }

        List<CardDataSO> rarityPool = pool.Where(c => c.rarity == selectedRarity).ToList();
        return rarityPool[Random.Range(0, rarityPool.Count)];
    }
    
    public void ToggleHexGridExpander(bool turnOn)
    {
        if (turnOn)
        {
            _roundStateManager.postProcessingEffects.SetActive(true);
            
            _roundStateManager.MoveCamera(CameraLocation.PinCameraLocation);
            
            if (ticketCostTvPrefab != null && tvSpawnPoint != null)
            {
                _spawnedTicketCostTv = Instantiate(ticketCostTvPrefab, tvSpawnPoint.position, tvSpawnPoint.rotation);

                _spawnedTicketCostTv.transform.rotation = Quaternion.Euler(0, 90, 90);
                
                // Changed this part to use the new helper method
                UpdateGridTVText();
            }
        }
        else
        {
            _roundStateManager.MoveCamera(CameraLocation.ShopCameraLocation);
            
            _roundStateManager.postProcessingEffects.SetActive(false);
            
            if (_spawnedTicketCostTv != null)
            {
                Destroy(_spawnedTicketCostTv);
            }

            UpdateGridButtonText();
        }
        
        _roundStateManager.PinPlacementVisuals(turnOn);

        throwBallTv.SetActive(!turnOn);
        goBackToShopTv.SetActive(turnOn);
        hexGridExpander.enabled = turnOn;
    }
    
    private void UpdateGridButtonText()
    {
        if (expandGridButton != null)
        {
            TextMeshProUGUI btnText = expandGridButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = $"Increase Grid Size: {GetGridUpgradeCost()}";
            }
        }
    }
    
    private void ClearCardVisual(int index, Image cardImage)
    {
        GameObject trackedVisual = _spawnedShopCardVisuals[index];
        if (trackedVisual != null)
        {
            trackedVisual.SetActive(false);
            Destroy(trackedVisual);
            _spawnedShopCardVisuals[index] = null;
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
    
    public void UpdateGridTVText()
    {
        if (_spawnedTicketCostTv != null)
        {
            TextMeshProUGUI textComponent = _spawnedTicketCostTv.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = $"Cost: {GetGridUpgradeCost()}\n\n<color=green>Your Tickets: {SessionData.Instance.tickets}</color>";
            }
        }
    }
    
    public bool TryBuyGridExpansion()
    {
        int cost = GetGridUpgradeCost();
        
        if (SessionData.Instance.SpendTickets(cost))
        {
            totalGridUpgrades++;
            UpdateGridTVText();
            return true;
        }
        
        return false;
    }
    
    // Replace your existing UpdateButtonText method
    private void UpdateButtonText(int index)
    {
        TextMeshProUGUI buttonText = cardTextButtons[index];

        int currentRound = _roundStateManager.sessionData.currentRound;
        int costModifier = 1;

        costModifier = GetShopCostModifier(currentRound, costModifier);

        CardDataSO card = _currentShopCards[index];
        float rarityMultiplier = 1f;
        
        switch (card.rarity)
        {
            case CardRarity.Common: rarityMultiplier = commonCostMultiplier; break;
            case CardRarity.Rare: rarityMultiplier = rareCostMultiplier; break;
            case CardRarity.Legendary: rarityMultiplier = legendaryCostMultiplier; break;
        }

        // we first see shop on round 1 ?
        int calculatedCost = Mathf.RoundToInt(card.baseCost + costModifier + rarityMultiplier);
        
        if (_currentCardCosts.Count <= index)
        {
            _currentCardCosts.Add(calculatedCost);
        }
        else
        {
            _currentCardCosts[index] = calculatedCost;
        }
        
        buttonText.text = $"Buy (${calculatedCost})";
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
        _roundStateManager.SwitchState(_roundStateManager.roundStartState);
    }

    public void BuyCard(int index)
    {
        if (index < _currentShopCards.Count && _currentShopCards[index] != null)
        {
            int costToSpend = _currentCardCosts[index];
            
            if (SessionData.Instance.SpendTickets(costToSpend))
            {
                DeckManager.Instance.AddCardToDeck(_currentShopCards[index]);
                
                // Remove from the array so it can't be bought again
                _currentShopCards[index] = null; 

                // turn of the parent image, as we see jumbo if we dont (a card background image)
                cardVisuals[index].enabled = false;
                // turn of alpha of the card we just want the hitbox
                Color tempColor = cardVisuals[index].color;
                tempColor.a = 0f;
                cardVisuals[index].color = tempColor;
                
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

        GameObject spawnedVisual = _spawnedShopCardVisuals[index];
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
// Ai bounce effect stuff dw about it.
    private void AddCardHoverEvents(GameObject buttonGo, CardAnimator animator)
    {
        EventTrigger trigger = buttonGo.GetComponent<EventTrigger>();
        if (trigger == null) 
        {
            trigger = buttonGo.AddComponent<EventTrigger>();
        }
        else
        {
            trigger.triggers.Clear();
            // Reset scale in case the shop was rerolled while a button was actively being hovered
            buttonGo.transform.DOKill();
            buttonGo.transform.localScale = Vector3.one;
        }

        Vector3 originalScale = buttonGo.transform.localScale;

        EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((data) => 
        { 
            Button btn = buttonGo.GetComponent<Button>();
            if (btn != null && !btn.interactable) return;

            if (animator != null)
            {
                animator.shopIsHovering = true;
                animator.PlayPointerEnter();
            }

            buttonGo.transform.DOKill();
            buttonGo.transform.DOScale(originalScale * 1.05f, 0.15f).SetEase(Ease.OutBack);
        });
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((data) => 
        { 
            if (animator != null)
            {
                animator.shopIsHovering = false;
                animator.PlayPointerExit();
            }

            buttonGo.transform.DOKill();
            buttonGo.transform.DOScale(originalScale, 0.15f).SetEase(Ease.OutQuad);
        });
        trigger.triggers.Add(exitEntry);
        
        EventTrigger.Entry downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        downEntry.callback.AddListener((data) => 
        { 
            Button btn = buttonGo.GetComponent<Button>();
            if (btn != null && !btn.interactable) return;

            if (animator != null)
            {
                animator.PlayPointerDown();
            }

            buttonGo.transform.DOKill();
            buttonGo.transform.DOScale(originalScale * 0.9f, 0.1f).SetEase(Ease.OutQuad);
        });
        trigger.triggers.Add(downEntry);

        EventTrigger.Entry upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        upEntry.callback.AddListener((data) => 
        { 
            if (animator != null)
            {
                animator.PlayPointerUp(false);
            }

            Button btn = buttonGo.GetComponent<Button>();
            if (btn != null)
            {
                buttonGo.transform.DOKill();
                
                if (btn.interactable)
                {
                    bool isHovering = RectTransformUtility.RectangleContainsScreenPoint(
                        buttonGo.GetComponent<RectTransform>(), 
                        ((PointerEventData)data).position, 
                        ((PointerEventData)data).pressEventCamera
                    );
                    
                    DG.Tweening.Sequence clickSeq = DOTween.Sequence();
                    clickSeq.Append(buttonGo.transform.DOScale(originalScale * 1.15f, 0.1f).SetEase(Ease.OutQuad));
                    
                    float targetScale = isHovering ? 1.05f : 1f;
                    clickSeq.Append(buttonGo.transform.DOScale(originalScale * targetScale, 0.15f).SetEase(Ease.OutBack));
                }
                else
                {
                    buttonGo.transform.DOScale(originalScale, 0.15f).SetEase(Ease.OutQuad);
                }
            }
        });
        trigger.triggers.Add(upEntry);
    }
    // ai stuff dw about it
    private void AddBounceEffectToButton(Button btn)
    {
        if (btn == null) return;

        EventTrigger trigger = btn.GetComponent<EventTrigger>();
        if (trigger == null) 
        {
            trigger = btn.gameObject.AddComponent<EventTrigger>();
        }

        Vector3 originalScale = btn.transform.localScale;

        EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((data) => 
        { 
            if (btn.interactable)
            {
                btn.transform.DOKill();
                btn.transform.DOScale(originalScale * 1.05f, 0.15f).SetEase(Ease.OutBack);
            }
        });
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((data) => 
        { 
            btn.transform.DOKill();
            btn.transform.DOScale(originalScale, 0.15f).SetEase(Ease.OutQuad);
        });
        trigger.triggers.Add(exitEntry);
        
        EventTrigger.Entry downEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        downEntry.callback.AddListener((data) => 
        { 
            if (btn.interactable)
            {
                btn.transform.DOKill();
                btn.transform.DOScale(originalScale * 0.9f, 0.1f).SetEase(Ease.OutQuad);
            }
        });
        trigger.triggers.Add(downEntry);

        EventTrigger.Entry upEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        upEntry.callback.AddListener((data) => 
        { 
            btn.transform.DOKill();
            
            if (btn.interactable)
            {
                bool isHovering = RectTransformUtility.RectangleContainsScreenPoint(
                    btn.GetComponent<RectTransform>(), 
                    ((PointerEventData)data).position, 
                    ((PointerEventData)data).pressEventCamera
                );
                
                DG.Tweening.Sequence clickSeq = DOTween.Sequence();
                clickSeq.Append(btn.transform.DOScale(originalScale * 1.15f, 0.1f).SetEase(Ease.OutQuad));
                
                float targetScale = isHovering ? 1.05f : 1f;
                clickSeq.Append(btn.transform.DOScale(originalScale * targetScale, 0.15f).SetEase(Ease.OutBack));
            }
            else
            {
                btn.transform.DOScale(originalScale, 0.15f).SetEase(Ease.OutQuad);
            }
        });
        trigger.triggers.Add(upEntry);
    }
}