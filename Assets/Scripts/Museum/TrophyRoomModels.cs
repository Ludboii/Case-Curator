using System;
using System.Collections.Generic;

public enum TrophyRoomFocus
{
    MuseumGoldIncome,
    MuseumDiamondIncome,
    AutomatedAcquisitions,
    GiftRetrievals
}

public enum TrophyInventorySortMode
{
    HighestTrophyPower,
    HighestValue,
    HighestRarity,
    LowestFloat,
    Newest,
    Weapon
}

[Serializable]
public sealed class TrophyPowerBreakdown
{
    public double rarityScore;
    public double marketValueScore;
    public double variantScore;
    public double floatScore;

    public double rarityContribution;
    public double marketValueContribution;
    public double variantContribution;
    public double floatContribution;

    public double lowFloatPrestige;
    public double highFloatPrestige;
    public double rangeRelativePrestige;
    public double absoluteFloatPrestige;

    public double rawTrophyPower;
    public double pedestalMultiplier = 1d;
    public int finalContribution;
}

[Serializable]
public sealed class TrophyRoomSlotSnapshot
{
    public int slotIndex;
    public bool unlocked;
    public bool occupied;
    public double pedestalMultiplier = 1d;
    public InventoryItem item;
    public TrophyPowerBreakdown power;
}

[Serializable]
public sealed class TrophyRoomSnapshot
{
    public TrophyRoomFocus focus;
    public int unlockedSlotCount;
    public int occupiedSlotCount;
    public int totalWeightedPower;
    public double activeBonusFraction;
    public List<TrophyRoomSlotSnapshot> slots =
        new List<TrophyRoomSlotSnapshot>();
}

[Serializable]
public sealed class TrophyRoomOperationResult
{
    public bool success;
    public string message;
    public int slotIndex = -1;
    public InventoryItem item;

    public static TrophyRoomOperationResult Completed(
        string message,
        int slotIndex,
        InventoryItem item = null)
    {
        return new TrophyRoomOperationResult
        {
            success = true,
            message = message ?? "Trophy Room updated.",
            slotIndex = slotIndex,
            item = item
        };
    }

    public static TrophyRoomOperationResult Failed(string message)
    {
        return new TrophyRoomOperationResult
        {
            success = false,
            message = message ?? "The Trophy Room action failed."
        };
    }
}
