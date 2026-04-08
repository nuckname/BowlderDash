using System.Collections.Generic;
using UnityEngine;


[ExecuteInEditMode]
public class HexGridSnapper : MonoBehaviour
{
    [Header("Grid")]
    public int columns = 10;
    public int rows = 5;
    [Tooltip("Radius of hex (center to corner) in world units")] 
    public float hexSize = 0.5f;
    public Vector3 originOffset = Vector3.zero;

    [Header("Spawning")]
    public Transform spawnPrefab; // prefab to instantiate at each cell (optional)
    public bool clearBeforeSpawn = true;
    [Tooltip("If set, snap this transform's children to the grid at Start. If null, uses this.transform.")]
    public Transform childrenParent;
    public bool snapChildrenOnStart = false;

    [Header("Behavior")]
    public bool allowOnePerCell = true;
    public bool showGizmos = true;

    // internal cell representation
    public class Cell
    {
        public int col;
        public int row;
        public Vector3 center;
        public Transform occupant;
    }

    private List<Cell> cells = new List<Cell>();

    private void Start()
    {
        if (!Application.isPlaying) return;
        GenerateGrid();
        if (snapChildrenOnStart)
        {
            SnapChildrenToGrid(childrenParent != null ? childrenParent : this.transform);
        }
    }

    /// <summary>
    /// Snap all children of the given parent to the nearest free grid cell. Uses TryReserveCell so occupied cells won't be double-filled.
    /// </summary>
    public void SnapChildrenToGrid(Transform parent)
    {
        if (parent == null) return;
        if (cells == null || cells.Count == 0) GenerateGrid();

        foreach (Transform child in parent)
        {
            if (child == null) continue;
            // skip the grid object itself if accidentally included
            if (child == this.transform) continue;
            // find nearest cell
            var nearest = GetNearestCell(child.position);
            if (nearest == null) continue;
            // attempt to reserve; TryReserveCell will fail if occupied and allowOnePerCell
            bool ok = TryReserveCell(nearest, child);
            if (ok)
            {
                child.position = nearest.center;
                // If this child is a Pin, update its stored start position so ResetPin returns it to the snapped location
                var pin = child.GetComponent<Pin>();
                if (pin != null)
                {
                    pin.UpdateTransform();
                }
            }
        }
    }

    [ContextMenu("Generate Grid")]
    public void GenerateGrid()
    {
        // Build cell positions in local grid-space first (centered around origin),
        // then translate so the grid is centered on the object's pivot (+ originOffset).
        cells.Clear();

        float w = Mathf.Sqrt(3f) * hexSize; // horizontal spacing between centers
        float h = 2f * hexSize; // height of a hex
        float rowStep = h * 0.75f;

        var localCenters = new List<Vector3>();

        for (int r = 0; r < rows; r++)
        {
            // cycle between n and n-1 slots per row
            int colsThisRow = Mathf.Max(1, columns - (r % 2));
            for (int c = 0; c < colsThisRow; c++)
            {
                // pointy-top odd-r horizontal layout in local XZ plane
                float x = (c + 0.5f * (r % 2)) * w;
                float z = -r * rowStep;
                localCenters.Add(new Vector3(x, 0f, z));
            }
        }

        if (localCenters.Count == 0) return;

        // compute bounds of local centers
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var p in localCenters)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.z < minZ) minZ = p.z;
            if (p.z > maxZ) maxZ = p.z;
        }

        Vector3 localCenter = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);

        // create cells shifted so grid center aligns with transform.position (+ originOffset)
        int idx = 0;
        for (int r = 0; r < rows; r++)
        {
            int colsThisRow = Mathf.Max(1, columns - (r % 2));
            for (int c = 0; c < colsThisRow; c++)
            {
                Vector3 local = localCenters[idx++];
                Vector3 worldPos = transform.position + originOffset + (local - localCenter);
                var cell = new Cell()
                {
                    col = c,
                    row = r,
                    center = worldPos,
                    occupant = null
                };
                cells.Add(cell);
            }
        }
    }

    [ContextMenu("Spawn Grid Objects")]
    public void SpawnGridObjects()
    {
        if (spawnPrefab == null) return;
        if (clearBeforeSpawn) ClearGridObjects();
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (c.occupant != null) continue; // skip already-occupied

            var inst = Instantiate(spawnPrefab, c.center, Quaternion.identity, this.transform);
            inst.name = string.Format("Cell_{0}_{1}", c.col, c.row);

            // add draggable script if not present
            if (inst.GetComponent<DraggableSnap>() == null)
            {
                inst.gameObject.AddComponent<DraggableSnap>();
            }

            c.occupant = inst;
            cells[i] = c; // update list
        }
    }

    [ContextMenu("Clear Grid Objects")]
    public void ClearGridObjects()
    {
        foreach (var c in cells)
        {
            if (c.occupant != null)
            {
                if (Application.isPlaying)
                    Destroy(c.occupant.gameObject);
                else
                    DestroyImmediate(c.occupant.gameObject);
            }
        }
        for (int i = 0; i < cells.Count; i++) cells[i].occupant = null;
    }

    /// <summary>
    /// Finds the nearest cell to world position. Returns null if no cells exist.
    /// </summary>
    public Cell GetNearestCell(Vector3 worldPos)
    {
        if (cells.Count == 0) return null;
        float best = float.MaxValue;
        Cell bestCell = null;
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            float d = (c.center - worldPos).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestCell = c;
            }
        }
        return bestCell;
    }

    /// <summary>
    /// Attempts to reserve the cell for the requester. If allowOnePerCell is true and the cell is occupied by another, fails.
    /// If the requester already occupies a different cell, that previous cell will be released.
    /// </summary>
    public bool TryReserveCell(Cell target, Transform requester)
    {
        if (target == null || requester == null) return false;

        // if already occupied by requester, ok
        if (target.occupant == requester) return true;

        if (allowOnePerCell && target.occupant != null) return false; // occupied

        // release previous cell of requester
        ReleaseCell(requester);

        // set occupant
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].col == target.col && cells[i].row == target.row)
            {
                cells[i].occupant = requester;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Releases any cell currently occupied by requester.
    /// </summary>
    public void ReleaseCell(Transform requester)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].occupant == requester)
            {
                cells[i].occupant = null;
                return;
            }
        }
    }

    /// <summary>
    /// Finds the cell currently occupied by transform, or null.
    /// </summary>
    public Cell GetCellByOccupant(Transform t)
    {
        if (t == null) return null;
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].occupant == t) return cells[i];
        }
        return null;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        if (cells == null || cells.Count == 0) GenerateGrid();
        // draw hex outlines for each cell
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            Gizmos.color = c.occupant != null ? new Color(0f, 1f, 0f, 0.6f) : new Color(0f, 0.6f, 1f, 0.6f);
            DrawHex(c.center, hexSize);
        }

        // draw bounding rectangle for the grid
        if (cells.Count > 0)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var c in cells)
            {
                if (c.center.x < minX) minX = c.center.x;
                if (c.center.x > maxX) maxX = c.center.x;
                if (c.center.z < minZ) minZ = c.center.z;
                if (c.center.z > maxZ) maxZ = c.center.z;
            }
            Vector3 bl = new Vector3(minX - hexSize, transform.position.y, minZ - hexSize);
            Vector3 br = new Vector3(maxX + hexSize, transform.position.y, minZ - hexSize);
            Vector3 tr = new Vector3(maxX + hexSize, transform.position.y, maxZ + hexSize);
            Vector3 tl = new Vector3(minX - hexSize, transform.position.y, maxZ + hexSize);
            Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl);
            Gizmos.DrawLine(tl, bl);
        }
    }

    private void DrawHex(Vector3 center, float size)
    {
        Vector3[] verts = new Vector3[6];
        // pointy-top hex: start angle 30 degrees so a point faces forward on Z
        for (int i = 0; i < 6; i++)
        {
            float angleDeg = 30f + 60f * i;
            float rad = Mathf.Deg2Rad * angleDeg;
            verts[i] = center + new Vector3(size * Mathf.Cos(rad), 0f, size * Mathf.Sin(rad));
        }
        for (int i = 0; i < 6; i++)
        {
            Gizmos.DrawLine(verts[i], verts[(i + 1) % 6]);
        }
        Gizmos.DrawSphere(center, Mathf.Max(0.02f, size * 0.05f));
    }
}
