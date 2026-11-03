using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using System.Linq;

public class HandInteractionManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HandManager handManager;

    public CardMovement selectedCardMovement;
    [SerializeReference] private CardMovement hoveredCardMovement;

    private RectTransform rect;
    private bool isCrossing = false;

    private void Awake()
    {
        // Should be attached to the layout group GameObject to force rebuild layout correctly
        rect = GetComponent<RectTransform>();
    }

    public void RegisterCardEvents(CardMovement card, int index)
    {
        card.PointerEnterEvent.AddListener(CardPointerEnter);
        card.PointerExitEvent.AddListener(CardPointerExit);
        card.BeginDragEvent.AddListener(BeginDrag);
        card.EndDragEvent.AddListener(EndDrag);
        card.name = index.ToString();
    }

    private void BeginDrag(CardMovement cardMovement)
    {
        if (GameplayPause.IsInputBlocked(this)) return;
        selectedCardMovement = cardMovement;
    }

    private void EndDrag(CardMovement cardMovement)
    {
        if (selectedCardMovement == null)
            return;

        selectedCardMovement.transform.DOLocalMove(
            selectedCardMovement.selected ? new Vector3(0, selectedCardMovement.selectionOffset, 0) : Vector3.zero, 
            handManager.TweenCardReturn ? .15f : 0).SetEase(Ease.OutBack);

        // Forces layout rebuild trigger
        if (rect != null)
        {
            rect.sizeDelta += Vector2.right;
            rect.sizeDelta -= Vector2.right;
        }

        selectedCardMovement = null;
    }

    private void CardPointerEnter(CardMovement cardMovement)
    {
        if (GameplayPause.IsInputBlocked(this)) return;
        hoveredCardMovement = cardMovement;
    }

    private void CardPointerExit(CardMovement cardMovement)
    {
        hoveredCardMovement = null;
    }

    private void Update()
    {
        if (GameplayPause.IsInputBlocked(this)) return;
        /*
        if (Input.GetKeyDown(KeyCode.J))
        {
            DeckManager.Instance.Draw(5);
        }
        */
        
        HandleCardSwapping();
        
        HandleHotkeyInput();
        //UpdateHotkeyVisuals();
    }
    
    private void HandleHotkeyInput()
    {
        if (DraggableSnap.ActiveDrag != null) return;
        KeyCode[] numberKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 };

        for (int i = 0; i < numberKeys.Length; i++)
        {
            if (Input.GetKeyDown(numberKeys[i]))
            {
                if (i < handManager.cardsInHand.Count)
                {
                    CardMovement targetCard = handManager.cardsInHand[i];
                    if (selectedCardMovement != null && selectedCardMovement != targetCard)
                    {
                        selectedCardMovement.CancelDrag();
                    }

                    targetCard.GetComponent<CardUIAudio>()?.PlayKeyboardActivation();
                    targetCard.ForceStartDrag();
                }
            }
        }
    }

    private void HandleCardSwapping()
    {
        if (selectedCardMovement == null || isCrossing)
            return;

        for (int i = 0; i < handManager.cardsInHand.Count; i++)
        {
            if (selectedCardMovement.transform.position.x > handManager.cardsInHand[i].transform.position.x)
            {
                if (selectedCardMovement.ParentIndex() < handManager.cardsInHand[i].ParentIndex())
                {
                    Swap(i);
                    break;
                }
            }

            if (selectedCardMovement.transform.position.x < handManager.cardsInHand[i].transform.position.x)
            {
                if (selectedCardMovement.ParentIndex() > handManager.cardsInHand[i].ParentIndex())
                {
                    Swap(i);
                    break;
                }
            }
        }
    }

    private void UpdateHotkeyVisuals()
    {
        for (int i = 0; i < handManager.cardsInHand.Count; i++)
        {
            if (handManager.cardsInHand[i] != null && handManager.cardsInHand[i].hotkeyText != null)
            {
                // We only have 5 hotkeys mapped in HandleHotkeyInput()
                // Hide the text if we cross the Y threshold and start previewing in the world
                if (i < 5 && !handManager.cardsInHand[i].isPreviewingInWorld) 
                {
                    if (!handManager.cardsInHand[i].hotkeyText.gameObject.activeSelf)
                        handManager.cardsInHand[i].hotkeyText.gameObject.SetActive(true);

                    string expectedText = (i + 1).ToString();
                    
                    // Only update the text if it's different to prevent UI allocation every frame
                    if (handManager.cardsInHand[i].hotkeyText.text != expectedText)
                    {
                        handManager.cardsInHand[i].hotkeyText.text = expectedText;
                    }
                }
                else
                {
                    // Hide the text if the card is beyond the 5th slot or is currently in the play area
                    if (handManager.cardsInHand[i].hotkeyText.gameObject.activeSelf)
                    {
                        handManager.cardsInHand[i].hotkeyText.gameObject.SetActive(false);
                    }
                }
            }
        }
    }
    
    private void Swap(int index)
    {
        isCrossing = true;

        Transform focusedParent = selectedCardMovement.transform.parent;
        Transform crossedParent = handManager.cardsInHand[index].transform.parent;

        handManager.cardsInHand[index].transform.SetParent(focusedParent);
        handManager.cardsInHand[index].transform.localPosition = handManager.cardsInHand[index].selected ? new Vector3(0, handManager.cardsInHand[index].selectionOffset, 0) : Vector3.zero;
        selectedCardMovement.transform.SetParent(crossedParent);

        isCrossing = false;

        if (handManager.cardsInHand[index].cardAnimator == null)
            return;

        bool swapIsRight = handManager.cardsInHand[index].ParentIndex() > selectedCardMovement.ParentIndex();
        handManager.cardsInHand[index].cardAnimator.Swap(swapIsRight ? -1 : 1);

        //Updated Visual Indexes
        handManager.RebuildHandVisuals();
    }
}
