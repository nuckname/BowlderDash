using UnityEngine;

/// <summary>
/// Add this to any spawned object (with a Collider) to allow click-drag and snap-to-nearest-hex on release.
/// Works with HexGridSnapper in the scene. On pick-up the object temporarily releases its occupied cell.
/// On drop it attempts to reserve the nearest cell; if that fails it returns to its original cell/position.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DraggableSnap : MonoBehaviour
{
    // private static BowlingModeSwitcher cachedModeSwitcher; // REMOVED

    private Camera cam;
    private bool dragging = false;
    private Vector3 offset;

    private Vector3 originalPosition;
    private HexGridSnapper.Cell originalCell;
    private HexGridSnapper grid;

    private Collider[] colliders;
    private bool[] originalColliderEnabled;
    private bool[] originalColliderIsTrigger;

    private void Start()
    {
        cam = Camera.main;
        grid = FindObjectOfType<HexGridSnapper>();
        CachePhysicsComponents();
    }

    private void CachePhysicsComponents()
    {
        colliders = GetComponentsInChildren<Collider>(true);
        originalColliderEnabled = new bool[colliders.Length];
        originalColliderIsTrigger = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            originalColliderEnabled[i] = colliders[i].enabled;
            originalColliderIsTrigger[i] = colliders[i].isTrigger;
        }
    }

    private void OnMouseDown()
    {
        // Only allow grabbing in placement mode when a mode switcher is actually present.
        // Scenes still being wired up can fall back to unrestricted dragging.
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.GetCurrentState() != GameState.PinPlacement)
                return;
        }

        if (cam == null) cam = Camera.main;
        // ensure we have a reference to the grid (in case it was inactive at Start)
        if (grid == null) grid = FindObjectOfType<HexGridSnapper>();
        originalPosition = transform.position;
        if (grid != null)
        {
            originalCell = grid.GetCellByOccupant(transform);
            // release it while dragging
            grid.ReleaseCell(transform);
        }

        // make colliders non-blocking while dragging by setting them as triggers
        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].isTrigger = true;
                }
            }
        }

        // Use a horizontal plane at the object's current Y to pick a point and compute offset.
        Plane plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);
            offset = transform.position - worldPoint;
        }
        dragging = true;
    }

    private void OnMouseDrag()
    {
        if (!dragging || cam == null) return;
        if (grid == null) grid = FindObjectOfType<HexGridSnapper>();
        // Project mouse to the horizontal plane and move along XZ keeping original Y.
        Plane plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPoint = ray.GetPoint(enter);
            transform.position = worldPoint + offset;
        }
    }

    private void OnMouseUp()
    {
        dragging = false;

        Pin pin = GetComponent<Pin>();
        if (PinHandInventory.Instance != null && pin != null && PinHandInventory.Instance.TryReturnPinToInventory(pin, Input.mousePosition))
        {
            RestoreColliders();
            return;
        }

        if (grid == null) grid = FindObjectOfType<HexGridSnapper>();
        if (grid == null)
        {
            // no grid: just stop dragging and restore rigidbody & colliders
            RestoreColliders();
            return;
        }

        var nearest = grid.GetNearestCell(transform.position);
        bool placed = false;
        if (nearest != null)
        {
            placed = grid.TryReserveCell(nearest, transform);
            if (placed)
            {
                transform.position = nearest.center;
            }
        }

        if (!placed)
        {
            // try to re-reserve original cell if any
            if (originalCell != null)
            {
                bool ok = grid.TryReserveCell(originalCell, transform);
                if (ok)
                {
                    transform.position = originalCell.center;
                    placed = true;
                }
            }
        }

        if (!placed)
        {
            // fallback to original position
            transform.position = originalPosition;
        }

        // restore colliders
        RestoreColliders();
    }

    private void RestoreColliders()
    {
        if (colliders == null) return;
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null) continue;
            colliders[i].isTrigger = originalColliderIsTrigger[i];
            colliders[i].enabled = originalColliderEnabled[i];
        }
    }
}
