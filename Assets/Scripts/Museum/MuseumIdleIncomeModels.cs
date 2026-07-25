using System;

[Serializable]
public sealed class MuseumIdleIncomeSnapshot
{
    public bool goldUnlocked;
    public bool diamondsUnlocked;

    public double museumPoints;
    public int claimedGoldNodeCount;
    public double claimedGoldNodeWeight;

    public double incomeMultiplier = 1d;
    public double offlineHoursUpgradeBonus;
    public double goldCapacityMultiplier = 1d;
    public double diamondCapacityMultiplier = 1d;

    public double goldPerHour;
    public double diamondsPerHour;

    public double unclaimedGold;
    public double unclaimedDiamonds;

    public double goldCapacity;
    public double diamondCapacity;
    public double maximumOfflineHours;

    public bool goldAtCapacity;
    public bool diamondsAtCapacity;

    public int ClaimableWholeDiamonds =>
        Math.Max(0, (int)Math.Floor(unclaimedDiamonds + 0.0000001d));

    public bool CanClaimGold => unclaimedGold > 0.0001d;
    public bool CanClaimDiamonds => ClaimableWholeDiamonds > 0;
    public bool CanClaimAnything => CanClaimGold || CanClaimDiamonds;
}

[Serializable]
public sealed class MuseumIdleIncomeClaimResult
{
    public bool success;
    public double goldClaimed;
    public int diamondsClaimed;
    public string message;

    public static MuseumIdleIncomeClaimResult Empty(string reason)
    {
        return new MuseumIdleIncomeClaimResult
        {
            success = false,
            message = reason ?? "No Museum income is ready to claim."
        };
    }
}
