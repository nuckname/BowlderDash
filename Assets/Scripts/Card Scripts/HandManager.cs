using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using System.Linq;
using UnityEngine.UI;

// https://www.youtube.com/watch?v=I1dAZuWurw4
// https://github.com/mixandjam/balatro-feel

public class HandManager : MonoBehaviour
{
    public static HandManager Instance { get; private set; }

    private Tween _paddingTween;
    [SerializeField] private float layoutTweenDuration = 0.3f;
    
    [Header("References")]
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private HandInteractionManager interactionManager;

    [Header("Spawn Settings")]
    public List<CardMovement> cardsInHand;
    [SerializeField] private RectTransform spawnPointForDrawingCards;
    [SerializeField] private bool tweenCardReturn = true;
    public bool TweenCardReturn => tweenCardReturn;

    private HorizontalLayoutGroup _layoutGroup;
    
    [Header("Discard Settings")]
    [SerializeField] private RectTransform discardPileTransform;
    public float discardAnimationDuration = 0.5f;
    // The delay to switch to the next state
    public float discardAnimationDurationBuffer = 0.75f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
        _layoutGroup = GetComponent<HorizontalLayoutGroup>();
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

    public void AddDrawnCardToHand(CardDataSO drawnCard)
    {
        // For spawning cards that go to our hand. (Animation)
        Vector3 startPosition = spawnPointForDrawingCards.position;
        AddCardToHand(drawnCard, startPosition);
    }

    // https://share.gemini.google/usukNofWRGFR
    /// <summary>
    /// Removes a specific card from the hand, unparents its layout slot, and animates it 
    /// moving to the discard pile before destroying it.
    /// </summary>
    public void DiscardCard(CardMovement card)
    {
        if (card == null) return;

        if (card.cardData != null)
        {
            DeckManager.Instance.AddToDiscardPile(card.cardData);
        }

        // Instantly unparent the slot before destroying it so childCount updates instantly
        RemoveCardUi(card);
        
        // Unparent the card and put it on the root Canvas so it can fly freely over other UI elements
        card.transform.SetParent(transform.parent);

        // Rebuild the hand to reflect the deleted card
        RebuildHandVisuals();

        // Disable the script so it can't be interacted with while animating
        card.enabled = false;

        DiscardAnimation(card);
    }

    /// <summary>
    /// Removes a card UI after it becomes a placed world object. The world object owns the
    /// card data until it is recalled or cleared at the round boundary.
    /// </summary>
    public void ConsumePlayedCard(CardMovement card)
    {
        if (card == null) return;

        //DeckManager.Instance.AddToDiscardPile(card.cardData);

        RemoveCardUi(card);
    }

    private void RemoveCardUi(CardMovement card)
    {
        Transform slot = card.transform.parent;
        if (slot != null)
        {
            slot.SetParent(null);
            Destroy(slot.gameObject);
        }

        cardsInHand.Remove(card);
        RebuildHandVisuals();
        Destroy(card.gameObject);
    }

    // AI  
    // https://share.gemini.google/usukNofWRGFR
    private void DiscardAnimation(CardMovement card)
    {
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
            
            interactionManager.RegisterCardEvents(newCardMovement, transform.childCount - 1);
        }

        RebuildHandVisuals();

        // Fling the card from the mouse position into its slot!
        if (startScreenPosition.HasValue)
        {
            newCardObj.transform.position = startScreenPosition.Value;
            newCardObj.transform.DOLocalMove(Vector3.zero, tweenCardReturn ? 0.3f : 0f).SetEase(Ease.OutBack);
        }
    }

    /// <summary>
    /// Safely wraps an existing floating UI card into a new layout group slot 
    /// when it is dropped back into the hand area.
    /// </summary>
    public void AssignSlotToCard(CardMovement card)
    {
        DeckManager.Instance.RemoveFromDiscardPile(card.cardData);
        
        GameObject newSlot = Instantiate(slotPrefab, transform);
        card.transform.SetParent(newSlot.transform);
        
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

        UpdateLayoutPadding(currentLength);
    }

    private void UpdateLayoutPadding(int cardCount)
    {
        if (_layoutGroup == null) return;

        int targetPadding = -150;

        switch (cardCount)
        {
            // 1 Card in hand idk why it say 3.
            case 3:
                targetPadding = -900;
                break;
            // 2 Card
            case 4:
                targetPadding = -450;
                break;
            // 3 Card
            case 5:
                targetPadding = -350;
                break;
            // 4 Card
            case 6:
                targetPadding = -250;
                break;
            // 5 Card
            case 7:
                targetPadding = -355;
                break;
            default:
                targetPadding = -355;
                break;
        }

        // Kill any currently running padding animation so they don't fight
        _paddingTween?.Kill();

        // Smoothly tween the padding value so the layout shifts naturally with the cards
        _paddingTween = DOTween.To(
            getter: () => _layoutGroup.padding.left,
            setter: x => 
            {
                _layoutGroup.padding = new RectOffset(x, _layoutGroup.padding.right, _layoutGroup.padding.top, _layoutGroup.padding.bottom);
                LayoutRebuilder.MarkLayoutForRebuild(transform as RectTransform);
            },
            endValue: targetPadding,
            duration: layoutTweenDuration
        ).SetEase(Ease.OutCubic);
    }
}
