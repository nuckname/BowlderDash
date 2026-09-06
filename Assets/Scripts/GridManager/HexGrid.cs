using System.Collections.Generic;
using UnityEngine;

public class HexGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    public int columns = 10;
    public int rows = 5;
    public float hexSize = 0.5f;
    public Vector3 originOffset = Vector3.zero;

    private List<HexCell> cells = new List<HexCell>();
    public IReadOnlyList<HexCell> Cells => cells;

    public void GenerateGrid()
    {
        cells.Clear();
        float w = Mathf.Sqrt(3f) * hexSize; 
        float h = 2f * hexSize; 
        float rowStep = h * 0.75f;

        var localCenters = new List<Vector3>();

        for (int r = 0; r < rows; r++)
        {
            int colsThisRow = Mathf.Max(1, columns - (r % 2));
            for (int c = 0; c < colsThisRow; c++)
            {
                float x = (c + 0.5f * (r % 2)) * w;
                float z = -r * rowStep;
                localCenters.Add(new Vector3(x, 0f, z));
            }
        }

        if (localCenters.Count == 0) return;

        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var p in localCenters)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.z < minZ) minZ = p.z;
            if (p.z > maxZ) maxZ = p.z;
        }

        Vector3 localCenter = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        int idx = 0;

        for (int r = 0; r < rows; r++)
        {
            int colsThisRow = Mathf.Max(1, columns - (r % 2));
            for (int c = 0; c < colsThisRow; c++)
            {
                Vector3 worldPos = transform.position + originOffset + (localCenters[idx++] - localCenter);
                cells.Add(new HexCell(c, r, worldPos));
            }
        }
    }

    public HexCell GetNearestCell(Vector3 worldPos)
    {
        if (cells.Count == 0) return null;
        float best = float.MaxValue;
        HexCell bestCell = null;

        for (int i = 0; i < cells.Count; i++)
        {
            float d = (cells[i].center - worldPos).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestCell = cells[i];
            }
        }
        return bestCell;
    }
    
    public void AddCell(HexCell newCell)
    {
        cells.Add(newCell);
    }
    
    public Vector3 GetWorldOffsetFromAxial(Vector2Int axialOffset)
    {
        float w = Mathf.Sqrt(3f) * hexSize; 
        float h = 2f * hexSize; 
        float rowStep = h * 0.75f;

        Vector3 basisQ = new Vector3(w, 0f, 0f);
        Vector3 basisR = new Vector3(w * 0.5f, 0f, -rowStep);

        return (axialOffset.x * basisQ) + (axialOffset.y * basisR);
    }
    
    public Vector2Int GetAxialCoordinate(HexCell cell)
    {
        if (cell == null) return Vector2Int.zero;
        int q = cell.col - (cell.row - (cell.row & 1)) / 2;
        int r = cell.row;
        return new Vector2Int(q, r);
    }
    
    public HexCell GetCellAt(int col, int row)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].col == col && cells[i].row == row) 
                return cells[i];
        }
        return null;
    }

    public HexCell GetCellWithOffset(HexCell origin, Vector2Int axialOffset)
    {
        if (origin == null) return null;

        // Convert Odd-R (staggered) coordinates to Axial coordinates
        int q = origin.col - (origin.row - (origin.row & 1)) / 2;
        int r = origin.row;

        // Apply the pattern offset safely
        int targetQ = q + axialOffset.x;
        int targetR = r + axialOffset.y;

        // Convert Axial coordinates back to Odd-R (staggered) coordinates
        int targetCol = targetQ + (targetR - (targetR & 1)) / 2;
        int targetRow = targetR;

        return GetCellAt(targetCol, targetRow);
    }

}