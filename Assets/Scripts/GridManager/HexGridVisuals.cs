using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HexGrid), typeof(GridTicketManager))]
public class HexGridVisuals : MonoBehaviour
{
    [Header("Visuals")]
    public bool showGizmos = true;
    public bool showGridInPlay = true;
    public Color freeCellColor = new Color(0f, 0.6f, 1f, 0.6f);
    public Color occupiedCellColor = new Color(0f, 1f, 0f, 0.6f);
    public float runtimeLineWidth = 0.08f;
    public float runtimeLineHeightOffset = 0.05f;

    private HexGrid grid;
    private GridTicketManager tickets;
    private List<LineRenderer> lines = new List<LineRenderer>();

    private void Awake()
    {
        grid = GetComponent<HexGrid>();
        tickets = GetComponent<GridTicketManager>();
    }

    public void RebuildVisuals()
    {
        foreach (var line in lines) if (line != null) Destroy(line.gameObject);
        lines.Clear();

        if (!Application.isPlaying || !showGridInPlay) return;

        Material mat = new Material(Shader.Find("Sprites/Default"));

        foreach (var cell in grid.Cells)
        {
            GameObject go = new GameObject($"HexLine_{cell.col}_{cell.row}");
            go.transform.SetParent(transform);

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = 6;
            lr.widthMultiplier = runtimeLineWidth;
            lr.material = mat;

            lr.SetPositions(GetHexVertices(cell.center + Vector3.up * runtimeLineHeightOffset, grid.hexSize));
            lines.Add(lr);
        }
    }

    public void RefreshColors()
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (i >= grid.Cells.Count) break;
            HexCell cell = grid.Cells[i];

            Color col = tickets.HasTicketAt(cell) ? tickets.ticketCellColor 
                      : cell.occupant != null ? occupiedCellColor 
                      : freeCellColor;

            lines[i].startColor = col;
            lines[i].endColor = col;
        }
    }

    public Vector3[] GetHexVertices(Vector3 center, float size)
    {
        Vector3[] verts = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float rad = Mathf.Deg2Rad * (30f + 60f * i);
            verts[i] = center + new Vector3(size * Mathf.Cos(rad), 0f, size * Mathf.Sin(rad));
        }
        return verts;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || grid == null || grid.Cells == null) return;
        
        foreach (HexCell cell in grid.Cells)
        {
            Gizmos.color = cell.occupant != null ? occupiedCellColor : freeCellColor;
            Vector3[] v = GetHexVertices(cell.center, grid.hexSize);
            for (int i = 0; i < 6; i++) Gizmos.DrawLine(v[i], v[(i + 1) % 6]);
        }
    }
}