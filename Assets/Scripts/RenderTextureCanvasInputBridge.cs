using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RenderTextureCanvasInputBridge : MonoBehaviour
{
    [Header("Interaction Surface")]
    [SerializeField] private Camera inputCamera;
    [SerializeField] private Collider interactionCollider;
    [SerializeField] private RectTransform interactionSurface;

    [Header("Target UI")]
    [SerializeField] private GraphicRaycaster targetRaycaster;
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private Vector2 virtualCanvasResolution = new Vector2(1000f, 800f);

    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    private PointerEventData pointerData;
    private Vector2 lastVirtualPointerPosition;
    private bool hasLastVirtualPointerPosition;
    private GameObject currentHoveredObject;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();
        if (inputCamera == null || interactionCollider == null || interactionSurface == null || targetRaycaster == null || eventSystem == null)
        {
            return;
        }

        if (pointerData == null)
        {
            pointerData = new PointerEventData(eventSystem);
        }

        bool hitSurface = TryGetVirtualPointerPosition(out Vector2 virtualPosition);
        if (hitSurface)
        {
            lastVirtualPointerPosition = virtualPosition;
            hasLastVirtualPointerPosition = true;
        }

        pointerData.Reset();
        pointerData.button = PointerEventData.InputButton.Left;
        pointerData.position = hitSurface
            ? virtualPosition
            : (hasLastVirtualPointerPosition ? lastVirtualPointerPosition : new Vector2(-9999f, -9999f));
        pointerData.delta = Vector2.zero;
        pointerData.scrollDelta = Input.mouseScrollDelta;

        GameObject currentOverGo = null;
        if (hitSurface)
        {
            raycastResults.Clear();
            targetRaycaster.Raycast(pointerData, raycastResults);
            if (raycastResults.Count > 0)
            {
                pointerData.pointerCurrentRaycast = raycastResults[0];
                currentOverGo = raycastResults[0].gameObject;
            }
            else
            {
                pointerData.pointerCurrentRaycast = new RaycastResult();
            }
        }
        else
        {
            pointerData.pointerCurrentRaycast = new RaycastResult();
        }

        UpdatePointerHover(currentOverGo);

        if (hitSurface && pointerData.scrollDelta.sqrMagnitude > 0.0f && currentOverGo != null)
        {
            ExecuteEvents.ExecuteHierarchy(currentOverGo, pointerData, ExecuteEvents.scrollHandler);
        }

        if (Input.GetMouseButtonDown(0))
        {
            ProcessPointerDown(currentOverGo);
        }

        if (Input.GetMouseButton(0))
        {
            ProcessDrag();
        }

        if (Input.GetMouseButtonUp(0))
        {
            ProcessPointerUp(currentOverGo);
        }
    }

    private void ResolveReferences()
    {
        if (inputCamera == null)
        {
            inputCamera = Camera.main;
        }

        if (eventSystem == null)
        {
            eventSystem = EventSystem.current;
        }

        if (interactionSurface == null)
        {
            interactionSurface = GetComponent<RectTransform>();
        }

        if (interactionCollider == null)
        {
            interactionCollider = GetComponent<Collider>();
        }

        if (targetRaycaster == null)
        {
            targetRaycaster = FindObjectOfType<GraphicRaycaster>(true);
        }
    }

    private bool TryGetVirtualPointerPosition(out Vector2 virtualPosition)
    {
        virtualPosition = Vector2.zero;

        Ray ray = inputCamera.ScreenPointToRay(Input.mousePosition);
        if (!interactionCollider.Raycast(ray, out RaycastHit hit, inputCamera.farClipPlane))
        {
            return false;
        }

        Vector3 localHitPoint = interactionSurface.InverseTransformPoint(hit.point);
        Rect rect = interactionSurface.rect;

        float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, localHitPoint.x);
        float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, localHitPoint.y);
        if (normalizedX < 0f || normalizedX > 1f || normalizedY < 0f || normalizedY > 1f)
        {
            return false;
        }

        virtualPosition = new Vector2(
            normalizedX * virtualCanvasResolution.x,
            normalizedY * virtualCanvasResolution.y
        );
        return true;
    }

    private void ProcessPointerDown(GameObject currentOverGo)
    {
        pointerData.eligibleForClick = true;
        pointerData.delta = Vector2.zero;
        pointerData.dragging = false;
        pointerData.useDragThreshold = true;
        pointerData.pressPosition = pointerData.position;
        pointerData.pointerPressRaycast = pointerData.pointerCurrentRaycast;

        DeselectIfSelectionChanged(currentOverGo, pointerData);

        GameObject newPressed = ExecuteEvents.ExecuteHierarchy(currentOverGo, pointerData, ExecuteEvents.pointerDownHandler);
        if (newPressed == null)
        {
            newPressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);
        }

        pointerData.pointerPress = newPressed;
        pointerData.rawPointerPress = currentOverGo;
        pointerData.clickTime = Time.unscaledTime;
        pointerData.clickCount = 1;

        pointerData.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(currentOverGo);
        if (pointerData.pointerDrag != null)
        {
            ExecuteEvents.Execute(pointerData.pointerDrag, pointerData, ExecuteEvents.initializePotentialDrag);
        }
    }

    private void ProcessDrag()
    {
        if (pointerData.pointerDrag == null)
        {
            return;
        }

        if (!pointerData.dragging && ShouldStartDrag(pointerData.pressPosition, pointerData.position, eventSystem.pixelDragThreshold, pointerData.useDragThreshold))
        {
            ExecuteEvents.Execute(pointerData.pointerDrag, pointerData, ExecuteEvents.beginDragHandler);
            pointerData.dragging = true;
        }

        if (!pointerData.dragging)
        {
            return;
        }

        if (pointerData.pointerPress != pointerData.pointerDrag)
        {
            ExecuteEvents.Execute(pointerData.pointerPress, pointerData, ExecuteEvents.pointerUpHandler);
            pointerData.eligibleForClick = false;
            pointerData.pointerPress = null;
            pointerData.rawPointerPress = null;
        }

        ExecuteEvents.Execute(pointerData.pointerDrag, pointerData, ExecuteEvents.dragHandler);
    }

    private void ProcessPointerUp(GameObject currentOverGo)
    {
        ExecuteEvents.Execute(pointerData.pointerPress, pointerData, ExecuteEvents.pointerUpHandler);

        GameObject pointerUpHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);
        if (pointerData.eligibleForClick && pointerData.pointerPress == pointerUpHandler)
        {
            ExecuteEvents.Execute(pointerData.pointerPress, pointerData, ExecuteEvents.pointerClickHandler);
        }
        else if (pointerData.dragging && pointerData.pointerDrag != null)
        {
            ExecuteEvents.ExecuteHierarchy(currentOverGo, pointerData, ExecuteEvents.dropHandler);
        }

        if (pointerData.pointerDrag != null && pointerData.dragging)
        {
            ExecuteEvents.Execute(pointerData.pointerDrag, pointerData, ExecuteEvents.endDragHandler);
        }

        pointerData.eligibleForClick = false;
        pointerData.dragging = false;
        pointerData.pointerPress = null;
        pointerData.rawPointerPress = null;
        pointerData.pointerDrag = null;
    }

    private static bool ShouldStartDrag(Vector2 pressPos, Vector2 currentPos, float threshold, bool useDragThreshold)
    {
        if (!useDragThreshold)
        {
            return true;
        }

        return (pressPos - currentPos).sqrMagnitude >= threshold * threshold;
    }

    private void UpdatePointerHover(GameObject nextHoveredObject)
    {
        if (currentHoveredObject == nextHoveredObject)
        {
            return;
        }

        if (currentHoveredObject != null)
        {
            ExecuteEvents.Execute(currentHoveredObject, pointerData, ExecuteEvents.pointerExitHandler);
        }

        currentHoveredObject = nextHoveredObject;

        if (currentHoveredObject != null)
        {
            ExecuteEvents.Execute(currentHoveredObject, pointerData, ExecuteEvents.pointerEnterHandler);
        }
    }

    private void DeselectIfSelectionChanged(GameObject currentOverGo, BaseEventData pointerEvent)
    {
        if (eventSystem == null)
        {
            return;
        }

        GameObject selectedObject = ExecuteEvents.GetEventHandler<ISelectHandler>(currentOverGo);
        if (eventSystem.currentSelectedGameObject == selectedObject)
        {
            return;
        }

        eventSystem.SetSelectedGameObject(null, pointerEvent);
    }
}
