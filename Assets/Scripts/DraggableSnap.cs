using UnityEngine;
using System.Collections.Generic;

// https://share.gemini.google/0ZSCmXAsuGQ7
// https://share.gemini.google/ec5tShTPfLur

[RequireComponent(typeof(Collider))]
public class DraggableSnap : MonoBehaviour
{
    [Header("Drag Settings")]
    public float dragElevation = 0.5f;
    
    [Tooltip("Must match the playAreaThresholdY from CardMovement!")]
    public float playAreaThresholdY; 

    public bool canBeReturnedToHand = true;
    
    [Header("Invalid Placement Visualization")]
    [SerializeField] private Material invalidPlacementMaterial;

    private Camera _cam;
    private bool _dragging = false;
    private Vector3 _offset;

    private GridOccupancy _gridOccupancy;
    private HexGrid _hexGrid;
    private GridManager _gridManager;
    private RoundStateManager _roundStateManager;
    private WorldObjectToCard _worldObjectToCard;
    private bool _interactionEnabled = true;
    private PinInteractionAudio _interactionAudio;

    public static DraggableSnap ActiveDrag { get; private set; }
    public bool IsDragging => _dragging;
    public bool IsPlaced => _gridOccupancy != null && _gridOccupancy.GetCellByOccupant(transform) != null;

    // Group tracking data
    private Dictionary<DraggableSnap, Vector2Int> _peerAxialOffsets = new Dictionary<DraggableSnap, Vector2Int>();
    private Dictionary<DraggableSnap, HexCell> _peerOriginalCells = new Dictionary<DraggableSnap, HexCell>();
    private Dictionary<DraggableSnap, Vector3> _peerOriginalPositions = new Dictionary<DraggableSnap, Vector3>();
    
    // Visual tracking data
    private Dictionary<GameObject, bool> _pinValidityStates = new Dictionary<GameObject, bool>();
    private Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();

    private void Awake()
    {
        _cam = Camera.main;
        _gridOccupancy = FindFirstObjectByType<GridOccupancy>();
        _hexGrid = FindFirstObjectByType<HexGrid>();
        _gridManager = FindFirstObjectByType<GridManager>();
        _roundStateManager = FindFirstObjectByType<RoundStateManager>();
        
        _worldObjectToCard = GetComponent<WorldObjectToCard>();
        _interactionAudio = GetComponent<PinInteractionAudio>();
    }

    public bool CanInteract =>
        isActiveAndEnabled && _interactionEnabled &&
        _roundStateManager != null &&
        _roundStateManager.IsPinPlacementActive;

    public void SetInteractionEnabled(bool interactionEnabled)
    {
        if (!interactionEnabled)
        {
            CancelDrag();
        }

        _interactionEnabled = interactionEnabled;
    }

    public void CancelDrag()
    {
        if (!_dragging) return;

        _dragging = false;
        RestoreOriginalPlacement();
        ClearDragState();
    }

    private IEnumerable<GameObject> GetGroupObjects()
    {
        yield return this.gameObject;
        
        if (_worldObjectToCard != null && _worldObjectToCard.GroupPeers != null)
        {
            foreach (var peer in _worldObjectToCard.GroupPeers)
            {
                if (peer != null && peer.gameObject != this.gameObject)
                {
                    yield return peer.gameObject;
                }
            }
        }
    }

    private void Update()
    {
        if (_dragging && Input.GetKeyDown(KeyCode.Escape))
        {
            CancelDrag();
            return;
        }
        if (!CanInteract || _gridOccupancy == null || _hexGrid == null || _gridManager == null || _cam == null)
        {
            CancelDrag();
            return;
        }

        // Do not allow picking up placed pins while a hand card is currently being dragged
        if (HandManager.Instance != null && HandManager.Instance.IsAnyCardDragging())
        {
            CancelDrag();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (_dragging || ActiveDrag != null || PinInteractionAudio.IsPointerOverUI()) return;
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            Plane gridPlane = new Plane(Vector3.up, _hexGrid.transform.position);

            if (gridPlane.Raycast(ray, out float hitDistance))
            {
                Vector3 hitPoint = ray.GetPoint(hitDistance);
                HexCell nearestCell = _hexGrid.GetNearestCell(hitPoint);

                if (nearestCell != null && Vector3.Distance(hitPoint, nearestCell.center) <= _hexGrid.hexSize)
                {
                    if (nearestCell.occupant == transform)
                    {
                        BeginDrag(ray);
                    }
                }
            }
        }
        else if (Input.GetMouseButton(0) && _dragging)
        {
            ContinueDrag();
        }
        else if (Input.GetMouseButtonUp(0) && _dragging)
        {
            EndDrag();
        }
    }
    
    private void BeginDrag(Ray ray)
    {
        _peerAxialOffsets.Clear();
        _peerOriginalCells.Clear();
        _peerOriginalPositions.Clear();
        _originalMaterials.Clear();
        _pinValidityStates.Clear();

        HexCell myOriginalCell = _gridOccupancy.GetCellByOccupant(transform);
        if (myOriginalCell == null)
        {
            return;
        }

        Vector2Int myAxial = _gridManager.GetAxialCoordinate(myOriginalCell);

        // Gather and release all peers in the group
        foreach (var obj in GetGroupObjects())
        {
            var snap = obj.GetComponent<DraggableSnap>();
            
            HexCell cell = _gridOccupancy.GetCellByOccupant(obj.transform);
            if (snap == null || !snap.CanInteract || cell == null)
            {
                ClearDragState();
                return;
            }
            
            _peerOriginalCells[snap] = cell;
            _peerOriginalPositions[snap] = obj.transform.position;
            
            Vector2Int axial = _gridManager.GetAxialCoordinate(cell);
            _peerAxialOffsets[snap] = axial - myAxial;

        }

        foreach (var obj in GetGroupObjects())
        {
            var snap = obj.GetComponent<DraggableSnap>();
            _gridOccupancy.ReleaseCell(obj.transform);
  
            float elevatedY = _peerOriginalPositions[snap].y + dragElevation;
            obj.transform.position = new Vector3(obj.transform.position.x, elevatedY, obj.transform.position.z);

            // Save original materials and set default validity
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
            {
                _originalMaterials[renderer] = renderer.materials;
            }
            _pinValidityStates[obj] = true;
        }

        // Establish drag plane offset for the specific pin we clicked
        float anchorElevatedY = transform.position.y;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, anchorElevatedY, 0f));
        
        if (plane.Raycast(ray, out float enter))
        {
            _offset = transform.position - ray.GetPoint(enter);
        }
        
        _dragging = true;
        ActiveDrag = this;
        _interactionAudio?.PlayPickup();
    }

    private void ContinueDrag()
    {
        if (!_dragging) return;

        if (!CanInteract)
        {
            CancelDrag();
            return;
        }
        
        if (Input.mousePosition.y < playAreaThresholdY && canBeReturnedToHand)
        {
            if (_worldObjectToCard == null)
            {
                CancelDrag();
                return;
            }
            _dragging = false;
            
            // Clean up visuals before returning to hand
            foreach (var obj in GetGroupObjects())
            {
                UpdatePreviewVisuals(obj, true); 
            }

            ClearDragState();
            _worldObjectToCard.ReturnToHand();
            return; 
        }

        float elevatedY = _peerOriginalPositions[this].y + dragElevation;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, elevatedY, 0f));
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);
            Vector3 targetPos = worldPoint + _offset;

            HexCell anchorNearest = _hexGrid.GetNearestCell(targetPos);
            
            if (anchorNearest != null)
            {
                bool canPlaceOrSwap = EvaluatePlacementOrSwap(
                    anchorNearest, 
                    out HashSet<Transform> swapGroupTransforms, 
                    out _);

                // Snap visuals to the nearest cell and check validity for each piece
                foreach (var obj in GetGroupObjects())
                {
                    var snap = obj.GetComponent<DraggableSnap>();
                    HexCell target = _gridManager.GetCellWithOffset(anchorNearest, _peerAxialOffsets[snap]);
                    
                    bool isThisPinValid = target != null && (
                        target.occupant == null || 
                        (canPlaceOrSwap && swapGroupTransforms != null && swapGroupTransforms.Contains(target.occupant))
                    );

                    Vector3 axialWorldOffset = _gridManager.GetWorldOffsetFromAxial(_peerAxialOffsets[snap]);
                    float peerElevatedY = _peerOriginalPositions[snap].y + dragElevation;

                    // Move pin relative to the anchor cell
                    obj.transform.position = new Vector3(
                        anchorNearest.center.x + axialWorldOffset.x, 
                        peerElevatedY, 
                        anchorNearest.center.z + axialWorldOffset.z);

                    // Update material/colliders if validity changed
                    if (!_pinValidityStates.ContainsKey(obj) || _pinValidityStates[obj] != isThisPinValid)
                    {
                        UpdatePreviewVisuals(obj, isThisPinValid);
                        _pinValidityStates[obj] = isThisPinValid;
                    }
                }
            }
        }
    }

    private void EndDrag()
    {
        if (!_dragging) return;

        if (!CanInteract || PinInteractionAudio.IsPointerOverUI())
        {
            CancelDrag();
            return;
        }

        HexCell finalAnchor = _hexGrid.GetNearestCell(transform.position);
        bool canPlace = false;
        Dictionary<Transform, HexCell> swapTargetCells = null;

        if (finalAnchor != null)
        {
            canPlace = EvaluatePlacementOrSwap(finalAnchor, out _, out swapTargetCells);
        }

        bool placed = false;
        if (canPlace)
        {
            var destinations = new Dictionary<Transform, HexCell>();
            foreach (var obj in GetGroupObjects())
            {
                var snap = obj.GetComponent<DraggableSnap>();
                HexCell target = _gridManager.GetCellWithOffset(finalAnchor, _peerAxialOffsets[snap]);
                destinations[obj.transform] = target;
            }
            if (swapTargetCells != null)
            {
                foreach (var kvp in swapTargetCells)
                    destinations[kvp.Key] = kvp.Value;
            }

            placed = _gridOccupancy.TryReserveCells(destinations);
            if (placed)
            {
                foreach (var destination in destinations)
                    destination.Key.position = destination.Value.center;
                foreach (var obj in GetGroupObjects())
                    UpdatePreviewVisuals(obj, true);
            }
        }
        if (!placed)
        {
            RestoreOriginalPlacement();
        }

        _dragging = false;
        ClearDragState();
        if (placed) _interactionAudio?.PlayRelease();
    }

    private bool EvaluatePlacementOrSwap(
        HexCell finalAnchor, 
        out HashSet<Transform> swapGroupTransforms, 
        out Dictionary<Transform, HexCell> swapTargetCells)
    {
        swapGroupTransforms = null;
        swapTargetCells = null;

        Dictionary<DraggableSnap, HexCell> myTargetCells = new Dictionary<DraggableSnap, HexCell>();
        HashSet<HexCell> myTargetCellSet = new HashSet<HexCell>();
        HashSet<Transform> overlappedOccupants = new HashSet<Transform>();

        foreach (var obj in GetGroupObjects())
        {
            var snap = obj.GetComponent<DraggableSnap>();
            HexCell target = _gridManager.GetCellWithOffset(finalAnchor, _peerAxialOffsets[snap]);
            if (target == null)
            {
                return false;
            }

            myTargetCells[snap] = target;
            myTargetCellSet.Add(target);

            if (target.occupant != null)
            {
                overlappedOccupants.Add(target.occupant);
            }
        }

        // Standard empty placement (or shifting onto our own vacated cells)
        if (overlappedOccupants.Count == 0)
        {
            return true;
        }

        // Collect the full group of the first overlapped occupant
        Transform firstOccupant = null;
        foreach (var occ in overlappedOccupants)
        {
            firstOccupant = occ;
            break;
        }

        DraggableSnap otherSnap = firstOccupant.GetComponent<DraggableSnap>();
        if (otherSnap == null || !otherSnap.CanInteract)
        {
            return false;
        }

        swapGroupTransforms = new HashSet<Transform>();
        foreach (var otherObj in otherSnap.GetGroupObjects())
        {
            swapGroupTransforms.Add(otherObj.transform);
        }

        // Only allow swapping with a single group at a time
        foreach (var occ in overlappedOccupants)
        {
            if (!swapGroupTransforms.Contains(occ))
            {
                return false;
            }
        }

        // Determine which pin in the other group acts as the swap reference anchor
        Transform otherAnchorTransform = finalAnchor.occupant;
        if (otherAnchorTransform == null || !swapGroupTransforms.Contains(otherAnchorTransform))
        {
            otherAnchorTransform = firstOccupant;
        }

        HexCell otherAnchorCell = _gridOccupancy.GetCellByOccupant(otherAnchorTransform);
        if (otherAnchorCell == null)
        {
            return false;
        }

        Vector2Int otherAnchorAxial = _gridManager.GetAxialCoordinate(otherAnchorCell);
        HexCell myOriginalAnchorCell = _peerOriginalCells[this];
        if (myOriginalAnchorCell == null)
        {
            return false;
        }

        // Verify every pin in the other group has a valid, unoccupied destination cell
        swapTargetCells = new Dictionary<Transform, HexCell>();
        foreach (Transform otherTransform in swapGroupTransforms)
        {
            HexCell currentCell = _gridOccupancy.GetCellByOccupant(otherTransform);
            if (currentCell == null)
            {
                return false;
            }

            Vector2Int axial = _gridManager.GetAxialCoordinate(currentCell);
            Vector2Int offsetFromOtherAnchor = axial - otherAnchorAxial;

            HexCell destCell = _gridManager.GetCellWithOffset(myOriginalAnchorCell, offsetFromOtherAnchor);
            if (destCell == null)
            {
                return false;
            }

            // Destination cell cannot be where our dragged group is landing
            if (myTargetCellSet.Contains(destCell))
            {
                return false;
            }

            // Destination cell must be empty (or currently held by the group being swapped)
            if (destCell.occupant != null && !swapGroupTransforms.Contains(destCell.occupant))
            {
                return false;
            }

            swapTargetCells[otherTransform] = destCell;
        }

        return true;
    }

    private void RestoreOriginalPlacement()
    {
        var destinations = new Dictionary<Transform, HexCell>();
        foreach (var original in _peerOriginalCells)
        {
            if (original.Key != null && original.Value != null)
                destinations[original.Key.transform] = original.Value;
        }
        if (_gridOccupancy != null) _gridOccupancy.TryReserveCells(destinations);

        foreach (var obj in GetGroupObjects())
        {
            var snap = obj.GetComponent<DraggableSnap>();
            if (snap == null || !_peerOriginalPositions.TryGetValue(snap, out Vector3 originalPosition))
            {
                continue;
            }

            obj.transform.position = originalPosition;
            
            // Revert materials and re-enable colliders 
            UpdatePreviewVisuals(obj, true);
        }
    }

    private void UpdatePreviewVisuals(GameObject previewObject, bool isValid)
    {
        var colliders = previewObject.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = isValid;
        }

        var renderers = previewObject.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (isValid)
            {
                if (_originalMaterials.TryGetValue(renderer, out Material[] originalMats))
                {
                    renderer.materials = originalMats;
                }
            }
            else if (invalidPlacementMaterial != null)
            {
                Material[] invalidMats = new Material[renderer.materials.Length];
                for (int j = 0; j < invalidMats.Length; j++)
                {
                    invalidMats[j] = invalidPlacementMaterial;
                }
                renderer.materials = invalidMats;
            }
        }
    }

    private void ClearDragState()
    {
        if (ActiveDrag == this) ActiveDrag = null;
        _peerAxialOffsets.Clear();
        _peerOriginalCells.Clear();
        _peerOriginalPositions.Clear();
        _originalMaterials.Clear();
        _pinValidityStates.Clear();
    }

    private void OnDisable() => CancelDrag();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDragOwner() => ActiveDrag = null;
}
