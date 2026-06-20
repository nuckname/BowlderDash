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
    private HexGridSnapper.Cell _originalCell;
    private HexGridSnapper _grid;
    private WorldObjectToCard _worldObjectToCard;

    private void Awake()
    {
        _cam = Camera.main;
        _grid = GameObject.FindGameObjectWithTag("HexManager").GetComponent<HexGridSnapper>();
        _worldObjectToCard = GetComponent<WorldObjectToCard>();
    }

    private void OnMouseDown()
    {
        _originalPosition = transform.position;
        _originalCell = _grid.GetCellByOccupant(transform);
        
        if (_originalCell != null)
            _grid.ReleaseCell(transform);
  
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
        
        // Check exact same screen threshold as CardMovement.cs
        // INSTANT SWAP: If we cross the line, immediately swap to the UI card
        if (Input.mousePosition.y < playAreaThresholdY)
        {
            _dragging = false;
            
            if (_worldObjectToCard != null)
            {
                // This destroys the 3D object and tells the UI card to take over the mouse drag
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

            // Snap to Grid when in the Play Area
            HexGridSnapper.Cell nearestCell = _grid.GetNearestCell(targetPos);
            if (nearestCell != null)
            {
                transform.position = new Vector3(nearestCell.center.x, elevatedY, nearestCell.center.z);
            }
        }
    }

    private void OnMouseUp()
    {
        // Safety check: if the object was already converted to a card, abort!
        if (!_dragging) return; 

        _dragging = false;

        // Normal placement attempt in Play Area
        var nearest = _grid.GetNearestCell(transform.position);
        bool placed = false;
        
        if (nearest != null)
        {
            placed = _grid.TryReserveCell(nearest, transform);
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
                _grid.TryReserveCell(_originalCell, transform);
            }
        }
    }
}