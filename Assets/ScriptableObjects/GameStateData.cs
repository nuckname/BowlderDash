using UnityEngine;

[CreateAssetMenu(fileName = "NewGameState", menuName = "Game Data/Game State")]
public class GameStateData : ScriptableObject
{
    public int maxFrames = 10;
    public int maxThrowsPerFrame = 2;

    public int currentFrame = 1;
    public int throwsThisFrame = 0;
    public int totalScore = 0;
    public int scoreAppliedToTotalThisFrame = 0;
    public int currentFrameScoreTarget = 30;

    public void ResetState()
    {
        currentFrame = 1;
        throwsThisFrame = 0;
        totalScore = 0;
        scoreAppliedToTotalThisFrame = 0;
        currentFrameScoreTarget = 30;
    }
}