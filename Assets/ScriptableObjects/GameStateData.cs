using UnityEngine;

[CreateAssetMenu(fileName = "NewGameState", menuName = "Game Data/Game State")]
public class GameStateData : ScriptableObject
{
    public int maxFrames = 10;

    public int startingRound = 1;
    public int startingRoundScoreTarget = 30;

    public void ResetState()
    {
        startingRound = 1;
        startingRoundScoreTarget = 30;
    }
}