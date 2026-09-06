using UnityEngine;

// AI
// https://share.gemini.google/FIMMcnyyBfOg
[RequireComponent(typeof(HexGrid))]
public class HexGridExpander : MonoBehaviour
{
    private HexGrid grid;
    private HexGridVisuals visuals;

    [Header("Grid Limits")]
    public int leftSideExpandingLimit;
    public int rightSideExpandingLimit;
    public int upSideExpandingLimit;
    public int downSideExpandingLimit;
    
    private void Awake()
    {
        grid = GetComponent<HexGrid>();
        visuals = GetComponent<HexGridVisuals>();
    }

    private void Update()
    {
        if (TryGetExpansionData(out Vector2Int newAxial, out Vector3 newCenter))
        {
            if (visuals != null) visuals.ShowPreview(newCenter);

            if (Input.GetMouseButtonDown(0))
            {
                AddCell(newAxial, newCenter);
            }
        }
        else
        {
            if (visuals != null) visuals.HidePreview();
        }
    }

    private bool TryGetExpansionData(out Vector2Int newAxial, out Vector3 newCenter)
    {
        newAxial = Vector2Int.zero;
        newCenter = Vector3.zero;

        float planeY = grid.transform.position.y + grid.originOffset.y;
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
}