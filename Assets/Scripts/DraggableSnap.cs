using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class DraggableSnap : MonoBehaviour
{
    [Header("Drag Settings")]
    public float dragElevation = 0.5f;
    
    [Tooltip("Must match the playAreaThresholdY from CardMovement!")]
    public float playAreaThresholdY = 500f; 

    private Camera _cam;
    private bool _dragging = false;
    private Vector3 _offset;

    private GridOccupancy _gridOccupancy;
    private HexGrid _hexGrid;
    private GridManager _gridManager;
    private WorldObjectToCard _worldObjectToCard;

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
        
        _worldObjectToCard = GetComponent<WorldObjectToCard>();
    }

    private void OnMouseDown()
    {
        if (_worldObjectToCard.GroupPeers == null || _worldObjectToCard.GroupPeers.Count == 0) return;

        _peerAxialOffsets.Clear();
        _peerOriginalCells.Clear();
        _peerOriginalPositions.Clear();

        HexCell myOriginalCell = _gridOccupancy.GetCellByOccupant(transform);
        Vector2Int myAxial = _gridManager.GetAxialCoordinate(myOriginalCell);

        // Gather and release all peers in the group
        foreach (var peerObj in _worldObjectToCard.GroupPeers)
        {
            if (peerObj == null) continue;
            var peerSnap = peerObj.GetComponent<DraggableSnap>();
            
            HexCell peerCell = _gridOccupancy.GetCellByOccupant(peerObj.transform);
            
            _peerOriginalCells[peerSnap] = peerCell;
            _peerOriginalPositions[peerSnap] = peerObj.transform.position;
            
            Vector2Int peerAxial = _gridManager.GetAxialCoordinate(peerCell);
            _peerAxialOffsets[peerSnap] = peerAxial - myAxial;

            if (peerCell != null)
                _gridOccupancy.ReleaseCell(peerObj.transform);
  
            float elevatedY = _peerOriginalPositions[peerSnap].y + dragElevation;
            peerObj.transform.position = new Vector3(peerObj.transform.position.x, elevatedY, peerObj.transform.position.z);
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
        
        if (Input.mousePosition.y < playAreaThresholdY)
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
                foreach (var peerObj in _worldObjectToCard.GroupPeers)
                {
                    if (peerObj == null) continue;
                    var peerSnap = peerObj.GetComponent<DraggableSnap>();
                    
                    HexCell target = _gridManager.GetCellWithOffset(anchorNearest, _peerAxialOffsets[peerSnap]);
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
                    foreach (var peerObj in _worldObjectToCard.GroupPeers)
                    {
                        if (peerObj == null) continue;
                        var peerSnap = peerObj.GetComponent<DraggableSnap>();
                        
                        HexCell target = _gridManager.GetCellWithOffset(_currentValidAnchor, _peerAxialOffsets[peerSnap]);
                        float peerElevatedY = _peerOriginalPositions[peerSnap].y + dragElevation;
                        
                        peerObj.transform.position = new Vector3(target.center.x, peerElevatedY, target.center.z);
                    }
                }
            }
        }
    }
    
    private void OnMouseUp()
    {
        if (!_dragging) return; 
        _dragging = false;

        HexCell finalAnchor = _hexGrid.GetNearestCell(transform.position);
        bool allFit = true;
        List<HexCell> finalCells = new List<HexCell>();

        if (finalAnchor != null)
        {
            foreach (var peerObj in _worldObjectToCard.GroupPeers)
            {
                var peerSnap = peerObj.GetComponent<DraggableSnap>();
                HexCell target = _gridManager.GetCellWithOffset(finalAnchor, _peerAxialOffsets[peerSnap]);
                
                if (target == null || target.occupant != null)
                {
                    allFit = false;
                    break;
                }
                finalCells.Add(target);
            }
        }
        else
        {
            allFit = false;
        }

        if (allFit)
        {
            // Reserve and drop into new spots
            int idx = 0;
            foreach (var peerObj in _worldObjectToCard.GroupPeers)
            {
                var peerSnap = peerObj.GetComponent<DraggableSnap>();
                _gridOccupancy.TryReserveCell(finalCells[idx], peerObj.transform);
                
                peerObj.transform.position = new Vector3(
                    finalCells[idx].center.x, 
                    _peerOriginalPositions[peerSnap].y, 
                    finalCells[idx].center.z);
                
                idx++;
            }
        }
        else
        {
            // Fallback: Return group to original position
            foreach (var peerObj in _worldObjectToCard.GroupPeers)
            {
                var peerSnap = peerObj.GetComponent<DraggableSnap>();
                
                if (_peerOriginalCells[peerSnap] != null)
                {
                    _gridOccupancy.TryReserveCell(_peerOriginalCells[peerSnap], peerObj.transform);
                }
                peerObj.transform.position = _peerOriginalPositions[peerSnap];
            }
        }
    }
}