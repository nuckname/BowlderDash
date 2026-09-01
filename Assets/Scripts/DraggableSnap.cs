using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class DraggableSnap : MonoBehaviour
{
    [Header("Drag Settings")]
    public float dragElevation = 0.5f;
    
    [Tooltip("Must match the playAreaThresholdY from CardMovement!")]
    public float playAreaThresholdY = 500f; 

    [SerializeField] private bool canBeReturnedToHand = true;
    
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
    private HexCell _currentValidAnchor;

    private void Awake()
    {
        _cam = Camera.main;
        // Is this better then using a string tag?
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
    
    private void OnMouseDown()
    {
        if (!CanInteract || _gridOccupancy == null || _hexGrid == null || _gridManager == null || _cam == null)
        {
            return;
        }

        _peerAxialOffsets.Clear();
        _peerOriginalCells.Clear();
        _peerOriginalPositions.Clear();

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
        }

        // Establish drag plane offset for the specific pin we clicked
        float anchorElevatedY = transform.position.y;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, anchorElevatedY, 0f));
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        
        if (plane.Raycast(ray, out float enter))
        {
            _offset = transform.position - ray.GetPoint(enter);
        }
        
        _currentValidAnchor = myOriginalCell;
        _dragging = true;
    }

    private void OnMouseDrag()
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
                bool allFit = true;

                // Validate if the entire shape fits relative to the new anchor cell
                foreach (var obj in GetGroupObjects())
                {
                    var snap = obj.GetComponent<DraggableSnap>();
                    
                    HexCell target = _gridManager.GetCellWithOffset(anchorNearest, _peerAxialOffsets[snap]);
                    if (target == null || target.occupant != null)
                    {
                        allFit = false;
                        break;
                    }
                }

                if (allFit)
                {
                    _currentValidAnchor = anchorNearest;
                }

                // Snap visuals to the most recent valid cells
                if (_currentValidAnchor != null)
                {
                    foreach (var obj in GetGroupObjects())
                    {
                        var snap = obj.GetComponent<DraggableSnap>();
                        
                        HexCell target = _gridManager.GetCellWithOffset(_currentValidAnchor, _peerAxialOffsets[snap]);
                        float peerElevatedY = _peerOriginalPositions[snap].y + dragElevation;
                        
                        obj.transform.position = new Vector3(target.center.x, peerElevatedY, target.center.z);
                    }
                }
            }
        }
    }
    private void OnMouseUp()
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
                
                // FIX: Use target.center.y instead of _peerOriginalPositions[snap].y
                // This forces the object to snap perfectly to the floor height of the hex cell.
                obj.transform.position = new Vector3(
                    target.center.x, 
                    target.center.y, 
                    target.center.z);
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
        }
    }

    private void ClearDragState()
    {
        _peerAxialOffsets.Clear();
        _peerOriginalCells.Clear();
        _peerOriginalPositions.Clear();
        _currentValidAnchor = null;
    }
}
