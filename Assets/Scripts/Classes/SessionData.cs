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
    }

    public bool CanAfford(int amount)
    {
        return tickets >= amount;
    }

    public bool SpendTickets(int amount)
    {
        if (CanAfford(amount))
        {
            tickets -= amount;
            return true;
        }
        return false;
    }

    public void ResetSession()
    {
        currentScore = 0;
        currentFrame = 1;
        ballsThrown = 0;
        tickets = 0;
    }
}