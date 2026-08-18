using UnityEngine;

[System.Serializable]
public class SessionData
{
    private static SessionData _instance;
    
    // Just rider shorthand, it just returns a singleton or creates a new one if its null.
    public static SessionData Instance => _instance ?? (_instance = new SessionData());

    public int currentScore;
    public int currentFrame;
    public int ballsThrown;
    public int tickets;

    public void AddScore(int amount)
    {
        currentScore += amount;
    }

    public void AddBallThrown()
    {
        ballsThrown++;
    }

    public void AddTickets(int amount)
    {
        tickets += amount;
        InGameScoreUi.Instance.UpdateTicketText(tickets);
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
            InGameScoreUi.Instance.UpdateTicketText(tickets);
            return true;
        }
        return false;
    }

    public void NewRoundReset()
    {
        currentScore = 0;
        ballsThrown = 0;
    }
    
    public void StartBrandNewSession()
    {
        currentScore = 0;
        currentFrame = 1;
        ballsThrown = 0;
        tickets = 0;
        InGameScoreUi.Instance.UpdateTicketText(tickets);
    }
}