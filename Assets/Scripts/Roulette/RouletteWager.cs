using System;

public readonly struct RouletteWager
{
    public RouletteColour Colour { get; }
    public int Stake { get; }
    public int TotalReturn { get; }

    private RouletteWager(RouletteColour colour, int stake, int totalReturn)
    {
        Colour = colour;
        Stake = stake;
        TotalReturn = totalReturn;
    }

    public static int GetMultiplier(RouletteColour colour)
    {
        switch (colour)
        {
            case RouletteColour.Red:
            case RouletteColour.Black: return 2;
            case RouletteColour.Green: return 30;
            default: return 0;
        }
    }

    public static int GetMaximumStake(RouletteColour colour, int wallet)
    {
        int multiplier = GetMultiplier(colour);
        if (wallet <= 0 || multiplier == 0) return 0;
        // Keep the payout and final balance within the integer limit
        int payoutLimit = int.MaxValue / multiplier;
        int balanceLimit = (int.MaxValue - wallet) / (multiplier - 1);
        return Math.Min(wallet, Math.Min(payoutLimit, balanceLimit));
    }

    public static bool TryCreate(RouletteColour colour, int stake, int wallet, out RouletteWager wager)
    {
        wager = default;
        if (stake <= 0 || wallet < stake || GetMultiplier(colour) == 0) return false;
        try
        {
            int totalReturn = checked(stake * GetMultiplier(colour));
            int remainingBalance = checked(wallet - stake);
            _ = checked(remainingBalance + totalReturn);
            wager = new RouletteWager(colour, stake, totalReturn);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}
