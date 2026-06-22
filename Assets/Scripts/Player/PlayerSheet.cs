using UnityEngine;

public class PlayerSheet : MonoBehaviour
{
    public static PlayerSheet Instance { get; private set; }

    public int playerCurrentTicket = 0;
    public int playerAmountOfBallsThrown = 0;
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
    
    public void AddTickets(int amount)
    {
        playerCurrentTicket += amount;
        
        // keeps track, not sure why
        ticketsEarnedLastFrame += amount; 
    }
}