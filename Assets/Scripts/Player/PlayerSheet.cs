using UnityEngine;

public class PlayerSheet : MonoBehaviour
{
    public static PlayerSheet Instance { get; private set; }

    public int playerCurrentTicket = 0;
    private int _playerAmountOfBallsThrown = 0;
    public int currentScore = 0;
    public bool lastFrameUsedTicketBonus = false;
    public bool lastFrameUsedStrikeTicketBonus = false;
    public int ticketsEarnedLastFrame = 0;
    public int scoreTargetTicketsEarnedThisFrame = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
            
            DontDestroyOnLoad(this.gameObject); 
        }
    }

    public void ResetPlayerStats()
    {
        ResetScore();
        ResetBallsThrown();
    }
    
    public void AddTickets(int amount)
    {
        playerCurrentTicket += amount;
        
        // keeps track, not sure why
        ticketsEarnedLastFrame += amount; 
    }

    // Score

    public void AddScore(int amount)
    {
        currentScore += amount;
    }

    public void ResetScore()
    {
        currentScore = 0;
    }

    public int GetScore()
    {
        return currentScore;
    }

    // Ball Throw

    public void AddBallsThrown()
    {
        _playerAmountOfBallsThrown++;
    }

    public void ResetBallsThrown()
    {
        _playerAmountOfBallsThrown = 0;
    }

    public int GetAmountOfBallsThrown()
    {
        return _playerAmountOfBallsThrown;
    }
}