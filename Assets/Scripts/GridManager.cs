using UnityEngine;

public class GridManager : MonoBehaviour
{
    public HexGrid grid;
    private GridOccupancy occupancy;
    public GridTicketManager gridTicketManager { get; private set; }
    private HexGridVisuals visuals;

    private void Awake()
    {
        grid = GetComponent<HexGrid>();
        occupancy = GetComponent<GridOccupancy>();
        gridTicketManager = GetComponent<GridTicketManager>();
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
        gridTicketManager.SpawnTicketsForFrame();
        visuals.RefreshColors();
    }
    
    public HexCell GetCellWithOffset(HexCell origin, Vector2Int axialOffset) => grid.GetCellWithOffset(origin, axialOffset);
    public Vector2Int GetAxialCoordinate(HexCell cell) => grid.GetAxialCoordinate(cell);
    public bool TryCollectTicketForPin(PinBase pin, out int val) => gridTicketManager.TryCollectTicketForPin(pin, out val);
    public Vector3 GetWorldOffsetFromAxial(Vector2Int axialOffset) => grid.GetWorldOffsetFromAxial(axialOffset);
    public HexCell GetNearestCell(Vector3 pos) => grid.GetNearestCell(pos);

    public bool TryReserveCell(HexCell cell, Transform requester) => occupancy.TryReserveCell(cell, requester);

}