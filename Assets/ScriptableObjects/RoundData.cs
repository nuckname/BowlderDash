using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoundScore
{
    public int roundNumber = 1;

    public int scoreNeededToBeat = 30;

    public int maxBallsAllowedToThrow = 3;
}

[CreateAssetMenu(fileName = "NewRoundData", menuName = "Game Data/Round Data")]
public class RoundData : ScriptableObject
{
    [Header("Round Settings")]
    public List<RoundScore> rounds = new List<RoundScore>();

    public RoundScore GetRoundInfo(int targetRoundNumber)
    {
        if (rounds == null)
        {
            Debug.LogWarning("Round data has no configured rounds");
            return null;
        }

        // Search through the list for a matching round number
        foreach (RoundScore round in rounds)
        {
            if (round != null && round.roundNumber == targetRoundNumber)
            {
                return round;
            }
        }

        Debug.LogWarning($"Round {targetRoundNumber} was not found");
        return null;
    }

    public int GetMaxRoundNumber()
    {
        int maxRound = 0;

        if (rounds == null)
        {
            return maxRound;
        }

        foreach (RoundScore round in rounds)
        {
            if (round != null && round.roundNumber > maxRound)
            {
                maxRound = round.roundNumber;
            }
        }
        
        return maxRound;
    }

    public bool IsLastRound(int targetRoundNumber)
    {
        if (rounds == null || rounds.Count == 0)
        {
            return false;
        }

        bool targetRoundExists = false;
        int maxRoundNumber = 0;

        foreach (RoundScore round in rounds)
        {
            if (round == null)
            {
                continue;
            }

            if (round.roundNumber == targetRoundNumber)
            {
                targetRoundExists = true;
            }

            if (round.roundNumber > maxRoundNumber)
            {
                maxRoundNumber = round.roundNumber;
            }
        }

        return targetRoundExists && targetRoundNumber == maxRoundNumber;
    }
}
