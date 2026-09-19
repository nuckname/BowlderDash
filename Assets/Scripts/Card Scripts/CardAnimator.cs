using System;
using UnityEngine;
using DG.Tweening;
using System.Collections;
using UnityEngine.EventSystems;
using Unity.Collections;
using UnityEngine.UI;
using Unity.VisualScripting;

//https://www.youtube.com/watch?v=I1dAZuWurw4
//https://github.com/mixandjam/balatro-feel

public class CardAnimator : MonoBehaviour
{
    private bool initalize = false;

    [Header("Shop Mode")]
    public bool isShopMode = false;
    public bool shopIsHovering = false;
    private float baseScale = 1f;

    [Header("Card")]
    public CardMovement parentCardMovement;
    private Transform cardTransform;
    private Vector3 rotationDelta;
    private int savedIndex;
    Vector3 movementDelta;
    private Canvas canvas;

    [Header("References")]
    private float shadowOffset = 20;
    [SerializeField] private Transform shakeParent;
    [SerializeField] private Transform tiltParent;
    [SerializeField] private Image cardImage;
    [SerializeField] private CardVisualPresenter visualPresenter;

    [Header("Follow Parameters")]
    [SerializeField] private float followSpeed = 30;

    [Header("Rotation Parameters")]
    [SerializeField] private float rotationAmount = 13;
    [SerializeField] private float rotationSpeed = 13;
    [SerializeField] private float autoTiltAmount = 15;
    [SerializeField] private float manualTiltAmount = 1;
    [SerializeField] private float tiltSpeed = 20;

    [Header("Scale Parameters")]
    [SerializeField] private bool scaleAnimations = true;
    [SerializeField] private float scaleOnHover = 1.15f;
    [SerializeField] private float scaleOnSelect = 1.25f;
    [SerializeField] private float scaleTransition = .15f;
    [SerializeField] private Ease scaleEase = Ease.OutBack;
    
    private float randomTimeOffset;
    
    [Header("Select Parameters")]
    [SerializeField] private float selectPunchAmount = 20;

    [Header("Hober Parameters")]
    [SerializeField] private float hoverPunchAngle = 5;
    [SerializeField] private float hoverTransition = .15f;

    [Header("Swap Parameters")]
    [SerializeField] private bool swapAnimations = true;
    [SerializeField] private float swapRotationAngle = 30;
    [SerializeField] private float swapTransition = .15f;
    [SerializeField] private int swapVibrato = 5;

    [Header("Curve")]
    [SerializeField] private CurveParameters curve;

    private float curveYOffset;
    private float curveRotationOffset;
    private Coroutine pressCoroutine;

    private int _currentHandLength;

    public void Initialize(CardMovement target, int index = 0)
    {
        baseScale = 1f;
        isShopMode = false;
        
        //Declarations
        parentCardMovement = target;
        cardTransform = target.transform;
        canvas = GetComponent<Canvas>();
        
        if (visualPresenter == null)
        {
            visualPresenter = GetComponentInChildren<CardVisualPresenter>(true);
        }

        if (visualPresenter != null)
        {
            visualPresenter.Bind(target.cardData);
        }

        //Event Listening
        parentCardMovement.PointerEnterEvent.AddListener(PointerEnter);
        parentCardMovement.PointerExitEvent.AddListener(PointerExit);
        parentCardMovement.BeginDragEvent.AddListener(BeginDrag);
        parentCardMovement.EndDragEvent.AddListener(EndDrag);
        parentCardMovement.PointerDownEvent.AddListener(PointerDown);
        parentCardMovement.PointerUpEvent.AddListener(PointerUp);
        parentCardMovement.SelectEvent.AddListener(Select);

        //Initialization
        initalize = true;
    }

    public void InitializeShopMode(Transform targetTransform, CardDataSO cardData, float customBaseScale = 1f)
    {
        cardTransform = targetTransform;
        baseScale = customBaseScale;
        transform.localScale = Vector3.one * baseScale;
        canvas = GetComponent<Canvas>();
        
        if (visualPresenter == null)
        {
            visualPresenter = GetComponentInChildren<CardVisualPresenter>(true);
        }

        if (visualPresenter != null)
        {
            visualPresenter.Bind(cardData);
        }

        isShopMode = true;
        initalize = true;
        
        randomTimeOffset = UnityEngine.Random.Range(0f, 1000f);
    }
    
    public void UpdateLength(int length)
    {
        _currentHandLength = length;
    }
    
    private void HandPositioning(int length)
    {
        curveYOffset = (curve.positioning.Evaluate(parentCardMovement.NormalizedPosition()) * curve.positioningInfluence) * _currentHandLength;
    }

    void Update()
    {
        if (!initalize) return;

        if (isShopMode)
        {
            curveYOffset = 0;
            curveRotationOffset = 0;
            SmoothFollow();
            FollowRotation();
            ShopCardTilt();
            return;
        }

        if (parentCardMovement == null) return;

        HandPositioning();
        SmoothFollow();
        FollowRotation();
        CardTilt();

    }

    private void HandPositioning()
    {
        // Replaced parentCardMovement.SiblingAmount() with currentHandLength
        curveYOffset = (curve.positioning.Evaluate(parentCardMovement.NormalizedPosition()) * curve.positioningInfluence) * _currentHandLength;
        curveYOffset = _currentHandLength < 5 ? 0 : curveYOffset;
        curveRotationOffset = curve.rotation.Evaluate(parentCardMovement.NormalizedPosition());
    }
    
    private void SmoothFollow()
    {
        bool isDragging = parentCardMovement != null && parentCardMovement.isDragging;
        Vector3 verticalOffset = (Vector3.up * (isDragging ? 0 : curveYOffset));
        transform.position = Vector3.Lerp(transform.position, cardTransform.position + verticalOffset, followSpeed * Time.deltaTime);
    }

    private void FollowRotation()
    {
        Vector3 movement = (transform.position - cardTransform.position);
        movementDelta = Vector3.Lerp(movementDelta, movement, 25 * Time.deltaTime);
        
        bool isDragging = parentCardMovement != null && parentCardMovement.isDragging;
        
        // IMPORTANT: We divide by 100f here because 'movement' is now in pixels (hundreds of units)
        // instead of world space (single units). This prevents extreme rotation snapping.
        Vector3 movementRotation = (isDragging ? movementDelta : movement) * (rotationAmount / 100f); 
        
        rotationDelta = Vector3.Lerp(rotationDelta, movementRotation, rotationSpeed * Time.deltaTime);
        transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y, Mathf.Clamp(rotationDelta.x, -60, 60));
    }

    private void CardTilt()
    {
        savedIndex = parentCardMovement.isDragging ? savedIndex : parentCardMovement.ParentIndex();
        float sine = Mathf.Sin(Time.time + savedIndex) * (parentCardMovement.isHovering ? .2f : 1);
        float cosine = Mathf.Cos(Time.time + savedIndex) * (parentCardMovement.isHovering ? .2f : 1);

        Vector3 offset = transform.position - Input.mousePosition;
        
        // Scale down the pixel offset so the rotation math doesn't explode
        float pixelScaleFactor = 100f; 

        float tiltX = parentCardMovement.isHovering ? (((offset.y / pixelScaleFactor) * -1) * manualTiltAmount) : 0;
        float tiltY = parentCardMovement.isHovering ? (((offset.x / pixelScaleFactor)) * manualTiltAmount) : 0;
        float tiltZ = parentCardMovement.isDragging ? tiltParent.eulerAngles.z : (curveRotationOffset * (curve.rotationInfluence * parentCardMovement.SiblingAmount()));

        float lerpX = Mathf.LerpAngle(tiltParent.eulerAngles.x, tiltX + (sine * autoTiltAmount), tiltSpeed * Time.deltaTime);
        float lerpY = Mathf.LerpAngle(tiltParent.eulerAngles.y, tiltY + (cosine * autoTiltAmount), tiltSpeed * Time.deltaTime);
        float lerpZ = Mathf.LerpAngle(tiltParent.eulerAngles.z, tiltZ, tiltSpeed / 2 * Time.deltaTime);

        tiltParent.eulerAngles = new Vector3(lerpX, lerpY, lerpZ);
    }
    
    private void ShopCardTilt()
    {
        float sine = Mathf.Sin(Time.time + randomTimeOffset) * (shopIsHovering ? .2f : 1);
        float cosine = Mathf.Cos(Time.time + randomTimeOffset) * (shopIsHovering ? .2f : 1);

        Vector3 offset = transform.position - Input.mousePosition;
        float pixelScaleFactor = 100f; 

        float tiltX = shopIsHovering ? (((offset.y / pixelScaleFactor) * -1) * manualTiltAmount) : 0;
        float tiltY = shopIsHovering ? (((offset.x / pixelScaleFactor)) * manualTiltAmount) : 0;
        float tiltZ = 0;

        float lerpX = Mathf.LerpAngle(tiltParent.eulerAngles.x, tiltX + (sine * autoTiltAmount), tiltSpeed * Time.deltaTime);
        float lerpY = Mathf.LerpAngle(tiltParent.eulerAngles.y, tiltY + (cosine * autoTiltAmount), tiltSpeed * Time.deltaTime);
        float lerpZ = Mathf.LerpAngle(tiltParent.eulerAngles.z, tiltZ, tiltSpeed / 2 * Time.deltaTime);

        tiltParent.eulerAngles = new Vector3(lerpX, lerpY, lerpZ);
    }

    private void Select(CardMovement cardMovement, bool state) { PlaySelect(state); }
    
    public void PlaySelect(bool state)
    {
        DOTween.Kill(2, true);
        float dir = state ? 1 : 0;
        shakeParent.DOPunchPosition(shakeParent.up * selectPunchAmount * dir, scaleTransition, 10, 1);
        shakeParent.DOPunchRotation(Vector3.forward * (hoverPunchAngle/2), hoverTransition, 20, 1).SetId(2);

        if(scaleAnimations)
            transform.DOScale(baseScale * scaleOnHover, scaleTransition).SetEase(scaleEase);

    }

    public void Swap(float dir = 1)
    {
        if (!swapAnimations)
            return;

        DOTween.Kill(2, true);
        shakeParent.DOPunchRotation((Vector3.forward * swapRotationAngle) * dir, swapTransition, swapVibrato, 1).SetId(3);
    }

    private void BeginDrag(CardMovement cardMovement) { PlayBeginDrag(); }
    public void PlayBeginDrag()
    {
        if(scaleAnimations)
            transform.DOScale(baseScale * scaleOnSelect, scaleTransition).SetEase(scaleEase);

        if (canvas != null) canvas.overrideSorting = true;
    }

    private void EndDrag(CardMovement cardMovement) { PlayEndDrag(); }
    public void PlayEndDrag()
    {
        if (canvas != null) canvas.overrideSorting = false;
        if (scaleAnimations) transform.DOScale(baseScale, scaleTransition).SetEase(scaleEase);
    }

    private void PointerEnter(CardMovement cardMovement) { PlayPointerEnter(); }
    public void PlayPointerEnter()
    {
        if(scaleAnimations)
            transform.DOScale(baseScale * scaleOnHover, scaleTransition).SetEase(scaleEase);

        DOTween.Kill(2, true);
        shakeParent.DOPunchRotation(Vector3.forward * hoverPunchAngle, hoverTransition, 20, 1).SetId(2);
    }

    private void PointerExit(CardMovement cardMovement) { PlayPointerExit(); }
    public void PlayPointerExit()
    {
        bool wasDragged = parentCardMovement != null && parentCardMovement.wasDragged;
        if (!wasDragged && scaleAnimations)
            transform.DOScale(baseScale, scaleTransition).SetEase(scaleEase);
    }

    private void PointerUp(CardMovement cardMovement, bool longPress) { PlayPointerUp(longPress); }
    public void PlayPointerUp(bool longPress)
    {
        if(scaleAnimations)
            transform.DOScale(baseScale * (longPress ? scaleOnHover : scaleOnSelect), scaleTransition).SetEase(scaleEase);
        if (canvas != null) canvas.overrideSorting = false;
    }

    private void PointerDown(CardMovement cardMovement) { PlayPointerDown(); }
    public void PlayPointerDown()
    {
        if(scaleAnimations)
            transform.DOScale(baseScale * scaleOnSelect, scaleTransition).SetEase(scaleEase);
    }
}