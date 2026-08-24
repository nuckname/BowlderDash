using UnityEngine;

public class GridManager : MonoBehaviour
{
    private HexGrid grid;
    private GridOccupancy occupancy;
    private GridTicketManager tickets;
    private HexGridVisuals visuals;

    private void Awake()
    {
        grid = GetComponent<HexGrid>();
        occupancy = GetComponent<GridOccupancy>();
        tickets = GetComponent<GridTicketManager>();
        visuals = GetComponent<HexGridVisuals>();
    }

    [ContextMenu("Generate Grid")]
    public void GenerateGrid()
    {
        grid.GenerateGrid();
        visuals.RebuildVisuals();
    }

    public void ClearAllOccupants() => occupancy.ClearAllOccupants();
    
    public void SpawnTicketsForFrame()
    {
        tickets.SpawnTicketsForFrame();
        visuals.RefreshColors();
    }
    
    public HexCell GetCellWithOffset(HexCell origin, Vector2Int axialOffset) => grid.GetCellWithOffset(origin, axialOffset);

    public bool TryCollectTicketForPin(PinBase pin, out int val) => tickets.TryCollectTicketForPin(pin, out val);
    public Vector3 GetWorldOffsetFromAxial(Vector2Int axialOffset) => grid.GetWorldOffsetFromAxial(axialOffset);
    public HexCell GetNearestCell(Vector3 pos) => grid.GetNearestCell(pos);

    public bool TryReserveCell(HexCell cell, Transform requester) => occupancy.TryReserveCell(cell, requester);

}