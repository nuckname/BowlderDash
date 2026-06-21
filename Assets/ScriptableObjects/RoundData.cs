using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoundScore
{
    [Tooltip("The round number, or total number of rounds for this set.")]
    public int roundNumber = 1;

    [Tooltip("The target score required to beat this round.")]
    public int scoreNeededToBeat = 100;

    [Tooltip("The amount of balls the player is allowed to throw.")]
    public int ballsAllowed = 3;
}

[CreateAssetMenu(fileName = "NewRoundData", menuName = "Game Data/Round Data")]
public class RoundData : ScriptableObject
{
    [Header("Round Settings")]
    public List<RoundScore> rounds = new List<RoundScore>();

    public RoundScore GetRoundInfo(int targetRoundNumber)
    {
        // Search through the list for a matching round number
        foreach (RoundScore round in rounds)
        {
            if (round.roundNumber == targetRoundNumber)
            {
                return round;
            }
        }

        Debug.LogWarning($"Round {targetRoundNumber} was not found");
        return null;
    }
}