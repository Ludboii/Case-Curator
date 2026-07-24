using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Canonical 80-step, 5,000,000 MP Museum staircase approved for phase M4.
/// Thresholds and reward summaries match the final balancing workbook.
/// </summary>
public sealed class MuseumMilestone80Definition
{
    public int step;
    public double requiredMuseumPoints;
    public MuseumMilestoneBand band;
    public MuseumMilestoneType milestoneType;
    public string displayName;
    public string rewardSummary;
    public string presentTier;
    public string notes;
    public bool unlocksPassiveMuseumGold;
    public bool unlocksPassiveDiamonds;
    public string announcedSystemId;

    public string MilestoneId => $"museum-step-{step:00}";
    public string PlaqueId => $"museum-plaque-step-{step:00}";
    public bool IsMajor => milestoneType != MuseumMilestoneType.Plaque;
}

public static class MuseumMilestone80Defaults
{
    public const int FinalStep = 80;
    public const double FinalMuseumPoints = 5000000d;
    public const int PassiveDiamondUnlockStep = 75;

    private static readonly double[] RequiredPoints =
    {
        100, 250, 500, 800, 1200, 1800, 2500, 3300, 4100, 5000, 6000, 7500, 9000, 11000, 13500, 16500, 20000, 24000, 28500, 33500, 39000, 45000, 50000, 56000, 63000, 72000, 82000, 93000, 105000, 118000, 132000, 148000, 166000, 186000, 208000, 232000, 252000, 270000, 285000, 300000, 335000, 375000, 415000, 460000, 505000, 550000, 600000, 645000, 695000, 745000, 795000, 845000, 895000, 950000, 1000000, 1080000, 1180000, 1280000, 1390000, 1500000, 1610000, 1720000, 1840000, 1960000, 2080000, 2200000, 2320000, 2450000, 2570000, 2700000, 2880000, 3090000, 3310000, 3540000, 3770000, 4010000, 4250000, 4500000, 4750000, 5000000
    };

    private static readonly string[] RewardSummaries =
    {
        "Gold + XP + first Dusty fragments",
        "Gold + small upgrade token chance",
        "Dusty fragments + small Gold bundle",
        "Dusty Present fragments + Gold",
        "Full Dusty Present + first Museum plaque cosmetic",
        "Gold + small upgrade token chance",
        "Dusty Present fragments + XP",
        "Dusty Present fragments + Gold",
        "Gold + XP",
        "Full Bronze Present + Dusty Lobby renovation + passive Museum Gold income node",
        "Bronze Present fragments + XP",
        "Bronze fragments + Gold + XP",
        "Gold + XP",
        "Gold + small upgrade token chance",
        "Full Bronze Present + archive display cosmetic",
        "Bronze Present fragments + Gold",
        "Gold + XP",
        "Bronze fragments + small upgrade token chance",
        "Bronze Present fragments + XP",
        "Passive Museum Gold income node + Bronze Present fragments",
        "Gold + XP",
        "Gold + small upgrade token chance",
        "Large Bronze fragment bundle + Gold",
        "Bronze Present fragments + Gold",
        "Full Silver Present + Starter Archive renovation + passive Museum Gold income node",
        "Gold + small upgrade token chance",
        "Silver Present fragments + XP",
        "Silver Present fragments + Gold",
        "Gold + XP",
        "Full Silver Present + Rare Vault Stage 3 milestone eligibility",
        "Silver Present fragments + XP",
        "Silver fragments + Gold + XP",
        "Gold + XP",
        "Gold + small upgrade token chance",
        "Passive Museum Gold income node + Silver Present fragments",
        "Silver Present fragments + Gold",
        "Gold + XP",
        "Large Silver fragment bundle + passive-income capacity increase",
        "Silver Present fragments + XP",
        "Full Gold Present + Automated Acquisitions prerequisite + large passive Museum Gold income node",
        "Gold + XP",
        "Gold fragments + Gold + XP",
        "Gold Present fragments + XP",
        "Gold Present fragments + Gold",
        "Full Gold Present + premium display cosmetic",
        "Gold + small upgrade token chance",
        "Gold Present fragments + XP",
        "Large Gold fragment bundle + Museum cosmetic",
        "Gold + XP",
        "Passive Museum Gold income node + Gold Present fragments",
        "Gold Present fragments + XP",
        "Gold Present fragments + Gold",
        "Gold + XP",
        "Gold + small upgrade token chance",
        "Full Diamond Present + Premium Vault renovation + passive Museum Gold income node",
        "Diamond Present fragments + Gold",
        "Gold + XP",
        "Diamond fragments + Gold + XP",
        "Diamond Present fragments + XP",
        "Full Diamond Present + mythic display cosmetic",
        "Gold + XP",
        "Gold + small upgrade token chance",
        "Large Diamond fragment bundle + Trophy Room boost-cap increase",
        "Diamond Present fragments + Gold",
        "Passive Museum Gold income node + Diamond Present fragments",
        "Gold + small upgrade token chance",
        "Diamond Present fragments + XP",
        "Global Elite fragments + Museum cosmetic",
        "Gold + XP",
        "Full Global Elite Present + Mythic Gallery renovation + Rare Vault Stage 6 milestone eligibility",
        "Global Elite Present fragments + XP",
        "Global Elite Present fragments + Gold",
        "Global Elite fragments + Gold + XP",
        "Gold + small upgrade token chance",
        "Full Global Elite Present + prestige display cosmetic + passive diamond generation unlock",
        "Global Elite Present fragments + Gold",
        "Gold + XP",
        "Large Global Elite fragment bundle + final Curator cosmetic",
        "Global Elite Present fragments + XP",
        "3x Global Elite Presents + final Museum centerpiece + Curator prestige title + largest passive Museum Gold income node"
    };

    private static readonly HashSet<int> PassiveGoldSteps =
        new HashSet<int> { 10, 20, 25, 35, 40, 50, 55, 65, 70, 80 };

    public static readonly MuseumMilestone80Definition[] All =
        BuildAll();

    private static MuseumMilestone80Definition[] BuildAll()
    {
        MuseumMilestone80Definition[] result =
            new MuseumMilestone80Definition[FinalStep];

        for (int index = 0; index < FinalStep; index++)
        {
            int step = index + 1;
            MuseumMilestoneBand band = GetBand(step);
            MuseumMilestoneType type = GetType(step);

            result[index] = new MuseumMilestone80Definition
            {
                step = step,
                requiredMuseumPoints = RequiredPoints[index],
                band = band,
                milestoneType = type,
                displayName = GetDisplayName(step, band, type),
                rewardSummary = RewardSummaries[index],
                presentTier = GetPresentTier(step),
                notes = GetNotes(step, type),
                unlocksPassiveMuseumGold =
                    PassiveGoldSteps.Contains(step),
                unlocksPassiveDiamonds = step == 75,
                announcedSystemId = GetSystemId(step, band, type)
            };
        }

        return result;
    }

    public static List<MuseumMilestoneData> CreateRuntimePreviewMilestones()
    {
        List<MuseumMilestoneData> result =
            new List<MuseumMilestoneData>(All.Length);

        for (int i = 0; i < All.Length; i++)
        {
            MuseumMilestone80Definition definition = All[i];
            MuseumMilestoneData data =
                ScriptableObject.CreateInstance<MuseumMilestoneData>();

            data.hideFlags = HideFlags.DontSave;
            data.name = $"Runtime_MuseumMilestone_{definition.step:00}";
            data.milestoneId = definition.MilestoneId;
            data.stairNumber = definition.step;
            data.displayName = definition.displayName;
            data.description = definition.notes;
            data.majorMilestone = definition.IsMajor;
            data.band = definition.band;
            data.milestoneType = definition.milestoneType;
            data.rewardSummary = definition.rewardSummary;
            data.presentTier = definition.presentTier;
            data.requiredMuseumPoints = definition.requiredMuseumPoints;
            data.unlockedPlaqueId = definition.PlaqueId;
            data.unlocksPassiveMuseumGold =
                definition.unlocksPassiveMuseumGold;
            data.unlocksPassiveDiamonds =
                definition.unlocksPassiveDiamonds;
            data.announcedSystemId = definition.announcedSystemId;
            result.Add(data);
        }

        return result;
    }

    private static MuseumMilestoneBand GetBand(int step)
    {
        if (step <= 10) return MuseumMilestoneBand.DustyLobby;
        if (step <= 25) return MuseumMilestoneBand.StarterArchive;
        if (step <= 40) return MuseumMilestoneBand.CollectorHall;
        if (step <= 55) return MuseumMilestoneBand.PremiumVault;
        if (step <= 70) return MuseumMilestoneBand.MythicGallery;
        return MuseumMilestoneBand.GlobalExhibit;
    }

    private static MuseumMilestoneType GetType(int step)
    {
        if (step == 80) return MuseumMilestoneType.Finale;
        if (step == 40) return MuseumMilestoneType.SystemUnlock;
        if (step == 10 || step == 25 || step == 55 || step == 70)
            return MuseumMilestoneType.BandTransition;
        if (step == 20 || step == 35 || step == 50 || step == 65)
            return MuseumMilestoneType.IncomeNode;
        if (step == 5 || step == 15 || step == 30 ||
            step == 45 || step == 60 || step == 75)
            return MuseumMilestoneType.MajorPresent;
        return MuseumMilestoneType.Plaque;
    }

    private static string GetPresentTier(int step)
    {
        if (step <= 10) return "Dusty";
        if (step <= 25) return "Bronze";
        if (step <= 40) return "Silver";
        if (step <= 55) return "Gold";
        if (step <= 70) return "Diamond";
        return "Global Elite";
    }

    private static string GetDisplayName(
        int step,
        MuseumMilestoneBand band,
        MuseumMilestoneType type)
    {
        if (step == 40) return "Automated Acquisitions Prerequisite";
        if (step == 75) return "Diamond Endowment";
        if (step == 80) return "Global Museum Completion";

        string bandName = GetBandName(band);

        if (type == MuseumMilestoneType.BandTransition)
            return bandName + " Completion";
        if (type == MuseumMilestoneType.IncomeNode)
            return bandName + " Income Node";
        if (type == MuseumMilestoneType.MajorPresent)
            return bandName + " Major Milestone";
        return bandName + " — Step " + step;
    }

    private static string GetNotes(
        int step,
        MuseumMilestoneType type)
    {
        if (step == 1)
            return "Tutorial claim; player should reach this quickly.";
        if (step == 40)
            return "Automated Acquisitions also requires Global Elite V.";
        if (step == 75)
            return "Unlocks very slow, capped passive diamond generation. " +
                   "The rate and capacity are configured in M5.";
        if (step == 80)
            return "5M Museum-completion endpoint. Normalized collection " +
                   "completion bonuses make the endpoint achievable.";
        if (type == MuseumMilestoneType.Plaque)
            return "Small claim plaque.";
        return "Large claim plaque and major Museum progression moment.";
    }

    private static string GetSystemId(
        int step,
        MuseumMilestoneBand band,
        MuseumMilestoneType type)
    {
        if (step == 40) return "automated-acquisitions";
        if (step == 75) return "passive-diamond-generation";
        if (step == 80) return "museum-complete";
        if (type == MuseumMilestoneType.BandTransition)
        {
            return "museum-band-" +
                   GetBandName(band).ToLowerInvariant().Replace(" ", "-") +
                   "-complete";
        }

        return "";
    }

    private static string GetBandName(MuseumMilestoneBand band)
    {
        switch (band)
        {
            case MuseumMilestoneBand.DustyLobby: return "Dusty Lobby";
            case MuseumMilestoneBand.StarterArchive: return "Starter Archive";
            case MuseumMilestoneBand.CollectorHall: return "Collector Hall";
            case MuseumMilestoneBand.PremiumVault: return "Premium Vault";
            case MuseumMilestoneBand.MythicGallery: return "Mythic Gallery";
            case MuseumMilestoneBand.GlobalExhibit: return "Global Exhibit";
            default: return band.ToString();
        }
    }
}
