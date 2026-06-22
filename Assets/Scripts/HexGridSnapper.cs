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

    [Header("Runtime Visuals")]
    [Tooltip("Draw the grid for the player while the game is running.")]
    public bool showGridInPlay = true;
    [Tooltip("Optional material for runtime line renderers. Defaults to Sprites/Default if left empty.")]
    public Material runtimeLineMaterial;
    public Color freeCellColor = new Color(0f, 0.6f, 1f, 0.6f);
    public Color occupiedCellColor = new Color(0f, 1f, 0f, 0.6f);
    public float runtimeLineWidth = 0.08f;
    public float ticketLineWidthOffset = 0.1f;
    public float runtimeLineHeightOffset = 0.05f;

    [Header("Tickets")]
    public bool enableFrameTickets = true;
    public int minTicketsPerFrame = 2;
    public int maxTicketsPerFrame = 4;
    public int ticketValue = 1;
    public float ticketHeightOffset = 0.08f;
    [Tooltip("ticket cells prevent z fighting")]
    public float ticketCellLineYOffset = 0.1f;
    public float ticketVisualScale = 0.18f;
    public Color ticketCellColor = new Color(1f, 0.82f, 0.18f, 0.92f);
    public GameObject ticketVisualPrefab;

    // internal cell representation
    public class Cell
    {
        public int col;
        public int row;
        public Vector3 center;
        public Transform occupant;
    }

    private class TicketCell
    {
        public Cell cell;
        public int value;
        public GameObject visual;
    }

    private List<Cell> cells = new List<Cell>();
    private readonly List<LineRenderer> runtimeGridLines = new List<LineRenderer>();
    private readonly List<TicketCell> activeTickets = new List<TicketCell>();
    private Material generatedRuntimeMaterial;
    private bool runtimeGridVisible;

    private void Start()
    {
        if (!Application.isPlaying) return;
        GenerateGrid();
        if (snapChildrenOnStart)
        {
            SnapChildrenToGrid(childrenParent != null ? childrenParent : this.transform);
        }

        RebuildRuntimeGrid();
        ApplyRuntimeGridVisibility(ShouldShowRuntimeGrid());
    }

    private void Update()
    {
        if (!Application.isPlaying || !showGridInPlay) return;

        if (runtimeGridLines.Count != cells.Count)
        {
            RebuildRuntimeGrid();
        }

        ApplyRuntimeGridVisibility(ShouldShowRuntimeGrid());
        UpdateRuntimeGridColors();
        UpdateTicketVisuals();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;

        if (cells == null || cells.Count == 0)
        {
            GenerateGrid();
        }

        if (runtimeGridLines.Count == 0)
        {
            RebuildRuntimeGrid();
        }

        ApplyRuntimeGridVisibility(ShouldShowRuntimeGrid());
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        ApplyRuntimeGridVisibility(false);
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

        if (Application.isPlaying)
        {
            RebuildRuntimeGrid();
            ApplyRuntimeGridVisibility(ShouldShowRuntimeGrid());
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

    public Cell GetCell(int col, int row)
    {
        if (cells == null || cells.Count == 0)
        {
            GenerateGrid();
        }

        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];
            if (cell.col == col && cell.row == row)
            {
                return cell;
            }
        }

        return null;
    }

    public List<Cell> GetCellsSnapshot()
    {
        if (cells == null || cells.Count == 0)
        {
            GenerateGrid();
        }

        return new List<Cell>(cells);
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

    public void ClearAllOccupants()
    {
        for (int i = 0; i < cells.Count; i++)
        {
            cells[i].occupant = null;
        }
    }

    public void SyncPinsToGrid(IEnumerable<Pin> pins)
    {
        if (pins == null)
        {
            return;
        }

        if (cells == null || cells.Count == 0)
        {
            GenerateGrid();
        }

        ClearAllOccupants();

        foreach (Pin pin in pins)
        {
            if (pin == null || !pin.gameObject.activeInHierarchy)
            {
                continue;
            }

            Cell nearestCell = GetNearestCell(pin.transform.position);
            if (nearestCell == null)
            {
                continue;
            }

            TryReserveCell(nearestCell, pin.transform);
            pin.transform.position = nearestCell.center;
        }
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

    public void SpawnTicketsForFrame()
    {
        ClearTickets();

        if (!Application.isPlaying || !enableFrameTickets)
        {
            return;
        }

        if (cells == null || cells.Count == 0)
        {
            GenerateGrid();
        }

        if (cells.Count == 0)
        {
            return;
        }

        int clampedMin = Mathf.Clamp(minTicketsPerFrame, 0, cells.Count);
        int clampedMax = Mathf.Clamp(maxTicketsPerFrame, clampedMin, cells.Count);
        int ticketCount = Random.Range(clampedMin, clampedMax + 1);

        List<int> indices = new List<int>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
        {
            indices.Add(i);
        }

        for (int i = 0; i < indices.Count; i++)
        {
            int swapIndex = Random.Range(i, indices.Count);
            int temp = indices[i];
            indices[i] = indices[swapIndex];
            indices[swapIndex] = temp;
        }

        for (int i = 0; i < ticketCount; i++)
        {
            Cell cell = cells[indices[i]];
            TicketCell ticket = new TicketCell
            {
                cell = cell,
                value = Mathf.Max(1, ticketValue),
                visual = CreateTicketVisual(cell)
            };

            activeTickets.Add(ticket);
        }

        UpdateRuntimeGridColors();
        RefreshTicketCellLinePositions();
    }

    public bool TryCollectTicketForPin(Pin pin, out int collectedValue)
    {
        collectedValue = 0;
        if (pin == null)
        {
            return false;
        }

        Cell occupantCell = GetCellByOccupant(pin.transform);
        if (occupantCell == null)
        {
            occupantCell = GetNearestCell(pin.transform.position);
        }

        if (occupantCell == null)
        {
            return false;
        }

        TicketCell ticket = GetTicketAtCell(occupantCell);
        if (ticket == null)
        {
            return false;
        }

        collectedValue = ticket.value;
        if (ticket.visual != null)
        {
            Destroy(ticket.visual);
        }

        activeTickets.Remove(ticket);
        UpdateRuntimeGridColors();
        RefreshTicketCellLinePositions();
        return true;
    }

    public bool AreAllPlacedPinsCleared()
    {
        bool foundPlacedPin = false;

        for (int i = 0; i < cells.Count; i++)
        {
            Transform occupant = cells[i].occupant;
            if (occupant == null)
            {
                continue;
            }

            Pin pin = occupant.GetComponent<Pin>();
            if (pin == null)
            {
                continue;
            }


            foundPlacedPin = true;

            if (!occupant.gameObject.activeInHierarchy)
            {
                continue;
            }

            Debug.LogError("Need to check for fallen pin here");
            /*
            if (!pin.IsFallen())
            {
                return false;
            }
            */
        }

        return foundPlacedPin;
    }

    public void ClearTickets()
    {
        for (int i = 0; i < activeTickets.Count; i++)
        {
            if (activeTickets[i].visual != null)
            {
                Destroy(activeTickets[i].visual);
            }
        }

        activeTickets.Clear();
        UpdateRuntimeGridColors();
        RefreshTicketCellLinePositions();
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

    private void OnDestroy()
    {
        ClearTickets();
        ClearRuntimeGrid();
        if (generatedRuntimeMaterial != null)
        {
            Destroy(generatedRuntimeMaterial);
            generatedRuntimeMaterial = null;
        }
    }

    private void RebuildRuntimeGrid()
    {
        ClearRuntimeGrid();

        if (!Application.isPlaying || !showGridInPlay || cells.Count == 0) return;

        Material lineMat = GetRuntimeLineMaterial();
        if (lineMat == null) return;

        for (int i = 0; i < cells.Count; i++)
        {
            Cell cell = cells[i];
            GameObject lineObject = new GameObject($"RuntimeHex_{cell.row}_{cell.col}");
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 6;
            line.widthMultiplier = runtimeLineWidth;
            line.material = lineMat;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.textureMode = LineTextureMode.Stretch;

            Vector3[] points = GetHexVertices(cell.center + Vector3.up * runtimeLineHeightOffset, hexSize);
            line.SetPositions(points);

            runtimeGridLines.Add(line);
        }

        UpdateRuntimeGridColors();
        RefreshTicketCellLinePositions();
    }

    private void ClearRuntimeGrid()
    {
        for (int i = 0; i < runtimeGridLines.Count; i++)
        {
            if (runtimeGridLines[i] == null) continue;
            Destroy(runtimeGridLines[i].gameObject);
        }

        runtimeGridLines.Clear();
    }

    private void ApplyRuntimeGridVisibility(bool visible)
    {
        bool shouldBeVisible = showGridInPlay && visible;
        if (runtimeGridVisible == shouldBeVisible) return;

        runtimeGridVisible = shouldBeVisible;
        for (int i = 0; i < runtimeGridLines.Count; i++)
        {
            if (runtimeGridLines[i] != null)
            {
                runtimeGridLines[i].enabled = runtimeGridVisible;
            }
        }
    }

    private bool ShouldShowRuntimeGrid()
    {
        if (!Application.isPlaying) return showGizmos;

        return showGridInPlay
            && showGizmos
            && isActiveAndEnabled
            && gameObject.activeInHierarchy
            && (GameManager.Instance != null && GameManager.Instance.GetCurrentState() == GameState.PinPlacement);
    }

    private void UpdateRuntimeGridColors()
    {
        for (int i = 0; i < runtimeGridLines.Count && i < cells.Count; i++)
        {
            LineRenderer line = runtimeGridLines[i];
            if (line == null) continue;

            Color lineColor = GetCellColor(cells[i]);
            line.startColor = lineColor;
            line.endColor = lineColor;
        }
    }

    private void RefreshTicketCellLinePositions()
    {
        for (int i = 0; i < runtimeGridLines.Count && i < cells.Count; i++)
        {
            LineRenderer line = runtimeGridLines[i];
            if (line == null) continue;

            Vector3 center = cells[i].center + Vector3.up * runtimeLineHeightOffset;
            if (GetTicketAtCell(cells[i]) != null)
            {
                center += Vector3.up * ticketCellLineYOffset;
                line.widthMultiplier = runtimeLineWidth + ticketLineWidthOffset;
            }

            Vector3[] points = GetHexVertices(center, hexSize);
            line.SetPositions(points);
            
        }
    }

    private Color GetCellColor(Cell cell)
    {
        if (GetTicketAtCell(cell) != null)
        {
            return ticketCellColor;
        }

        return cell.occupant != null ? occupiedCellColor : freeCellColor;
    }

    private Material GetRuntimeLineMaterial()
    {
        if (runtimeLineMaterial != null) return runtimeLineMaterial;
        if (generatedRuntimeMaterial != null) return generatedRuntimeMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        generatedRuntimeMaterial = new Material(shader);
        generatedRuntimeMaterial.name = "HexGridRuntimeMaterial";
        return generatedRuntimeMaterial;
    }

    private void DrawHex(Vector3 center, float size)
    {
        Vector3[] verts = GetHexVertices(center, size);
        for (int i = 0; i < 6; i++)
        {
            Gizmos.DrawLine(verts[i], verts[(i + 1) % 6]);
        }
        Gizmos.DrawSphere(center, Mathf.Max(0.02f, size * 0.05f));
    }

    private Vector3[] GetHexVertices(Vector3 center, float size)
    {
        Vector3[] verts = new Vector3[6];
        // pointy-top hex: start angle 30 degrees so a point faces forward on Z
        for (int i = 0; i < 6; i++)
        {
            float angleDeg = 30f + 60f * i;
            float rad = Mathf.Deg2Rad * angleDeg;
            verts[i] = center + new Vector3(size * Mathf.Cos(rad), 0f, size * Mathf.Sin(rad));
        }

        return verts;
    }

    private TicketCell GetTicketAtCell(Cell cell)
    {
        if (cell == null)
        {
            return null;
        }

        for (int i = 0; i < activeTickets.Count; i++)
        {
            TicketCell ticket = activeTickets[i];
            if (ticket.cell.col == cell.col && ticket.cell.row == cell.row)
            {
                return ticket;
            }
        }

        return null;
    }

    private GameObject CreateTicketVisual(Cell cell)
    {
        if (cell == null)
        {
            return null;
        }

        GameObject visual;
        if (ticketVisualPrefab != null)
        {
            visual = Instantiate(ticketVisualPrefab, transform);
        }
        else
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Collider collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = ticketCellColor;
            }
        }

        visual.name = $"Ticket_{cell.row}_{cell.col}";
        visual.transform.SetParent(transform, true);
        visual.transform.position = cell.center + Vector3.up * ticketHeightOffset;
        // visual.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        // visual.transform.localScale = new Vector3(ticketVisualScale, ticketVisualScale * 0.1f, ticketVisualScale);
        return visual;
    }

    private void UpdateTicketVisuals()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        for (int i = 0; i < activeTickets.Count; i++)
        {
            TicketCell ticket = activeTickets[i];
            if (ticket.visual == null)
            {
                continue;
            }

            float bob = Mathf.Sin((Time.time * 3f) + i) * 0.025f;
            ticket.visual.transform.position = ticket.cell.center + Vector3.up * (ticketHeightOffset + bob);
            ticket.visual.transform.Rotate(Vector3.up, 60f * Time.deltaTime, Space.World);
        }
    }
}
