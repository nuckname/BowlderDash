using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using System.Linq;

// https://www.youtube.com/watch?v=I1dAZuWurw4
// https://github.com/mixandjam/balatro-feel

public class CardHandController : MonoBehaviour
{
    public static CardHandController Instance { get; private set; }

    [SerializeField] private CardMovement selectedCardMovement;
    [SerializeReference] private CardMovement hoveredCardMovement;

    [SerializeField] private GameObject slotPrefab;
    private RectTransform rect;

    [Header("Spawn Settings")]
    public List<CardMovement> cardsInHand;

    bool isCrossing = false;
    [SerializeField] private bool tweenCardReturn = true;

    [Header("Deck Settings")]
    [SerializeField] private List<CardDataSO> startingDeck = new List<CardDataSO>();
    private List<CardDataSO> _currentDeck = new List<CardDataSO>();
    
	[Header("Discard Settings")]
    [SerializeField] private RectTransform discardPileTransform;
    [SerializeField] private float discardAnimationDuration = 0.5f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        rect = GetComponent<RectTransform>();
        
        _currentDeck = new List<CardDataSO>(startingDeck);
    }

    /// <summary>
    /// Draws a specified number of cards from the current deck and adds them to the player's hand.
    /// Stops drawing if the deck becomes empty.
    /// </summary>
    public void Draw(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            if (_currentDeck.Count == 0)
            {
                Debug.LogWarning("Deck is empty!");
                break;
            }

            CardDataSO drawnCard = _currentDeck[0];
            _currentDeck.RemoveAt(0);

            AddCardToHand(drawnCard);
        }
    }

    /// <summary>
    /// Discards all currently held cards in the hand, triggering their discard animations.
    /// </summary>
	public void DiscardHand()
    {
        for (int i = cardsInHand.Count - 1; i >= 0; i--)
        {
            DiscardCard(cardsInHand[i]);
        }
    }
    
    /// <summary>
    /// Adds a specific card data directly into the active draw deck.
    /// </summary>
    public void AddCardToDeck(CardDataSO newCard)
    {
        _currentDeck.Add(newCard);
    }

	// https://share.gemini.google/usukNofWRGFR
    /// <summary>
    /// Removes a specific card from the hand, unparents its layout slot, and animates it 
    /// moving to the discard pile before destroying it.
    /// </summary>
	public void DiscardCard(CardMovement card)
    {
        if (card == null) return;

        // Instantly unparent the slot before destroying it so childCount updates instantly
        Transform slot = card.transform.parent;
        slot.SetParent(null);
        Destroy(slot.gameObject);
        
        cardsInHand.Remove(card);
        
        // Unparent the card and put it on the root Canvas so it can fly freely over other UI elements
        card.transform.SetParent(transform.parent);

        // Rebuild the hand to reflect the deleted card
        RebuildHandVisuals();

        // Disable the script so it can't be interacted with while animating
        card.enabled = false;

        if (discardPileTransform != null)
        {
            Sequence discardSeq = DOTween.Sequence();
            
            discardSeq.Append(card.transform.DOMove(discardPileTransform.position, discardAnimationDuration).SetEase(Ease.InBack));
            discardSeq.Join(card.transform.DOScale(Vector3.zero, discardAnimationDuration).SetEase(Ease.InBack));
            discardSeq.Join(card.transform.DORotate(new Vector3(0, 0, 180f), discardAnimationDuration, RotateMode.FastBeyond360));
            
            discardSeq.OnComplete(() => Destroy(card.gameObject));
        }
        else
        {
            Destroy(card.gameObject);
        }
    }

    /// <summary>
    /// Instantiates a new card layout slot and UI prefab based on the provided CardDataSO, 
    /// injecting its data and optionally animating its entry from a specific screen position.
    /// </summary>
    public void AddCardToHand(CardDataSO cardDataToSpawn, Vector3? startScreenPosition = null)
    {
        if (cardDataToSpawn == null || cardDataToSpawn.cardUIPrefab == null) return;

        // Instantiate a new slot so the layout group handles it properly
        GameObject newSlot = Instantiate(slotPrefab, transform);
        
        // Instantiate the actual UI card inside it USING THE DATA
        GameObject newCardObj = Instantiate(cardDataToSpawn.cardUIPrefab, newSlot.transform);
        CardMovement newCardMovement = newCardObj.GetComponent<CardMovement>();

        if (newCardMovement != null)
        {
            // INJECT THE DATA HERE
            newCardMovement.cardData = cardDataToSpawn;
            
            RegisterCardEvents(newCardMovement, transform.childCount - 1);
        }

        RebuildHandVisuals();

        // Fling the card from the mouse position into its slot!
        if (startScreenPosition.HasValue)
        {
            newCardObj.transform.position = startScreenPosition.Value;
            newCardObj.transform.DOLocalMove(Vector3.zero, tweenCardReturn ? 0.3f : 0f).SetEase(Ease.OutBack);
        }
    }

    private void RegisterCardEvents(CardMovement card, int index)
    {
        card.PointerEnterEvent.AddListener(CardPointerEnter);
        card.PointerExitEvent.AddListener(CardPointerExit);
        card.BeginDragEvent.AddListener(BeginDrag);
        card.EndDragEvent.AddListener(EndDrag);
        card.name = index.ToString();
    }

    private void BeginDrag(CardMovement cardMovement)
    {
        selectedCardMovement = cardMovement;
    }

    void EndDrag(CardMovement cardMovement)
    {
        if (selectedCardMovement == null)
            return;

        selectedCardMovement.transform.DOLocalMove(selectedCardMovement.selected ? new Vector3(0,selectedCardMovement.selectionOffset,0) : Vector3.zero, tweenCardReturn ? .15f : 0).SetEase(Ease.OutBack);

        // Forces layout rebuild trigger
        rect.sizeDelta += Vector2.right;
        rect.sizeDelta -= Vector2.right;

        selectedCardMovement = null;
    }

    void CardPointerEnter(CardMovement cardMovement)
    {
        hoveredCardMovement = cardMovement;
    }

    void CardPointerExit(CardMovement cardMovement)
    {
        hoveredCardMovement = null;
    }

    void Update()
    {
        // E to end turn, temp move to FSM.
        if (Input.GetKey(KeyCode.E))
        {
            DiscardHand();
        }
        
        HandleCardSwapping();
        HandleHotkeyInput();
    }
    
    private void HandleHotkeyInput()
    {
        KeyCode[] numberKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 };

        for (int i = 0; i < numberKeys.Length; i++)
        {
            if (Input.GetKeyDown(numberKeys[i]))
            {
                if (i < cardsInHand.Count)
                {
                    if (selectedCardMovement != null && selectedCardMovement != cardsInHand[i])
                    {
                        selectedCardMovement.OnEndDrag(null);
                    }

                    cardsInHand[i].ForceStartDrag();
                }
            }
        }
    }

    private void HandleCardSwapping()
    {
        if (selectedCardMovement == null || isCrossing)
            return;

        for (int i = 0; i < cardsInHand.Count; i++)
        {
            if (selectedCardMovement.transform.position.x > cardsInHand[i].transform.position.x)
            {
                if (selectedCardMovement.ParentIndex() < cardsInHand[i].ParentIndex())
                {
                    Swap(i);
                    break;
                }
            }

            if (selectedCardMovement.transform.position.x < cardsInHand[i].transform.position.x)
            {
                if (selectedCardMovement.ParentIndex() > cardsInHand[i].ParentIndex())
                {
                    Swap(i);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Safely wraps an existing floating UI card into a new layout group slot 
    /// when it is dropped back into the hand area.
    /// </summary>
    public void AssignSlotToCard(CardMovement card)
    {
        GameObject newSlot = Instantiate(slotPrefab, transform);
        card.transform.SetParent(newSlot.transform);
        
        RebuildHandVisuals();
    }

    void Swap(int index)
    {
        isCrossing = true;

        Transform focusedParent = selectedCardMovement.transform.parent;
        Transform crossedParent = cardsInHand[index].transform.parent;

        cardsInHand[index].transform.SetParent(focusedParent);
        cardsInHand[index].transform.localPosition = cardsInHand[index].selected ? new Vector3(0, cardsInHand[index].selectionOffset, 0) : Vector3.zero;
        selectedCardMovement.transform.SetParent(crossedParent);

        isCrossing = false;

        if (cardsInHand[index].cardAnimator == null)
            return;

        bool swapIsRight = cardsInHand[index].ParentIndex() > selectedCardMovement.ParentIndex();
        cardsInHand[index].cardAnimator.Swap(swapIsRight ? -1 : 1);

        //Updated Visual Indexes
        RebuildHandVisuals();
    }

    /// <summary>
    /// Evaluates the current physical slots in the hand and updates all visual card components 
    /// with their correct sibling indexes and the new total hand length for positioning math.
    /// </summary>
    public void RebuildHandVisuals()
    {
        // Wipe the list completely clean
        cardsInHand.Clear();

        // Count the physical slots remaining in the hand
        int currentLength = transform.childCount;

        for (int i = 0; i < currentLength; i++)
        {
            Transform slot = transform.GetChild(i);
            CardMovement cardSlot = slot.GetComponentInChildren<CardMovement>();

            // Re-add only the valid cards that are actually in the slots right now
            if (cardSlot != null)
            {
                cardsInHand.Add(cardSlot);

                if (cardSlot.cardAnimator != null)
                {
                    // Update the visual's sibling index to match the slot's order
                    cardSlot.cardAnimator.transform.SetSiblingIndex(i);

                    // Pass the new length to the animator for curve/spacing math
                    cardSlot.cardAnimator.UpdateLength(currentLength);
                }
            }
        }
    }
}