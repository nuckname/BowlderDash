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
    private Canvas canvas;
    private Image imageComponent;
    [SerializeField] private bool instantiateVisual = true;
    private VisualCardsHandler visualHandler;
    
    private Vector3 offset; 

    [Header("Movement")]
    [SerializeField] private float moveSpeedLimit = 3000; 

    [Header("Selection")]
    public bool selected;
    public float selectionOffset = 50;
    private float pointerDownTime;
    private float pointerUpTime;

    [Header("Visual")]
    [SerializeField] private GameObject cardVisualPrefab;
    [HideInInspector] public CardAnimator cardAnimator;

    [Header("World Interaction")]
    // Object we want to spawn
    [SerializeField] private GameObject worldGameObjectPrefab; 
    [Tooltip("How high up the screen we the spawn the pin, in pixels")]
    [SerializeField] private float playAreaThresholdY = 500f; 
    [SerializeField] private LayerMask worldPlacementMask; 

    private GameObject spawnedWorldObject;
    private bool isPreviewingInWorld = false;

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

    void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        imageComponent = GetComponent<Image>();

        if (!instantiateVisual)
            return;

        visualHandler = FindObjectOfType<VisualCardsHandler>();
        cardAnimator = Instantiate(cardVisualPrefab, visualHandler ? visualHandler.transform : canvas.transform).GetComponent<CardAnimator>();
        cardAnimator.Initialize(this);
    }

    void Update()
    {
        ClampPosition();

        if (isDragging)
        {
            HandlePlayAreaTransition();

            if (isPreviewingInWorld && spawnedWorldObject != null)
            {
                // Move the WORLD object using Raycasting
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 1000f, worldPlacementMask))
                {
                    spawnedWorldObject.transform.position = hit.point;
                }
            }
            else
            {
                // Move the UI card using original velocity logic
                Vector3 targetPosition = Input.mousePosition - offset;
                Vector3 direction = (targetPosition - transform.position).normalized;
                
                float distance = Vector3.Distance(transform.position, targetPosition);
                Vector3 velocity = direction * Mathf.Min(moveSpeedLimit, distance / Time.deltaTime);
                
                transform.Translate(velocity * Time.deltaTime, Space.World);
            }
        }
    }

    private void HandlePlayAreaTransition()
    {
        bool isMouseInPlayArea = Input.mousePosition.y > playAreaThresholdY;

        if (isMouseInPlayArea && !isPreviewingInWorld)
        {
            // TRANSITION TO WORLD
            isPreviewingInWorld = true;
            
            imageComponent.enabled = false;
            if (cardAnimator != null) 
                cardAnimator.gameObject.SetActive(false);

            if (spawnedWorldObject == null && worldGameObjectPrefab != null)
            {
                spawnedWorldObject = Instantiate(worldGameObjectPrefab);
            }
        }
        else if (!isMouseInPlayArea && isPreviewingInWorld)
        {
            // TRANSITION BACK TO HAND
            isPreviewingInWorld = false;

            imageComponent.enabled = true;
            if (cardAnimator != null) 
                cardAnimator.gameObject.SetActive(true);

            if (spawnedWorldObject != null)
            {
                Destroy(spawnedWorldObject);
                spawnedWorldObject = null;
            }
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
        
        canvas.GetComponent<GraphicRaycaster>().enabled = false;
        imageComponent.raycastTarget = false;

        wasDragged = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Drag logic handled in Update
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        EndDragEvent.Invoke(this);
        isDragging = false;
        
        if (isPreviewingInWorld)
        {
            spawnedWorldObject = null;
            Destroy(gameObject);
            return; 
        }

        // RETURNED TO HAND
        canvas.GetComponent<GraphicRaycaster>().enabled = true;
        imageComponent.raycastTarget = true;

        StartCoroutine(FrameWait());

        IEnumerator FrameWait()
        {
            yield return new WaitForEndOfFrame();
            wasDragged = false;
        }
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

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        PointerDownEvent.Invoke(this);
        pointerDownTime = Time.time;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        pointerUpTime = Time.time;

        PointerUpEvent.Invoke(this, pointerUpTime - pointerDownTime > .2f);

        if (pointerUpTime - pointerDownTime > .2f)
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
            
        // Clean up preview if destroyed mid-drag
        if (spawnedWorldObject != null)
            Destroy(spawnedWorldObject);
    }
}

public static class ExtensionMethods
{
    public static float Remap(this float value, float from1, float to1, float from2, float to2)
    {
        return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
    }
}