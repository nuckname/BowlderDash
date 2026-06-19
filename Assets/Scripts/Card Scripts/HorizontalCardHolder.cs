using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using DG.Tweening;
using System.Linq;

public class HorizontalCardHolder : MonoBehaviour
{
    public static HorizontalCardHolder Instance { get; private set; }

    [SerializeField] private CardMovement selectedCardMovement;
    [SerializeReference] private CardMovement hoveredCardMovement;

    [SerializeField] private GameObject slotPrefab;
    private RectTransform rect;

    [Header("Spawn Settings")]
    [SerializeField] private int cardsToSpawn = 7;
    public List<CardMovement> cards;

    bool isCrossing = false;
    [SerializeField] private bool tweenCardReturn = true;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        for (int i = 0; i < cardsToSpawn; i++)
        {
            Instantiate(slotPrefab, transform);
        }

        rect = GetComponent<RectTransform>();
        cards = GetComponentsInChildren<CardMovement>().ToList();

        int cardCount = 0;

        foreach (CardMovement card in cards)
        {
            card.PointerEnterEvent.AddListener(CardPointerEnter);
            card.PointerExitEvent.AddListener(CardPointerExit);
            card.BeginDragEvent.AddListener(BeginDrag);
            card.EndDragEvent.AddListener(EndDrag);
            card.name = cardCount.ToString();
            cardCount++;
        }

        StartCoroutine(Frame());

        IEnumerator Frame()
        {
            yield return new WaitForSecondsRealtime(.1f);
            
            RebuildHandVisuals();
        }
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
        if (Input.GetKeyDown(KeyCode.Delete))
        {
            if (hoveredCardMovement != null)
            {
                // Instantly unparent the slot before destroying it so childCount updates instantly
                hoveredCardMovement.transform.parent.SetParent(null);
                Destroy(hoveredCardMovement.transform.parent.gameObject);
                
                cards.Remove(hoveredCardMovement);
                
                // Rebuild the hand to reflect the deleted card
                RebuildHandVisuals();
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            foreach (CardMovement card in cards)
            {
                card.Deselect();
            }
        }

        if (selectedCardMovement == null)
            return;

        if (isCrossing)
            return;

        for (int i = 0; i < cards.Count; i++)
        {
            if (selectedCardMovement.transform.position.x > cards[i].transform.position.x)
            {
                if (selectedCardMovement.ParentIndex() < cards[i].ParentIndex())
                {
                    Swap(i);
                    break;
                }
            }

            if (selectedCardMovement.transform.position.x < cards[i].transform.position.x)
            {
                if (selectedCardMovement.ParentIndex() > cards[i].ParentIndex())
                {
                    Swap(i);
                    break;
                }
            }
        }
    }

    void Swap(int index)
    {
        isCrossing = true;

        Transform focusedParent = selectedCardMovement.transform.parent;
        Transform crossedParent = cards[index].transform.parent;

        cards[index].transform.SetParent(focusedParent);
        cards[index].transform.localPosition = cards[index].selected ? new Vector3(0, cards[index].selectionOffset, 0) : Vector3.zero;
        selectedCardMovement.transform.SetParent(crossedParent);

        isCrossing = false;

        if (cards[index].cardAnimator == null)
            return;

        bool swapIsRight = cards[index].ParentIndex() > selectedCardMovement.ParentIndex();
        cards[index].cardAnimator.Swap(swapIsRight ? -1 : 1);

        //Updated Visual Indexes
        RebuildHandVisuals();
    }

    /// <summary>
    /// Evaluates the current slots in the hand and updates all visual cards 
    /// with their correct sibling index and the new total hand length.
    /// </summary>
    public void RebuildHandVisuals()
    {
        // Clean out any null references from the list (cards that were destroyed)
        cards.RemoveAll(c => c == null);

        int currentLength = transform.childCount;

        for (int i = 0; i < currentLength; i++)
        {
            Transform slot = transform.GetChild(i);
            CardMovement cardSlot = slot.GetComponentInChildren<CardMovement>();

            if (cardSlot != null && cardSlot.cardAnimator != null)
            {
                // Update the visual's sibling index to match the slot's order
                cardSlot.cardAnimator.transform.SetSiblingIndex(i);

                // Pass the new length to the animator for curve/spacing math
                cardSlot.cardAnimator.UpdateLength(currentLength);
            }
        }
    }
}