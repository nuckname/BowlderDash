using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine.UI;

// https://www.youtube.com/watch?v=I1dAZuWurw4
// https://github.com/mixandjam/balatro-feel

public class CardMovement : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerUpHandler, IPointerDownHandler
{
    private Image _imageComponent;
    [SerializeField] private bool instantiateVisual = true;
    private VisualCardsHandler _visualHandler;
    
    private Vector3 offset; 

    [Header("Movement")]
    [SerializeField] private float moveSpeedLimit = 3000; 

    [Header("Selection")]
    public bool selected;
    public float selectionOffset = 50;
    private float _pointerDownTime;
    private float _pointerUpTime;

    public CardDataSO cardData;
    
    [Header("Visual")]
    [HideInInspector] public CardAnimator cardAnimator;
    public TextMeshProUGUI hotkeyText;
    
    [Header("Play Area Threshold")]
    [Tooltip("How high up the screen we the spawn the pin, in pixels")]
    [SerializeField] private float playAreaThresholdY = 500f; 
    
    [HideInInspector] public bool isPreviewingInWorld = false;

    private Vector3 _preSelectionPosition;
    
    [Header("States")]
    public bool isHovering;
    public bool isDragging;
    [HideInInspector] public bool wasDragged;
    [HideInInspector] public bool wasSuccessfullyPlayed = false;
    
    [Header("Events")]
    [HideInInspector] public UnityEvent<CardMovement> PointerEnterEvent;
    [HideInInspector] public UnityEvent<CardMovement> PointerExitEvent;
    [HideInInspector] public UnityEvent<CardMovement, bool> PointerUpEvent;
    [HideInInspector] public UnityEvent<CardMovement> PointerDownEvent;
    [HideInInspector] public UnityEvent<CardMovement> BeginDragEvent;
    [HideInInspector] public UnityEvent<CardMovement> EndDragEvent;
    [HideInInspector] public UnityEvent<CardMovement, bool> SelectEvent;
    
    [HideInInspector] public UnityEvent OnEnterPlayArea;
    [HideInInspector] public UnityEvent OnExitPlayArea;
    [HideInInspector] public UnityEvent OnDropInPlayArea;

    private void Awake()
    {
        _imageComponent = GetComponent<Image>();
        _visualHandler = GameObject.FindGameObjectWithTag("VisualCardHandler").GetComponent<VisualCardsHandler>();
    }

    void Start()
    {
        // idk where the hotkey text is being set
        if (hotkeyText != null)
        {
            hotkeyText.gameObject.SetActive(false);
        }

        if (!instantiateVisual || cardData == null || cardData.cardVisualPrefab == null)
            return;

        cardAnimator = Instantiate(cardData.cardVisualPrefab, _visualHandler.transform).GetComponent<CardAnimator>();
        
        cardAnimator.Initialize(this);
    }
    // Forces the card into a drag state programmatically
    public void ForceStartDrag()
    {
        wasSuccessfullyPlayed = false;

        // Tell HorizontalCardHolder to set this as the selected card
        BeginDragEvent.Invoke(this); 
        
        // Center the card directly on the mouse
        offset = Vector3.zero; 
        
        isDragging = true;
        isDragging = true;
        wasDragged = true;
        
        // Prevent the card from blocking raycasts while dragging
        _imageComponent.raycastTarget = false; 
    }
    void Update()
    {
        ClampPosition();

        if (isDragging)
        {
            // MOVE MOVEMENT LOGIC HERE: Since the EventSystem won't fire OnDrag 
            // automatically when forced, we update the position in Update()
            if (!isPreviewingInWorld)
            {
                transform.position = Input.mousePosition - offset;
            }

            HandlePlayAreaTransition();

            // MANUAL DROP DETECTION: Listen for the mouse release manually
            if (Input.GetMouseButtonUp(0))
            {
                OnEndDrag(null); // Passing null is perfectly safe here
            }
        }
    }

    // UPDATE OnDrag: You can completely empty out OnDrag since we moved 
    // the movement logic into Update(), but keep the interface method so Unity doesn't complain.
    public void OnDrag(PointerEventData eventData) 
    { 
        // Logic moved to Update() to support both UI dragging and programmatic dragging
    }

    // Toggles the card's visual state when dragged in or out of the designated play area.
    private void HandlePlayAreaTransition()
    {
        bool isMouseInPlayArea = Input.mousePosition.y > playAreaThresholdY;

        if (isMouseInPlayArea && !isPreviewingInWorld)
        {
            // TRANSITION TO WORLD
            isPreviewingInWorld = true;
            
            _imageComponent.enabled = false;
            cardAnimator.gameObject.SetActive(false);

            OnEnterPlayArea?.Invoke();
        }
        else if (!isMouseInPlayArea && isPreviewingInWorld)
        {
            // TRANSITION BACK TO HAND
            isPreviewingInWorld = false;

            _imageComponent.enabled = true;
            cardAnimator.gameObject.SetActive(true);

            OnExitPlayArea?.Invoke();
        }
    }

    // Prevents the UI card from being dragged outside the edges of the screen.
    void ClampPosition()
    {
        Vector3 clampedPosition = transform.position;
        
        clampedPosition.x = Mathf.Clamp(clampedPosition.x, 0, Screen.width);
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, 0, Screen.height);
        
        transform.position = clampedPosition;
    }

    // Triggers drag events, calculates the grab offset.
    public void OnBeginDrag(PointerEventData eventData)
    {
        wasSuccessfullyPlayed = false;

        BeginDragEvent.Invoke(this);
        
        offset = Input.mousePosition - transform.position;
        isDragging = true;
        
        _imageComponent.raycastTarget = false;

        wasDragged = true;
    }

    // Fires drop events and resolves placement based on whether the card is in the play area.
    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        
        if (isPreviewingInWorld)
        {
            EndDragEvent.Invoke(this);
            OnDropInPlayArea?.Invoke();

            if (!wasSuccessfullyPlayed)
            {
                if (transform.parent != null && !transform.parent.CompareTag("Slot") && HandManager.Instance != null)
                {
                    HandManager.Instance.AssignSlotToCard(this);
                }
                
                ReturnToHand();
            }
        }
        else
        {
            // NEW: If this is a floating card pulled from the world, it needs a slot 
            // BEFORE we fire EndDragEvent so the DOLocalMove animation targets the slot center!
            if (transform.parent != null && !transform.parent.CompareTag("Slot") && HandManager.Instance != null)
            {
                HandManager.Instance.DiscardCard(this);
            }

            EndDragEvent.Invoke(this);
            ReturnToHand();
        }
    }

    // Forcefully cancels the drag without triggering OnDropInPlayArea
    public void CancelDrag()
    {
        isDragging = false;
        
        if (transform.parent != null && !transform.parent.CompareTag("Slot") && HandManager.Instance != null)
        {
            HandManager.Instance.AssignSlotToCard(this);
        }

        EndDragEvent.Invoke(this);
        ReturnToHand();
    }
    
    // Cancels the play preview and restores the card's UI visuals in the hand.
    public void ReturnToHand()
    {
        Debug.Log("CardMovement REutnr to hand");
        // FAILED TO PLACE (Cell occupied or invalid) - Return to Hand
        
        if (isPreviewingInWorld)
        {
            OnExitPlayArea?.Invoke();
            isPreviewingInWorld = false;
        }

        _imageComponent.enabled = true;
        if (cardAnimator != null) 
            cardAnimator.gameObject.SetActive(true);

        // RETURNED TO HAND
        _imageComponent.raycastTarget = true;

        StartCoroutine(FrameWait());
    }

    // Waits for the end of the frame to reset the dragged state flag.
    private IEnumerator FrameWait()
    {
        yield return new WaitForEndOfFrame();
        wasDragged = false;
    }

    // Cleans up the card and its container slot after being successfully played.
    public void SuccessfulPlay()
    {
        wasSuccessfullyPlayed = true;

        // The placed world object now owns this card and returns it to the deck later.
        if (HandManager.Instance != null)
        {
            HandManager.Instance.ConsumePlayedCard(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Fires the enter event and marks the card as hovering.
    public void OnPointerEnter(PointerEventData eventData)
    {
        PointerEnterEvent.Invoke(this);
        isHovering = true;
    }

    // Fires the exit event and removes the hovering state.
    public void OnPointerExit(PointerEventData eventData)
    {
        PointerExitEvent.Invoke(this);
        isHovering = false;
    }

    // Select a card
    // Records the start time of a left mouse click for tap vs. drag detection.
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            Debug.LogError("returned");
            return;
        }

        PointerDownEvent.Invoke(this);
        _pointerDownTime = Time.time;
    }

    // Placing pin
    // Determines if a click was a quick tap to toggle selection and fires relevant events.
    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        _pointerUpTime = Time.time;

        PointerUpEvent.Invoke(this, _pointerUpTime - _pointerDownTime > .2f);

        if (_pointerUpTime - _pointerDownTime > .2f)
            return;

        if (wasDragged)
            return;

        selected = !selected;
        SelectEvent.Invoke(this, selected);

        if (selected)
        {
            _preSelectionPosition = transform.localPosition;
            transform.localPosition += (cardAnimator.transform.up * selectionOffset);
        }
        else
        {
            transform.localPosition = _preSelectionPosition;
        }
    }

    // Returns the total number of sibling slots in the parent container.
    public int SiblingAmount()
    {
        return transform.parent != null && transform.parent.parent != null && transform.parent.CompareTag("Slot") ? transform.parent.parent.childCount - 1 : 0;
    }

    // Gets the current index of this card's UI slot within its parent container.
    public int ParentIndex()
    {
        return transform.parent != null && transform.parent.CompareTag("Slot") ? transform.parent.GetSiblingIndex() : 0;
    }

    // Calculates the card's relative position (0.0 to 1.0) within the hand.
    public float NormalizedPosition()
    {
        return transform.parent != null && transform.parent.parent != null && transform.parent.CompareTag("Slot") ? ExtensionMethods.Remap((float)ParentIndex(), 0, (float)(transform.parent.parent.childCount - 1), 0, 1) : 0;
    }

    // Cleans up the linked visual animator object when this card is destroyed.
    private void OnDestroy()
    {
        if(cardAnimator != null)
            Destroy(cardAnimator.gameObject);
    }
}
