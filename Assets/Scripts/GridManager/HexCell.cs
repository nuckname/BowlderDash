using UnityEngine;

[System.Serializable]
public class HexCell
{
    public int col;
    public int row;
    public Vector3 center;
    public Transform occupant;

    public HexCell(int col, int row, Vector3 center)
    {
        this.col = col;
        this.row = row;
        this.center = center;
        this.occupant = null;
    }
}