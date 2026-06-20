using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DraggableSnap : MonoBehaviour
{
    [Header("Drag Settings")]
    [Tooltip("How much the pin lifts up on the Y axis while dragging.")]
    public float dragElevation = 0.5f;

    private Camera _cam;
    private bool _dragging = false;
    private Vector3 _offset;

    private Vector3 _originalPosition;
    private HexGridSnapper.Cell _originalCell;
    private HexGridSnapper _grid;
    private Pin _pin;

    private void Awake()
    {
        _pin = GetComponent<Pin>();
        _cam = Camera.main;
        _grid = GameObject.FindGameObjectWithTag("HexManager").GetComponent<HexGridSnapper>();
    }

    private void OnMouseDown()
    {
        _originalPosition = transform.position;
 
        _originalCell = _grid.GetCellByOccupant(transform);
        // Release it while dragging so it doesn't block itself
        _grid.ReleaseCell(transform);
  
        // Calculate the elevated height for dragging
        float elevatedY = _originalPosition.y + dragElevation;
        
        // Immediately pop the pin up
        transform.position = new Vector3(transform.position.x, elevatedY, transform.position.z);

        // Create a horizontal plane at the newly elevated Y level
        Plane plane = new Plane(Vector3.up, new Vector3(0f, elevatedY, 0f));
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);
            _offset = transform.position - worldPoint;
        }
        
        _dragging = true;
    }

    private void OnMouseDrag()
    {
        if (!_dragging) return;
        
        // Ensure we raycast against the same elevated plane to keep movement strictly horizontal
        float elevatedY = _originalPosition.y + dragElevation;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, elevatedY, 0f));
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);
            Vector3 targetPos = worldPoint + _offset;
         
            // Constantly check if the cell we are hovering over is empty
            var nearestCell = _grid.GetNearestCell(targetPos);

            // Snap visually to the empty cell's X/Z, but maintain the dragged Y elevation
            transform.position = new Vector3(nearestCell.center.x, elevatedY, nearestCell.center.z);
        
        }
    }

    private void OnMouseUp()
    {
        _dragging = false;

        var nearest = _grid.GetNearestCell(transform.position);
        bool placed = false;
        
        if (nearest != null)
        {
            placed = _grid.TryReserveCell(nearest, transform);
            if (placed)
            {
                // Drops the pin back down to the exact cell center
                transform.position = nearest.center;
            }
        }

        if (!placed)
        {
            // Try to re-reserve original cell if it was taken while we held it
            if (_originalCell != null)
            {
                bool ok = _grid.TryReserveCell(_originalCell, transform);
                if (ok)
                {
                    transform.position = _originalCell.center;
                    placed = true;
                }
            }
        }
    }
}