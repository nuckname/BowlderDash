using UnityEngine;

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

    private Vector3 _originalPosition;
    private HexCell _originalCell;
    private GridOccupancy _gridOccupancy;
    private HexGrid _hexGrid;
    private WorldObjectToCard _worldObjectToCard;

    private void Awake()
    {
        _cam = Camera.main;
        // Is this better then using a string tag?
        _gridOccupancy = FindFirstObjectByType<GridOccupancy>();
        _worldObjectToCard = GetComponent<WorldObjectToCard>();
        _hexGrid = GetComponent<HexGrid>();
    }

    private void OnMouseDown()
    {
        _originalPosition = transform.position;
        _originalCell = _gridOccupancy.GetCellByOccupant(transform);
        
        if (_originalCell != null)
            _gridOccupancy.ReleaseCell(transform);
  
        float elevatedY = _originalPosition.y + dragElevation;
        transform.position = new Vector3(transform.position.x, elevatedY, transform.position.z);

        Plane plane = new Plane(Vector3.up, new Vector3(0f, elevatedY, 0f));
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        
        if (plane.Raycast(ray, out float enter))
        {
            _offset = transform.position - ray.GetPoint(enter);
        }
        
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

        float elevatedY = _originalPosition.y + dragElevation;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, elevatedY, 0f));
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);
            Vector3 targetPos = worldPoint + _offset;

            HexCell nearestCell = _hexGrid.GetNearestCell(targetPos);
            if (nearestCell != null)
            {
                transform.position = new Vector3(nearestCell.center.x, elevatedY, nearestCell.center.z);
            }
        }
    }

    private void OnMouseUp()
    {
        if (!_dragging) return; 

        _dragging = false;

        HexCell nearest = _hexGrid.GetNearestCell(transform.position);
        bool placed = false;
        
        if (nearest != null)
        {
            placed = _gridOccupancy.TryReserveCell(nearest, transform);
            if (placed)
            {
                transform.position = new Vector3(nearest.center.x, _originalPosition.y, nearest.center.z);
            }
        }
        
        // Fallback: Return to original position
        if (!placed)
        {
            transform.position = _originalPosition;
            if (_originalCell != null)
            {
                _gridOccupancy.TryReserveCell(_originalCell, transform);
            }
        }
    }
}