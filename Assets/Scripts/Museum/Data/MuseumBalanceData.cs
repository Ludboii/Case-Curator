using System;
using System.Collections.Generic;
using UnityEngine;

public enum MuseumWearTier
{
    FactoryNew = 0,
    MinimalWear = 1,
    FieldTested = 2,
    WellWorn = 3,
    BattleScarred = 4
}

public enum MuseumDonationVariant
{
    Normal = 0,
    StatTrak = 1,
    Souvenir = 2
}

[Serializable]
public class MuseumRarityPointRule
{
    public Rarity rarity;

    [Min(0f)]
    public double baseMuseumPoints = 1d;
}

[Serializable]
public class MuseumWearPointRule
{
    public MuseumWearTier wear;

    [Min(0f)]
    public double pointMultiplier = 1d;
}

[Serializable]
public class MuseumVariantPointRule
{
    public MuseumDonationVariant variant;

    [Min(0f)]
    public double pointMultiplier = 1d;
}

[Serializable]
public class MuseumIdleIncomeSettings
{
    [Tooltip(
        "Gold generated per Museum Point per real-world hour. Leave at zero " +
        "until passive Museum income is enabled and balanced.")]
    [Min(0f)]
    public double goldPerMuseumPointPerHour;

    [Tooltip("Maximum offline duration eligible for Museum income.")]
    [Min(0f)]
    public float maximumOfflineHours = 8f;

    [Tooltip(
        "Maximum unclaimed Museum Gold before passive generation pauses. " +
        "Zero means no Museum-specific cap.")]
    [Min(0f)]
    public float unclaimedGoldCapacity;

    [Tooltip(
        "Smallest elapsed time used by the future income calculator. This " +
        "prevents excessive recalculation from tiny time differences.")]
    [Min(0f)]
    public float minimumCalculationIntervalSeconds = 30f;
}

/// <summary>
/// Authoritative tuning asset for Museum donation points and passive income.
/// The Museum uses one permanent slot per SkinData + wear + variant. Market
/// value is intentionally not part of the base point formula.
/// </summary>
[CreateAssetMenu(
    fileName = "MuseumBalanceData",
    menuName = "Case Curator/Museum/Museum Balance")]
public class MuseumBalanceData : ScriptableObject
{
    [Header("Donation Slot Rules")]
    [Tooltip(
        "Case Curator's Museum design allows one donation per exact SkinData + " +
        "wear + variant slot. Keep enabled unless the design is intentionally " +
        "changed later.")]
    public bool oneDonationPerSlot = true;

    public bool includeNormalSlots = true;
    public bool includeStatTrakSlots = true;
    public bool includeSouvenirSlots = true;
    public bool includeVanillaSlots = true;

    [Header("Point Rules")]
    [Tooltip(
        "Base points by rarity. Missing rarities use Default Base Museum Points.")]
    public List<MuseumRarityPointRule> rarityPointRules =
        new List<MuseumRarityPointRule>();

    [Min(0f)]
    public double defaultBaseMuseumPoints = 1d;

    [Tooltip(
        "Multipliers for Factory New through Battle-Scarred Museum slots. " +
        "Missing wear entries use a multiplier of 1.")]
    public List<MuseumWearPointRule> wearPointRules =
        new List<MuseumWearPointRule>();

    [Tooltip(
        "Normal, StatTrak and Souvenir multipliers. Missing variants use a " +
        "multiplier of 1.")]
    public List<MuseumVariantPointRule> variantPointRules =
        new List<MuseumVariantPointRule>();

    [Tooltip("Additional multiplier applied to vanilla Rare Special slots.")]
    [Min(0f)]
    public double vanillaPointMultiplier = 1d;

    [Tooltip("Additional multiplier applied to knife and glove slots.")]
    [Min(0f)]
    public double rareSpecialPointMultiplier = 1d;

    [Header("Passive Income")]
    public MuseumIdleIncomeSettings idleIncome =
        new MuseumIdleIncomeSettings();

    public double GetBasePoints(Rarity rarity)
    {
        if (rarityPointRules != null)
        {
            for (int i = 0; i < rarityPointRules.Count; i++)
            {
                MuseumRarityPointRule rule = rarityPointRules[i];

                if (rule != null && rule.rarity == rarity)
                    return Math.Max(0d, rule.baseMuseumPoints);
            }
        }

        return Math.Max(0d, defaultBaseMuseumPoints);
    }

    public double GetWearMultiplier(MuseumWearTier wear)
    {
        if (wearPointRules != null)
        {
            for (int i = 0; i < wearPointRules.Count; i++)
            {
                MuseumWearPointRule rule = wearPointRules[i];

                if (rule != null && rule.wear == wear)
                    return Math.Max(0d, rule.pointMultiplier);
            }
        }

        return 1d;
    }

    public double GetVariantMultiplier(MuseumDonationVariant variant)
    {
        if (variantPointRules != null)
        {
            for (int i = 0; i < variantPointRules.Count; i++)
            {
                MuseumVariantPointRule rule = variantPointRules[i];

                if (rule != null && rule.variant == variant)
                    return Math.Max(0d, rule.pointMultiplier);
            }
        }

        return 1d;
    }

    public double CalculateBaseSlotPoints(
        Rarity rarity,
        MuseumWearTier wear,
        MuseumDonationVariant variant,
        bool isVanilla)
    {
        double points =
            GetBasePoints(rarity) *
            GetWearMultiplier(wear) *
            GetVariantMultiplier(variant);

        if (rarity == Rarity.RareSpecial)
            points *= Math.Max(0d, rareSpecialPointMultiplier);

        if (isVanilla)
            points *= Math.Max(0d, vanillaPointMultiplier);

        return Math.Max(0d, points);
    }

    private void OnValidate()
    {
        if (rarityPointRules == null)
            rarityPointRules = new List<MuseumRarityPointRule>();

        if (wearPointRules == null)
            wearPointRules = new List<MuseumWearPointRule>();

        if (variantPointRules == null)
            variantPointRules = new List<MuseumVariantPointRule>();

        if (idleIncome == null)
            idleIncome = new MuseumIdleIncomeSettings();

        defaultBaseMuseumPoints =
            Math.Max(0d, defaultBaseMuseumPoints);

        vanillaPointMultiplier =
            Math.Max(0d, vanillaPointMultiplier);

        rareSpecialPointMultiplier =
            Math.Max(0d, rareSpecialPointMultiplier);
    }
}
