using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

// Local, synchronous storage for a small leaderboard. Only successful submissions can write a file.
public class LeaderboardStorage
{
    public const int MaxNameLength = 10;
    public string FilePath { get; }

    [Serializable]
    private class LeaderboardFile
    {
        public int version;
        public List<LeaderboardEntry> entries;
    }

    public LeaderboardStorage(string filePath = null)
    {
        FilePath = filePath ?? Path.Combine(Application.persistentDataPath, "leaderboard.txt");
    }

    public static bool TryValidateName(string input, out string playerName, out string error)
    {
        playerName = (input ?? string.Empty).Trim();
        error = null;
        if (playerName.Length == 0)
            error = "Please enter a player name.";
        else if (playerName.Length > MaxNameLength)
            error = "Player names can contain at most 10 characters.";
        else if (playerName.Any(c => char.IsControl(c) || c == '\u2028' || c == '\u2029'))
            error = "Player names must be a single line without control characters.";
        return error == null;
    }

    public bool TryLoad(out List<LeaderboardEntry> entries, out string error)
    {
        entries = new List<LeaderboardEntry>();
        error = null;
        try
        {
            string json;
            try
            {
                // Read directly so access failures are not mistaken for a missing file.
                json = File.ReadAllText(FilePath, Encoding.UTF8);
            }
            catch (FileNotFoundException) { return true; }
            catch (DirectoryNotFoundException) { return true; }

            var data = JsonUtility.FromJson<LeaderboardFile>(json);
            if (data == null || data.version != 1 || data.entries == null)
            {
                error = "The leaderboard file is damaged or uses an unsupported format. It has been left unchanged.";
                return false;
            }

            foreach (var entry in data.entries)
            {
                if (entry == null ||
                    !TryValidateName(entry.playerName, out string name, out _) ||
                    name != entry.playerName || !ValidScores(entry))
                {
                    error = "The leaderboard contains an invalid entry. The file has been left unchanged.";
                    return false;
                }
            }

            // OrderByDescending, equal scores keep their submission order.
            entries = data.entries.OrderByDescending(entry => entry.totalScore).ToList();
            return true;
        }
        catch (Exception exception) when (IsFileError(exception))
        {
            error = "Could not load the leaderboard. The file has been left unchanged. " + exception.Message;
            return false;
        }
    }

    public bool TryAddEntry(LeaderboardEntry entry, out List<LeaderboardEntry> entries, out string error)
    {
        entries = new List<LeaderboardEntry>();
        error = null;
        if (entry == null)
        {
            error = "No game result was provided.";
            return false;
        }
        if (!TryValidateName(entry.playerName, out string name, out error)) return false;
        if (!ValidScores(entry))
        {
            error = "Game scores and rounds must be non-negative, and the highest round score cannot exceed the total score.";
            return false;
        }

        // Never overwrite a file that cannot be successfully read and validated
        if (!TryLoad(out entries, out error)) return false;
        var updated = new List<LeaderboardEntry>(entries)
        {
            new LeaderboardEntry
            {
                playerName = name,
                totalScore = entry.totalScore,
                highestRoundScore = entry.highestRoundScore,
                highestRoundReached = entry.highestRoundReached
            }
        };
        updated = updated.OrderByDescending(result => result.totalScore).ToList();

        string temporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(FilePath));
            Directory.CreateDirectory(directory);
            string json = JsonUtility.ToJson(new LeaderboardFile { version = 1, entries = updated }, true);
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));

            // Write beside the destination, then replace it only after the full write succeeds.
            if (File.Exists(FilePath))
                File.Replace(temporaryPath, FilePath, null);
            else
                File.Move(temporaryPath, FilePath);

            entries = updated;
            return true;
        }
        catch (Exception exception) when (IsFileError(exception))
        {
            error = "Could not save your score. Please retry or skip score entry. " + exception.Message;
            return false;
        }
        finally
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
            catch (Exception exception) when (IsFileError(exception))
            {
                Debug.LogWarning("Could not remove temporary leaderboard file: " + exception.Message);
            }
        }
    }

    private static bool ValidScores(LeaderboardEntry entry)
    {
        return entry.totalScore >= 0 && entry.highestRoundScore >= 0 &&
               entry.highestRoundScore <= entry.totalScore && entry.highestRoundReached >= 0;
    }

    private static bool IsFileError(Exception exception)
    {
        return exception is IOException || exception is UnauthorizedAccessException ||
               exception is ArgumentException || exception is NotSupportedException ||
               exception is System.Security.SecurityException;
    }
}
