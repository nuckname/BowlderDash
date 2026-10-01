using UnityEngine;

[CreateAssetMenu(fileName = "AmountOfTicketsToSpawnOnLane", menuName = "AmountOfTicketsToSpawnOnLane")]
public class AmountOfTicketsToSpawnOnLane : ScriptableObject
{
    public int GetAmountToSpawn(int currentRound)
    {
        switch (currentRound)
        {
            case 0:
                return 0;
            case 1:
                return 1;
            case 2:
                return 2;
            case 3:
                return 3;
            case 4:
                return 3;
            case 5:
                return 3;
            case 6:
                return 4;
            case 7:
                return 4;
            case 8:
                return 4;
            default:
                return 1;
        }
    }
}

