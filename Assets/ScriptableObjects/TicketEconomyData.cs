using UnityEngine;

// Actaully i want a ticket UI singleton
[CreateAssetMenu(fileName = "NewTicketEconomy", menuName = "Game Data/Ticket Economy")]
public class TicketEconomyData : ScriptableObject
{
    public int startingTickets = 0;

    public int currentTickets = 0;
    public int gridTicketsEarnedThisFrame = 0;
    public int scoreTargetTicketsEarnedThisFrame = 0;
    public int ticketsEarnedLastFrame = 0;
    public bool lastFrameUsedTicketBonus = false;
    public bool lastFrameUsedStrikeTicketBonus = false;

    public void ResetState()
    {
        currentTickets = startingTickets;
        gridTicketsEarnedThisFrame = 0;
        scoreTargetTicketsEarnedThisFrame = 0;
        ticketsEarnedLastFrame = 0;
        lastFrameUsedTicketBonus = false;
        lastFrameUsedStrikeTicketBonus = false;
    }
}