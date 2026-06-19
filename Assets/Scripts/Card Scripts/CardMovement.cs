using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Collections;
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

    [Header("Visual")]
    [SerializeField] private GameObject cardVisualPrefab;
    [HideInInspector] public CardAnimator cardAnimator;

    [Header("Play Area Threshold")]
    [Tooltip("How high up the screen we the spawn the pin, in pixels")]
    [SerializeField] private float playAreaThresholdY = 500f; 
    
    [HideInInspector] public bool isPreviewingInWorld = false;

    [Header("States")]
    public bool isHovering;
    public bool isDragging;
    [HideInInspector] public bool wasDragged;

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
        if (!instantiateVisual)
            return;

        cardAnimator = Instantiate(cardVisualPrefab, _visualHandler.transform).GetComponent<CardAnimator>();
        
        cardAnimator.Initialize(this);
    }

    void Update()
    {
        ClampPosition();

        if (isDragging)
        {
            HandlePlayAreaTransition();
        }
    }

    private void HandlePlayAreaTransition()
    {
        bool isMouseInPlayArea = Input.mousePosition.y > playAreaThresholdY;

        if (isMouseInPlayArea && !isPreviewingInWorld)
        {
            // TRANSITION TO WORLD
            isPreviewingInWorld = true;
            
            _imageComponent.enabled = false;
            if (cardAnimator != null) 
                cardAnimator.gameObject.SetActive(false);

            OnEnterPlayArea?.Invoke();
        }
        else if (!isMouseInPlayArea && isPreviewingInWorld)
        {
            // TRANSITION BACK TO HAND
            isPreviewingInWorld = false;

            _imageComponent.enabled = true;
            if (cardAnimator != null) 
                cardAnimator.gameObject.SetActive(true);

            OnExitPlayArea?.Invoke();
        }
    }

    void ClampPosition()
    {
        Vector3 clampedPosition = transform.position;
        
        clampedPosition.x = Mathf.Clamp(clampedPosition.x, 0, Screen.width);
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, 0, Screen.height);
        
        transform.position = clampedPosition;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        BeginDragEvent.Invoke(this);
        
        offset = Input.mousePosition - transform.position;
        isDragging = true;
        
        _imageComponent.raycastTarget = false;

        wasDragged = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isPreviewingInWorld)
        {
            transform.position = Input.mousePosition - offset;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        EndDragEvent.Invoke(this);
        isDragging = false;
        
        if (isPreviewingInWorld)
        {
            OnDropInPlayArea?.Invoke();
        }
        else
        {
            ReturnToHand();
        }
    }

    public void ReturnToHand()
    {
        // FAILED TO PLACE (Cell occupied or invalid) - Return to Hand
        isPreviewingInWorld = false;

        _imageComponent.enabled = true;
        if (cardAnimator != null) 
            cardAnimator.gameObject.SetActive(true);

        // RETURNED TO HAND
        _imageComponent.raycastTarget = true;

        StartCoroutine(FrameWait());
    }

    private IEnumerator FrameWait()
    {
        yield return new WaitForEndOfFrame();
        wasDragged = false;
    }

    public void SuccessfulPlay()
    {
        if (HorizontalCardHolder.Instance != null)
        {
            HorizontalCardHolder.Instance.cards.Remove(this);
        }

        // Instantly unparent the slot so childCount updates immediately
        if (transform.parent != null && transform.parent.CompareTag("Slot"))
        {
            transform.parent.SetParent(null);
            Destroy(transform.parent.gameObject);
        }
        
        // Tell the manager to update the visuals for the REMAINING cards
        if (HorizontalCardHolder.Instance != null)
        {
            HorizontalCardHolder.Instance.RebuildHandVisuals();
        }

        Destroy(gameObject);
    }


    public void OnPointerEnter(PointerEventData eventData)
    {
        PointerEnterEvent.Invoke(this);
        isHovering = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        PointerExitEvent.Invoke(this);
        isHovering = false;
    }

    // Select a card
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
            transform.localPosition += (cardAnimator.transform.up * selectionOffset);
        else
            transform.localPosition = Vector3.zero;
    }

    public void Deselect()
    {
        if (selected)
        {
            selected = false;
            transform.localPosition = Vector3.zero;
        }
    }

    public int SiblingAmount()
    {
        return transform.parent != null && transform.parent.CompareTag("Slot") ? transform.parent.parent.childCount - 1 : 0;
    }

    public int ParentIndex()
    {
        return transform.parent != null && transform.parent.CompareTag("Slot") ? transform.parent.GetSiblingIndex() : 0;
    }

    public float NormalizedPosition()
    {
        return transform.parent != null && transform.parent.CompareTag("Slot") ? ExtensionMethods.Remap((float)ParentIndex(), 0, (float)(transform.parent.parent.childCount - 1), 0, 1) : 0;
    }

    private void OnDestroy()
    {
        if(cardAnimator != null)
            Destroy(cardAnimator.gameObject);
    }
}