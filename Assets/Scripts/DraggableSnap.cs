using UnityEngine;
using System.Collections.Generic;

// https://share.gemini.google/0ZSCmXAsuGQ7

[RequireComponent(typeof(Collider))]
public class DraggableSnap : MonoBehaviour
{
    [Header("Drag Settings")]
    public float dragElevation = 0.5f;
    
    [Tooltip("Must match the playAreaThresholdY from CardMovement!")]
    public float playAreaThresholdY; 

    [SerializeField] private bool canBeReturnedToHand = true;
    
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
    }

    private bool CanInteract =>
        _interactionEnabled &&
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
        if (!CanInteract || _gridOccupancy == null || _hexGrid == null || _gridManager == null || _cam == null)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
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
            
            _peerOriginalCells[snap] = cell;
            _peerOriginalPositions[snap] = obj.transform.position;
            
            Vector2Int axial = _gridManager.GetAxialCoordinate(cell);
            _peerAxialOffsets[snap] = axial - myAxial;

            if (cell != null)
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
            _dragging = false;
            
            // Clean up visuals before returning to hand
            foreach (var obj in GetGroupObjects())
            {
                UpdatePreviewVisuals(obj, true); 
            }

            if (_worldObjectToCard != null)
            {
                _worldObjectToCard.ReturnToHand(); 
            }
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
                // Snap visuals to the nearest cell and check validity for each piece
                foreach (var obj in GetGroupObjects())
                {
                    var snap = obj.GetComponent<DraggableSnap>();
                    HexCell target = _gridManager.GetCellWithOffset(anchorNearest, _peerAxialOffsets[snap]);
                    
                    bool isThisPinValid = target != null && target.occupant == null;

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

        if (!CanInteract)
        {
            CancelDrag();
            return;
        }

        _dragging = false;

        HexCell finalAnchor = _hexGrid.GetNearestCell(transform.position);
        bool allFit = true;

        if (finalAnchor != null)
        {
            foreach (var obj in GetGroupObjects())
            {
                var snap = obj.GetComponent<DraggableSnap>();
                HexCell target = _gridManager.GetCellWithOffset(finalAnchor, _peerAxialOffsets[snap]);
                
                if (target == null || target.occupant != null)
                {
                    allFit = false;
                    break;
                }
            }
        }
        else
        {
            allFit = false;
        }

        if (allFit)
        {
            // Reserve and drop into new spots
            foreach (var obj in GetGroupObjects())
            {
                var snap = obj.GetComponent<DraggableSnap>();
                HexCell target = _gridManager.GetCellWithOffset(finalAnchor, _peerAxialOffsets[snap]);
                
                _gridOccupancy.TryReserveCell(target, obj.transform);
                
                // Force the object to snap perfectly to the floor height of the hex cell.
                obj.transform.position = new Vector3(
                    target.center.x, 
                    target.center.y, 
                    target.center.z);

                // Ensure visuals are restored
                UpdatePreviewVisuals(obj, true);
            }
        }
        else
        {
            RestoreOriginalPlacement();
        }

        ClearDragState();
    }

    private void RestoreOriginalPlacement()
    {
        foreach (var obj in GetGroupObjects())
        {
            var snap = obj.GetComponent<DraggableSnap>();
            if (snap == null || !_peerOriginalPositions.TryGetValue(snap, out Vector3 originalPosition))
            {
                continue;
            }

            if (_peerOriginalCells.TryGetValue(snap, out HexCell originalCell) && originalCell != null)
            {
                _gridOccupancy.TryReserveCell(originalCell, obj.transform);
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
        _peerAxialOffsets.Clear();
        _peerOriginalCells.Clear();
        _peerOriginalPositions.Clear();
        _originalMaterials.Clear();
        _pinValidityStates.Clear();
    }
}