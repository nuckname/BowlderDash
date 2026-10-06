using System.Collections.Generic;
using UnityEngine;

// Legacy Code
[RequireComponent(typeof(HexGrid))]
public class GridOccupancy : MonoBehaviour
{
    public bool allowOnePerCell = true;
    private HexGrid grid;

    private void Awake() => grid = GetComponent<HexGrid>();

    public bool TryReserveCell(HexCell target, Transform requester)
    {
        if (target == null || requester == null) return false;
        if (target.occupant == requester) return true;
        if (allowOnePerCell && target.occupant != null) return false;

        ReleaseCell(requester);
        target.occupant = requester;
        target.pinOccupant = requester.GetComponent<PinBase>();
        return true;
    }

    public void ReleaseCell(Transform requester)
    {
        foreach (var cell in grid.Cells)
        {
            if (cell.occupant == requester)
            {
                cell.occupant = null;
                cell.pinOccupant = null;
                return;
            }
        }
    }

    public bool TryReserveCells(IReadOnlyDictionary<Transform, HexCell> destinations)
    {
        if (grid == null || destinations == null || destinations.Count == 0) return false;

        var validCells = new HashSet<HexCell>(grid.Cells);
        var targets = new HashSet<HexCell>();
        foreach (var destination in destinations)
        {
            Transform requester = destination.Key;
            HexCell target = destination.Value;
            if (requester == null || target == null || !validCells.Contains(target) || !targets.Add(target))
                return false;
            if (allowOnePerCell && target.occupant != null && !destinations.ContainsKey(target.occupant))
                return false;
        }

        // Commit only after every destination passes validation.
        foreach (HexCell cell in grid.Cells)
        {
            if (cell.occupant != null && destinations.ContainsKey(cell.occupant))
            {
                cell.occupant = null;
                cell.pinOccupant = null;
            }
        }
        foreach (var destination in destinations)
        {
            destination.Value.occupant = destination.Key;
            destination.Value.pinOccupant = destination.Key.GetComponent<PinBase>();
        }
        return true;
    }

    public void ClearAllOccupants()
    {
        foreach (var cell in grid.Cells)
        {
            cell.occupant = null;
            cell.pinOccupant = null;
        }
    }

    public HexCell GetCellByOccupant(Transform t)
    {
        
        if (t == null) return null;
        foreach (var cell in grid.Cells)
        {
            if (cell.occupant == t) return cell;
        }
        return null;
    }
}
