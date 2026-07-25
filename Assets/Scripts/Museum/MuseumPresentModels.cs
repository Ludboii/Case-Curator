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

/// <summary>
/// One weighted openable-container result in a Museum Present. CaseData is used
/// for weapon cases, collection packages, souvenir packages, sticker capsules
/// and custom cases, so every existing container type can share this pool.
/// </summary>
[Serializable]
public class MuseumPresentContainerDrop
{
    public CaseData container;

    [Min(0.0001f)]
    public float weight = 1f;

    [Min(1)]
    public int minimumAmount = 1;

    [Min(1)]
    public int maximumAmount = 1;

    public bool IsValid =>
        container != null &&
        weight > 0f &&
        minimumAmount > 0 &&
        maximumAmount >= minimumAmount;

    public void Normalize()
    {
        weight = Mathf.Max(0.0001f, weight);
        minimumAmount = Mathf.Max(1, minimumAmount);
        maximumAmount = Mathf.Max(minimumAmount, maximumAmount);
    }
}

[Serializable]
public class MuseumPresentTierConfig
{
    public MuseumPresentTier tier = MuseumPresentTier.Dusty;
    public string displayName;
    public Sprite icon;

    [Min(1)]
    public int fragmentsPerPresent = 100;

    [Header("Opening Currency Reward Range")]
    [Min(0f)] public float minimumGold;
    [Min(0f)] public float maximumGold;
    [Min(0)] public int minimumXP;
    [Min(0)] public int maximumXP;
    [Min(0)] public int minimumDiamonds;
    [Min(0)] public int maximumDiamonds;

    [Header("Openable Container Drop Pool")]
    [Tooltip(
        "Each Museum Present awards one weighted entry from this pool in " +
        "addition to Gold, XP and possible Diamonds. CaseData includes cases, " +
        "collections, souvenir packages, sticker capsules and custom cases.")]
    public List<MuseumPresentContainerDrop> containerDrops =
        new List<MuseumPresentContainerDrop>();

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(displayName)
            ? displayName.Trim()
            : MuseumPresentUtility.GetTierDisplayName(tier);

    public int ValidContainerDropCount
    {
        get
        {
            int count = 0;

            if (containerDrops == null)
                return count;

            for (int i = 0; i < containerDrops.Count; i++)
            {
                if (containerDrops[i] != null && containerDrops[i].IsValid)
                    count++;
            }

            return count;
        }
    }

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

        if (containerDrops == null)
            containerDrops = new List<MuseumPresentContainerDrop>();

        for (int i = 0; i < containerDrops.Count; i++)
        {
            if (containerDrops[i] != null)
                containerDrops[i].Normalize();
        }
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
    public CaseData containerReward;
    public int containerAmount;
    public int remainingPresents;

    public bool HasContainerReward =>
        containerReward != null && containerAmount > 0;

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

public sealed class MuseumGoldContainerCompletionReward
{
    public CaseData container;
    public MuseumPresentTier tier;
    public int fragments;
    public string message;
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

    public static MuseumPresentTier FromMilestoneBand(
        MuseumMilestoneBand band)
    {
        switch (band)
        {
            case MuseumMilestoneBand.StarterArchive:
                return MuseumPresentTier.Bronze;
            case MuseumMilestoneBand.CollectorHall:
                return MuseumPresentTier.Silver;
            case MuseumMilestoneBand.PremiumVault:
                return MuseumPresentTier.Gold;
            case MuseumMilestoneBand.MythicGallery:
                return MuseumPresentTier.Diamond;
            case MuseumMilestoneBand.GlobalExhibit:
                return MuseumPresentTier.GlobalElite;
            default:
                return MuseumPresentTier.Dusty;
        }
    }
}
