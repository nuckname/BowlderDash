using UnityEngine;

public class GridManager : MonoBehaviour
{
    public HexGrid grid;
    private GridOccupancy occupancy;
    public GridTicketManager gridTicketManager { get; private set; }
    public HexGridVisuals hexGridVisuals;

    private void Awake()
    {
        grid = GetComponent<HexGrid>();
        occupancy = GetComponent<GridOccupancy>();
        gridTicketManager = GetComponent<GridTicketManager>();
        hexGridVisuals = GetComponent<HexGridVisuals>();
    }

    [ContextMenu("Generate Grid")]
    public void GenerateGrid()
    {
        grid.GenerateGrid();
        hexGridVisuals.RebuildVisuals();
    }

    public void ClearAllOccupants() => occupancy.ClearAllOccupants();
    
    public void SpawnTicketsForFrame()
    {
        gridTicketManager.SpawnTicketsForFrame();
        hexGridVisuals.RefreshColors();
    }
    
    public int GetAdjacentPinCount(PinBase pin)
    {
        if (pin == null) return 0;

        HexCell originCell = occupancy.GetCellByOccupant(pin.transform);
        if (originCell == null) return 0;

        int count = 0;
        foreach (HexCell neighbor in grid.GetNeighborCells(originCell))
        {
            if (neighbor.pinOccupant != null && neighbor.pinOccupant != pin)
            {
                count++;
            }
        }

        return count;
    }
    
    public HexCell GetCellWithOffset(HexCell origin, Vector2Int axialOffset) => grid.GetCellWithOffset(origin, axialOffset);
    public Vector2Int GetAxialCoordinate(HexCell cell) => grid.GetAxialCoordinate(cell);
    public bool TryCollectTicketForPin(PinBase pin, out int val) => gridTicketManager.TryCollectTicketForPin(pin, out val);
    public Vector3 GetWorldOffsetFromAxial(Vector2Int axialOffset) => grid.GetWorldOffsetFromAxial(axialOffset);
    public HexCell GetNearestCell(Vector3 pos) => grid.GetNearestCell(pos);

    public bool TryReserveCell(HexCell cell, Transform requester) => occupancy.TryReserveCell(cell, requester);
    public bool TryReserveCells(System.Collections.Generic.IReadOnlyDictionary<Transform, HexCell> destinations) =>
        occupancy.TryReserveCells(destinations);

}
