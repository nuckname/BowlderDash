using TMPro;
using UnityEngine;

public class LeaderboardRow : MonoBehaviour
{
    public TMP_Text rankText;
    public TMP_Text nameText;
    public TMP_Text totalScoreText;
    public TMP_Text highestRoundScoreText;
    public TMP_Text highestRoundText;

    public void Bind(int rank, LeaderboardEntry entry)
    {
        rankText.text = rank.ToString();
        // Names are plain text, even if a player types TMP formatting tags.
        nameText.richText = false;
        nameText.text = entry.playerName;
        totalScoreText.text = entry.totalScore.ToString("N0");
        highestRoundScoreText.text = entry.highestRoundScore.ToString("N0");
        // Saved rounds are zero-based; players see the first round as round 1.
        highestRoundText.text = (entry.highestRoundReached + 1).ToString();
    }
}
