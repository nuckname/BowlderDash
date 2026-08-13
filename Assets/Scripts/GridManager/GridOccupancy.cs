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
        return true;
    }

    public void ReleaseCell(Transform requester)
    {
        foreach (var cell in grid.Cells)
        {
            if (cell.occupant == requester)
            {
                cell.occupant = null;
                return;
            }
        }
    }

    public void ClearAllOccupants()
    {
        foreach (var cell in grid.Cells) cell.occupant = null;
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