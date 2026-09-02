using UnityEngine;

[System.Serializable]
public class SessionData
{
    private static SessionData _instance;
    
    // Just rider shorthand, it just returns a singleton or creates a new one if its null.
    public static SessionData Instance => _instance ?? (_instance = new SessionData());

    public int currentScore;
    public int currentRound = 0;
    public int ballsThrown;
    public int tickets;

    public void AddScore(int amount)
    {
        currentScore += amount;
    }

    public void AddRound()
    {
        currentRound++;
    }

    public void AddBallThrown()
    {
        ballsThrown++;
    }

    public void AddTickets(int amount)
    {
        tickets += amount;
    }

    private bool CanAfford(int amount)
    {
        return tickets >= amount;
    }

    public bool SpendTickets(int amount)
    {
        if (CanAfford(amount))
        {
            tickets -= amount;
            InGameScoreUi.Instance.UpdateTicketText(tickets); // this is for the shop
            return true;
        }
        return false;
    }
    
    public void StartBrandNewSession()
    {
        currentScore = 0;
        currentRound = 0;
        ballsThrown = 0;
        tickets = 0;
        InGameScoreUi.Instance.UpdateTicketText(tickets);
    }
    
    public void RoundOverResetValues()
    {
        currentScore = 0;
        ballsThrown = 0;
    }
}