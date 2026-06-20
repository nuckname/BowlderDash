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
    
    // We need to hide the 3D mesh when hovering over the hand area
    private Renderer[] _renderers;

    private void Awake()
    {
        _cam = Camera.main;
        _grid = GameObject.FindGameObjectWithTag("HexManager").GetComponent<HexGridSnapper>();
        _worldObjectToCard = GetComponent<WorldObjectToCard>();
        _renderers = GetComponentsInChildren<Renderer>();
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
        
        print("y:" + Input.mousePosition.y);
        // Check exact same screen threshold as CardMovement.cs
        bool isMouseInHandArea = Input.mousePosition.y < playAreaThresholdY;
        
        // Toggle 3D visibility based on where the mouse is
        ToggleRenderers(!isMouseInHandArea);

        float elevatedY = _originalPosition.y + dragElevation;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, elevatedY, 0f));
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);
            Vector3 targetPos = worldPoint + _offset;

            if (isMouseInHandArea)
            {
                // Move freely without grid snapping so it follows the mouse smoothly into the UI
                transform.position = new Vector3(targetPos.x, elevatedY, targetPos.z);
            }
            else
            {
                // Snap to Grid when in the Play Area
                HexGridSnapper.Cell nearestCell = _grid.GetNearestCell(targetPos);
                if (nearestCell != null)
                {
                    transform.position = new Vector3(nearestCell.center.x, elevatedY, nearestCell.center.z);
                }
            }
        }
    }

    private void OnMouseUp()
    {
        _dragging = false;
        
        // Ensure renderers are back on just in case
        ToggleRenderers(true);

        // Did we drop it in the hand area?
        if (Input.mousePosition.y < playAreaThresholdY)
        {
            if (_worldObjectToCard != null)
            {
                _worldObjectToCard.ReturnToHand(); 
            }
            return; 
        }

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

    // Helper method to toggle the 3D pin visuals on/off
    private void ToggleRenderers(bool state)
    {
        foreach (var r in _renderers)
        {
            r.enabled = state;
        }
    }
}