using System.IO;
using UnityEngine;

// Used only by the separate preview scene; never attach this to the game screen prefab.
public class LeaderboardScreenPreview : MonoBehaviour
{
    public LeaderboardScreen screen;
    public bool victory = true;
    public int totalScore = 550;
    public int highestRoundScore = 220;
    public int highestRoundReached = 8;

    private void Start() => ShowSample();

    [ContextMenu("Show New Sample Result")]
    public void ShowSample()
    {
        if (!Application.isPlaying) return;
        screen.Show(new LeaderboardEntry
        {
            totalScore = totalScore,
            highestRoundScore = highestRoundScore,
            highestRoundReached = highestRoundReached
        }, victory, ShowSample, ShowSample,
            new LeaderboardStorage(Path.Combine(Application.persistentDataPath, "leaderboard-test.txt")));
    }
}
