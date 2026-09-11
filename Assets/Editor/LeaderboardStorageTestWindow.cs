using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
// This script allows for sample leaderboard submissions.
// Use in Tools > Leaderboard > Storage Test
// Uses a separate file so manual sample submissions can't enter the game's leaderboard
public class LeaderboardStorageTestWindow : EditorWindow
{
    private string playerName = "Player";
    private int totalScore = 55;
    private int highestRoundScore = 35;
    private int highestRoundReached = 1;
    private List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
    private string status;
    private bool hasError;
    private Vector2 scroll;
    private LeaderboardStorage Storage => new LeaderboardStorage(
        Path.Combine(Application.persistentDataPath, "leaderboard-test.txt"));

    [MenuItem("Tools/Leaderboard/Storage Test")]
    private static void Open() => GetWindow<LeaderboardStorageTestWindow>("Leaderboard Storage");

    private void OnEnable() => Reload();

    private void Reload()
    {
        hasError = !Storage.TryLoad(out entries, out string error);
        status = hasError ? error : $"Loaded {entries.Count} entries.";
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Manual storage test: uses leaderboard-test.txt. Play Mode is not required.", MessageType.Info);
        EditorGUILayout.LabelField("Test file", Storage.FilePath);
        if (GUILayout.Button("Open Save Folder")) EditorUtility.RevealInFinder(Application.persistentDataPath);

        // Deliberately unrestricted input lets you test the storage validation itself.
        playerName = EditorGUILayout.TextField("Player name", playerName);
        totalScore = EditorGUILayout.IntField("Total score", totalScore);
        highestRoundScore = EditorGUILayout.IntField("Highest round score", highestRoundScore);
        highestRoundReached = EditorGUILayout.IntField("Highest round reached", highestRoundReached);

        if (GUILayout.Button("Save Sample Entry"))
        {
            var entry = new LeaderboardEntry
            {
                playerName = playerName,
                totalScore = totalScore,
                highestRoundScore = highestRoundScore,
                highestRoundReached = highestRoundReached
            };
            hasError = !Storage.TryAddEntry(entry, out entries, out string error);
            status = hasError ? error : "Saved sample entry. Each click intentionally submits a new game.";
        }
        if (GUILayout.Button("Reload From File")) Reload();
        if (!string.IsNullOrEmpty(status))
            EditorGUILayout.HelpBox(status, hasError ? MessageType.Error : MessageType.Info);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (entries.Count == 0) EditorGUILayout.LabelField("No entries to display.");
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            EditorGUILayout.LabelField($"{i + 1}. {entry.playerName}",
                $"Total: {entry.totalScore} | Best: {entry.highestRoundScore} | Round: {entry.highestRoundReached}");
        }
        EditorGUILayout.EndScrollView();
    }
}
