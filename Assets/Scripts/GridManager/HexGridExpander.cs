using UnityEngine;

// AI
// https://share.gemini.google/FIMMcnyyBfOg
[RequireComponent(typeof(HexGrid))]
public class HexGridExpander : MonoBehaviour
{
    private HexGrid grid;
    private HexGridVisuals visuals;

    public ShopUIManager shopManager;
    
    [Header("Grid Limits")]
    public int leftSideExpandingLimit;
    public int rightSideExpandingLimit;
    public int upSideExpandingLimit;
    public int downSideExpandingLimit;
    
    [Header("Interaction Limits")]
    public float maxInteractionDistance = 6f;

    [Header("Hex Expansion Audio")]
    [SerializeField] private SoundCueSO hoverCue;
    [SerializeField] private SoundCueSO purchaseCue;
    [SerializeField] private SoundCueSO deniedCue;
    private bool hasPreviewTarget;
    private Vector2Int previewAxial;
    
    private void Awake()
    {
        grid = GetComponent<HexGrid>();
        visuals = GetComponent<HexGridVisuals>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ClearPreview();
            if (shopManager != null) shopManager.ToggleHexGridExpander(false);
            return;
        }

        if ((shopManager != null && !shopManager.IsHexGridExpansionActive) ||
            PinInteractionAudio.IsPointerOverUI())
        {
            ClearPreview();
            return;
        }

        bool hasValidTarget = TryGetExpansionData(out Vector2Int newAxial, out Vector3 newCenter);
        if (hasValidTarget) ShowPreview(newAxial, newCenter);
        else ClearPreview();

        if (!Input.GetMouseButtonDown(0)) return;
        // Reject an invalid or newly occupied target before spending tickets.
        if (!hasValidTarget || !CanAddCell(newAxial))
        {
            ClearPreview();
            PlayOutcome(deniedCue);
            return;
        }

        if (shopManager != null && !shopManager.TryBuyGridExpansion())
        {
            PlayOutcome(deniedCue);
            Debug.LogWarning("Not enough tickets");
            return;
        }

        AddCell(newAxial, newCenter);
        ClearPreview();
        PlayOutcome(purchaseCue);
    }

    // Axial identity stays stable as the pointer moves within one preview target.
    private void ShowPreview(Vector2Int axial, Vector3 center)
    {
        if (visuals != null) visuals.ShowPreview(center);
        if (hasPreviewTarget && previewAxial == axial) return;
        hasPreviewTarget = true;
        previewAxial = axial;
        if (visuals != null && CanPlayAudio && hoverCue != null && hoverCue.isUI)
            GameAudio.Instance?.PlayUIHover(hoverCue);
    }

    private void ClearPreview()
    {
        hasPreviewTarget = false;
        if (visuals != null) visuals.HidePreview();
    }

    // The no-shop fallback can still add cells, but shop feedback needs an active owner.
    private bool CanPlayAudio => isActiveAndEnabled && shopManager != null && shopManager.IsHexGridExpansionActive;

    private void PlayOutcome(SoundCueSO cue)
    {
        if (CanPlayAudio && cue != null && cue.isUI) GameAudio.Instance?.Play(cue);
    }

    private bool CanAddCell(Vector2Int axial)
    {
        if (grid == null) return false;
        int col = axial.x + (axial.y - (axial.y & 1)) / 2;
        int row = axial.y;
        return col >= leftSideExpandingLimit && col <= rightSideExpandingLimit &&
            row >= upSideExpandingLimit && row <= downSideExpandingLimit && grid.GetCellAt(col, row) == null;
    }

    private bool TryGetExpansionData(out Vector2Int newAxial, out Vector3 newCenter)
    {
        newAxial = Vector2Int.zero;
        newCenter = Vector3.zero;

        if (grid == null || Camera.main == null) return false;

        float planeY = grid.transform.position.y;
        if (grid.Cells != null && grid.Cells.Count > 0)
        {
            planeY = grid.Cells[0].center.y;
        }

        if (visuals != null) 
        {
            planeY += visuals.runtimeLineHeightOffset;
        }

        Plane plane = new Plane(Vector3.up, new Vector3(0, planeY, 0));
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!plane.Raycast(ray, out float distance)) return false;

        Vector3 hitPoint = ray.GetPoint(distance);
        HexCell closestCell = grid.GetNearestCell(hitPoint);
        if (closestCell == null) return false;

        float distanceFromBoard = Vector2.Distance(new Vector2(hitPoint.x, hitPoint.z), new Vector2(closestCell.center.x, closestCell.center.z));
        if (distanceFromBoard > maxInteractionDistance) return false;

        Vector3 dir = hitPoint - closestCell.center;
        dir.y = 0;

        if (dir.sqrMagnitude < 0.001f) return false;

        float angle = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        int side = Mathf.RoundToInt(angle / 60f) % 6;
        Vector2Int axialOffset = Vector2Int.zero;

        switch (side)
        {
            case 0: axialOffset = new Vector2Int(1, 0); break;   
            case 1: axialOffset = new Vector2Int(1, -1); break;  
            case 2: axialOffset = new Vector2Int(0, -1); break;  
            case 3: axialOffset = new Vector2Int(-1, 0); break;  
            case 4: axialOffset = new Vector2Int(-1, 1); break;  
            case 5: axialOffset = new Vector2Int(0, 1); break;   
        }

        HexCell existing = grid.GetCellWithOffset(closestCell, axialOffset);
        if (existing != null) return false;

        Vector2Int baseAxial = grid.GetAxialCoordinate(closestCell);
        newAxial = baseAxial + axialOffset;

        int targetCol = newAxial.x + (newAxial.y - (newAxial.y & 1)) / 2;
        int targetRow = newAxial.y;

        if (targetCol < leftSideExpandingLimit || targetCol > rightSideExpandingLimit || targetRow < upSideExpandingLimit || targetRow > downSideExpandingLimit)
        {
            return false;
        }

        newCenter = closestCell.center + grid.GetWorldOffsetFromAxial(axialOffset);

        return true;
    }
    
    private void AddCell(Vector2Int newAxial, Vector3 newCenter)
    {
        int targetCol = newAxial.x + (newAxial.y - (newAxial.y & 1)) / 2;
        int targetRow = newAxial.y;

        HexCell newCell = new HexCell(targetCol, targetRow, newCenter);
        grid.AddCell(newCell);

        if (visuals != null)
        {
            visuals.RebuildVisuals();
            visuals.RefreshColors();
        }
    }
    
    private void OnDisable() => ClearPreview();
}
