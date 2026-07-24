using System;
using System.Collections.Generic;
using UnityEngine;

public enum MuseumPresentTier
{
    Dusty = 0,
    Bronze = 1,
    Silver = 2,
    Gold = 3,
    Diamond = 4,
    GlobalElite = 5
}

[Serializable]
public class MuseumPresentRewardEntry
{
    public MuseumPresentTier tier = MuseumPresentTier.Dusty;

    [Min(0)]
    public int fragments;

    [Min(0)]
    public int presents;

    public bool HasReward => fragments > 0 || presents > 0;
}

[Serializable]
public class MuseumPresentTierConfig
{
    public MuseumPresentTier tier = MuseumPresentTier.Dusty;
    public string displayName;
    public Sprite icon;

    [Min(1)]
    public int fragmentsPerPresent = 100;

    [Header("Opening Reward Range")]
    [Min(0f)] public float minimumGold;
    [Min(0f)] public float maximumGold;
    [Min(0)] public int minimumXP;
    [Min(0)] public int maximumXP;
    [Min(0)] public int minimumDiamonds;
    [Min(0)] public int maximumDiamonds;

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(displayName)
            ? displayName.Trim()
            : MuseumPresentUtility.GetTierDisplayName(tier);

    public void Normalize()
    {
        displayName = displayName != null ? displayName.Trim() : "";
        fragmentsPerPresent = Mathf.Max(1, fragmentsPerPresent);
        minimumGold = Mathf.Max(0f, minimumGold);
        maximumGold = Mathf.Max(minimumGold, maximumGold);
        minimumXP = Mathf.Max(0, minimumXP);
        maximumXP = Mathf.Max(minimumXP, maximumXP);
        minimumDiamonds = Mathf.Max(0, minimumDiamonds);
        maximumDiamonds = Mathf.Max(minimumDiamonds, maximumDiamonds);
    }
}

public sealed class MuseumPresentOpenResult
{
    public bool success;
    public string message;
    public MuseumPresentTier tier;
    public float gold;
    public int xp;
    public int diamonds;
    public int remainingPresents;

    public static MuseumPresentOpenResult Failed(
        MuseumPresentTier tier,
        string message)
    {
        return new MuseumPresentOpenResult
        {
            success = false,
            tier = tier,
            message = string.IsNullOrWhiteSpace(message)
                ? "The Museum Present could not be opened."
                : message
        };
    }
}

public sealed class MuseumPresentGrantSummary
{
    public string milestoneId;
    public List<string> rewardLines = new List<string>();

    public bool HasRewards => rewardLines != null && rewardLines.Count > 0;
}

public static class MuseumPresentUtility
{
    public static readonly MuseumPresentTier[] AllTiers =
    {
        MuseumPresentTier.Dusty,
        MuseumPresentTier.Bronze,
        MuseumPresentTier.Silver,
        MuseumPresentTier.Gold,
        MuseumPresentTier.Diamond,
        MuseumPresentTier.GlobalElite
    };

    public static string GetTierDisplayName(MuseumPresentTier tier)
    {
        return tier == MuseumPresentTier.GlobalElite
            ? "Global Elite"
            : tier.ToString();
    }

    public static bool TryParseTier(
        string value,
        out MuseumPresentTier tier)
    {
        tier = MuseumPresentTier.Dusty;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value
            .Trim()
            .Replace(" ", "")
            .Replace("-", "");

        return Enum.TryParse(normalized, true, out tier);
    }
}
