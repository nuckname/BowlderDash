using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(HexGrid), typeof(GridOccupancy))]
public class GridTicketManager : MonoBehaviour
{
    public class TicketInstance
    {
        public HexCell cell;
        public int value;
        public GameObject visual;
    }

    [Header("Ticket Economy")]
    // Scale with round number?
    public int minTicketsPerFrame = 3;
    public int maxTicketsPerFrame = 3;
    public int ticketValue = 1;
    public float ticketHeightOffset = 0.08f;
    public Color ticketCellColor = new Color(1f, 0.82f, 0.18f, 0.92f);
    public GameObject ticketVisualPrefab;

    private List<TicketInstance> activeTickets = new List<TicketInstance>();
    private HexGrid grid;
    private GridOccupancy occupancy;

    private void Awake()
    {
        grid = GetComponent<HexGrid>();
        occupancy = GetComponent<GridOccupancy>();
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        for (int i = 0; i < activeTickets.Count; i++)
        {
            var t = activeTickets[i];
            if (t.visual == null) continue;

            float bob = Mathf.Sin((Time.time * 3f) + i) * 0.025f;
            t.visual.transform.position = t.cell.center + Vector3.up * (ticketHeightOffset + bob);
            t.visual.transform.Rotate(Vector3.up, 60f * Time.deltaTime, Space.World);
        }
    }

    public void SpawnTicketsForFrame()
    {
        ClearTickets();
        if (grid.Cells.Count == 0) return;

        int count = Random.Range(Mathf.Clamp(minTicketsPerFrame, 0, grid.Cells.Count), 
            Mathf.Clamp(maxTicketsPerFrame, 0, grid.Cells.Count) + 1);

        List<HexCell> available = new List<HexCell>(grid.Cells);
        
        for (int i = 0; i < count; i++)
        {
            int r = Random.Range(0, available.Count);
            HexCell chosen = available[r];
            available.RemoveAt(r);

            activeTickets.Add(new TicketInstance
            {
                cell = chosen,
                value = Mathf.Max(1, ticketValue),
                visual = CreateTicketVisual(chosen)
            });
        }
    }

    public bool TryCollectTicketForPin(Pin pin, out int collectedValue)
    {
        collectedValue = 0;
        if (pin == null) return false;

        HexCell cell = occupancy.GetCellByOccupant(pin.transform) ?? grid.GetNearestCell(pin.transform.position);
        if (cell == null) return false;

        TicketInstance ticket = activeTickets.Find(t => t.cell == cell);
        if (ticket == null) return false;

        collectedValue = ticket.value;
        Destroy(ticket.visual);
        activeTickets.Remove(ticket);
        return true;
    }

    public bool HasTicketAt(HexCell cell) => activeTickets.Exists(t => t.cell == cell);

    public void ClearTickets()
    {
        foreach (var t in activeTickets) if (t.visual != null) Destroy(t.visual);
        activeTickets.Clear();
    }

    private GameObject CreateTicketVisual(HexCell cell)
    {
        GameObject vis = ticketVisualPrefab != null 
            ? Instantiate(ticketVisualPrefab, transform) 
            : GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        Destroy(vis.GetComponent<Collider>());
        vis.name = $"Ticket_{cell.row}_{cell.col}";
        vis.transform.position = cell.center + Vector3.up * ticketHeightOffset;
        return vis;
    }
}