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
    [Min(0f)] public double baseMuseumPoints = 1d;
}

[Serializable]
public class MuseumWearPointRule
{
    public MuseumWearTier wear;
    [Min(0f)] public double pointMultiplier = 1d;
}

[Serializable]
public class MuseumVariantPointRule
{
    public MuseumDonationVariant variant;
    [Min(0f)] public double pointMultiplier = 1d;
}

[Serializable]
public class MuseumRarityWearPointRule
{
    public Rarity rarity;

    [Min(0f)] public double battleScarred = 1d;
    [Min(0f)] public double wellWorn = 1d;
    [Min(0f)] public double fieldTested = 1d;
    [Min(0f)] public double minimalWear = 1d;
    [Min(0f)] public double factoryNew = 1d;

    public double GetPoints(MuseumWearTier wear)
    {
        switch (wear)
        {
            case MuseumWearTier.FactoryNew: return Math.Max(0d, factoryNew);
            case MuseumWearTier.MinimalWear: return Math.Max(0d, minimalWear);
            case MuseumWearTier.FieldTested: return Math.Max(0d, fieldTested);
            case MuseumWearTier.WellWorn: return Math.Max(0d, wellWorn);
            default: return Math.Max(0d, battleScarred);
        }
    }
}

[Serializable]
public class MuseumMarketValueBonusSettings
{
    [Tooltip("Adds a separate Museum Point bonus for unusually valuable donated items.")]
    public bool enabled = true;

    [Tooltip("No market-value bonus is awarded below this value.")]
    [Min(0f)] public double minimumMarketValue = 100d;

    [Tooltip("Market values at or above this value receive the maximum bonus.")]
    [Min(0f)] public double maximumMarketValue = 10000d;

    [Tooltip("Bonus awarded at Minimum Market Value. 100 Gold -> 25 MP by default.")]
    [Min(0f)] public double bonusAtMinimumValue = 25d;

    [Tooltip("Maximum value bonus. 10,000 Gold -> 100 MP by default.")]
    [Min(0f)] public double maximumBonusPoints = 100d;
}

[Serializable]
public class MuseumIdleMilestoneModifier
{
    [Tooltip("Stable Museum milestone ID, for example museum-step-40.")]
    public string milestoneId;

    [Tooltip(
        "Gold-income node weight added when this milestone is claimed. Normal " +
        "nodes use 1, the large step-40 node uses 2 and the finale uses 3.")]
    [Min(0f)] public float goldNodeWeight;

    [Tooltip(
        "Additive multiplier bonus to the base unclaimed-Gold capacity. A value " +
        "of 0.5 raises the capacity by 50%.")]
    [Min(0f)] public float goldCapacityMultiplierBonus;

    [Tooltip("Extra eligible offline hours added while this milestone is claimed.")]
    [Min(0f)] public float offlineHoursBonus;
}

[Serializable]
public class MuseumIdleIncomeSettings
{
    [Header("Visitor Gold")]
    [Tooltip(
        "Gold generated per Museum Point per real-world hour, before claimed " +
        "income-node weight is applied.")]
    [Min(0f)] public double goldPerMuseumPointPerHour = 0.000005d;

    [Tooltip(
        "Base maximum unclaimed Museum Gold. Zero removes the Museum-specific " +
        "Gold capacity.")]
    [Min(0f)] public double unclaimedGoldCapacity = 2500d;

    [Header("Diamond Endowment")]
    [Tooltip(
        "Diamonds generated per real-world hour after the passive-diamond " +
        "milestone has been claimed.")]
    [Min(0f)] public double diamondsPerHour = 0.05d;

    [Tooltip(
        "Maximum fractional unclaimed diamonds. Whole diamonds are granted on " +
        "claim; the remaining fraction stays stored.")]
    [Min(0f)] public double unclaimedDiamondCapacity = 3d;

    [Header("Time Rules")]
    [Tooltip("Maximum offline duration eligible for Museum income.")]
    [Min(0f)] public float maximumOfflineHours = 8f;

    [Tooltip(
        "Smallest elapsed time used by the income calculator. This prevents " +
        "excessive save-state updates from tiny time differences.")]
    [Min(0f)] public float minimumCalculationIntervalSeconds = 30f;

    [Header("Milestone Modifiers")]
    [Tooltip(
        "Optional data-driven node weights, capacity bonuses and offline-hour " +
        "bonuses keyed by stable Museum milestone ID.")]
    public List<MuseumIdleMilestoneModifier> milestoneModifiers =
        new List<MuseumIdleMilestoneModifier>();

    public MuseumIdleMilestoneModifier GetModifier(string milestoneId)
    {
        if (string.IsNullOrWhiteSpace(milestoneId) ||
            milestoneModifiers == null)
        {
            return null;
        }

        for (int i = 0; i < milestoneModifiers.Count; i++)
        {
            MuseumIdleMilestoneModifier modifier = milestoneModifiers[i];

            if (modifier != null &&
                string.Equals(
                    modifier.milestoneId,
                    milestoneId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return modifier;
            }
        }

        return null;
    }
}

/// <summary>
/// Authoritative tuning asset for Museum donation points and passive income.
/// One permanent slot exists per SkinData + wear + variant.
/// </summary>
[CreateAssetMenu(
    fileName = "MuseumBalanceData",
    menuName = "Case Curator/Museum/Museum Balance")]
public class MuseumBalanceData : ScriptableObject
{
    [Header("Donation Slot Rules")]
    public bool oneDonationPerSlot = true;
    public bool includeNormalSlots = true;
    public bool includeStatTrakSlots = true;
    public bool includeSouvenirSlots = true;
    public bool includeVanillaSlots = true;

    [Header("Rarity + Wear Point Matrix")]
    [Tooltip(
        "Optional overrides for the built-in Case Curator rarity/wear matrix. " +
        "Missing rarities use the built-in defaults.")]
    public List<MuseumRarityWearPointRule> rarityWearPointRules =
        new List<MuseumRarityWearPointRule>();

    [Header("Variant Multipliers")]
    [Tooltip(
        "Optional overrides. Missing values default to Normal x1.00, StatTrak " +
        "x1.50 and Souvenir x1.50.")]
    public List<MuseumVariantPointRule> variantPointRules =
        new List<MuseumVariantPointRule>();

    [Tooltip("Additional multiplier applied to vanilla slots.")]
    [Min(0f)] public double vanillaPointMultiplier = 1d;

    [Tooltip("Additional multiplier applied to knife and glove slots.")]
    [Min(0f)] public double rareSpecialPointMultiplier = 1d;

    [Header("Market Value Bonus")]
    public MuseumMarketValueBonusSettings marketValueBonus =
        new MuseumMarketValueBonusSettings();

    [Header("Legacy Point Fields")]
    [Tooltip("Retained so existing assets deserialize safely. M3 uses the matrix above.")]
    public List<MuseumRarityPointRule> rarityPointRules =
        new List<MuseumRarityPointRule>();

    public List<MuseumWearPointRule> wearPointRules =
        new List<MuseumWearPointRule>();

    [Min(0f)] public double defaultBaseMuseumPoints = 1d;

    [Header("Passive Income")]
    public MuseumIdleIncomeSettings idleIncome =
        new MuseumIdleIncomeSettings();

    public double GetRarityWearPoints(
        Rarity rarity,
        MuseumWearTier wear,
        bool isVanilla)
    {
        if (rarityWearPointRules != null)
        {
            for (int i = 0; i < rarityWearPointRules.Count; i++)
            {
                MuseumRarityWearPointRule rule = rarityWearPointRules[i];

                if (rule != null && rule.rarity == rarity)
                    return rule.GetPoints(wear);
            }
        }

        return GetDefaultRarityWearPoints(rarity, wear, isVanilla);
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

        switch (variant)
        {
            case MuseumDonationVariant.StatTrak: return 1.5d;
            case MuseumDonationVariant.Souvenir: return 1.5d;
            default: return 1d;
        }
    }

    public double CalculateMarketValueBonus(double marketValue)
    {
        if (marketValueBonus == null || !marketValueBonus.enabled)
            return 0d;

        double minimumValue = Math.Max(0d, marketValueBonus.minimumMarketValue);
        double maximumValue = Math.Max(minimumValue, marketValueBonus.maximumMarketValue);
        double minimumBonus = Math.Max(0d, marketValueBonus.bonusAtMinimumValue);
        double maximumBonus = Math.Max(minimumBonus, marketValueBonus.maximumBonusPoints);

        if (marketValue < minimumValue || minimumValue <= 0d)
            return 0d;

        if (marketValue >= maximumValue || maximumValue <= minimumValue)
            return maximumBonus;

        double denominator = Math.Log10(maximumValue / minimumValue);

        if (denominator <= 0d)
            return maximumBonus;

        double progress = Math.Log10(marketValue / minimumValue) / denominator;
        progress = Math.Max(0d, Math.Min(1d, progress));

        return minimumBonus + (maximumBonus - minimumBonus) * progress;
    }

    public double GetEffectiveMarketBonusRate(double marketValue)
    {
        return marketValue > 0d
            ? CalculateMarketValueBonus(marketValue) / marketValue
            : 0d;
    }

    public double CalculateBaseSlotPoints(
        Rarity rarity,
        MuseumWearTier wear,
        MuseumDonationVariant variant,
        bool isVanilla)
    {
        double points =
            GetRarityWearPoints(rarity, wear, isVanilla) *
            GetVariantMultiplier(variant);

        if (rarity == Rarity.RareSpecial)
            points *= Math.Max(0d, rareSpecialPointMultiplier);

        if (isVanilla)
            points *= Math.Max(0d, vanillaPointMultiplier);

        return Math.Max(0d, points);
    }

    // Compatibility methods retained for older callers and inspectors.
    public double GetBasePoints(Rarity rarity)
    {
        return GetRarityWearPoints(rarity, MuseumWearTier.FactoryNew, false);
    }

    public double GetWearMultiplier(MuseumWearTier wear)
    {
        return 1d;
    }

    private static double GetDefaultRarityWearPoints(
        Rarity rarity,
        MuseumWearTier wear,
        bool isVanilla)
    {
        if (isVanilla)
            wear = MuseumWearTier.FactoryNew;

        switch (rarity)
        {
            case Rarity.Consumer:
                return PickWear(wear, 1d, 1d, 2d, 3d, 4d);
            case Rarity.Industrial:
                return PickWear(wear, 2d, 3d, 4d, 6d, 8d);
            case Rarity.MilSpec:
                return PickWear(wear, 3d, 4d, 6d, 9d, 12d);
            case Rarity.Restricted:
                return PickWear(wear, 6d, 8d, 12d, 18d, 24d);
            case Rarity.Classified:
                return PickWear(wear, 12d, 16d, 24d, 36d, 48d);
            case Rarity.Covert:
                return PickWear(wear, 25d, 35d, 50d, 75d, 85d);
            case Rarity.RareSpecial:
                return PickWear(wear, 50d, 70d, 85d, 110d, 150d);
            default:
                return 1d;
        }
    }

    private static double PickWear(
        MuseumWearTier wear,
        double battleScarred,
        double wellWorn,
        double fieldTested,
        double minimalWear,
        double factoryNew)
    {
        switch (wear)
        {
            case MuseumWearTier.FactoryNew: return factoryNew;
            case MuseumWearTier.MinimalWear: return minimalWear;
            case MuseumWearTier.FieldTested: return fieldTested;
            case MuseumWearTier.WellWorn: return wellWorn;
            default: return battleScarred;
        }
    }

    private void OnValidate()
    {
        if (rarityWearPointRules == null)
            rarityWearPointRules = new List<MuseumRarityWearPointRule>();

        if (variantPointRules == null)
            variantPointRules = new List<MuseumVariantPointRule>();

        if (rarityPointRules == null)
            rarityPointRules = new List<MuseumRarityPointRule>();

        if (wearPointRules == null)
            wearPointRules = new List<MuseumWearPointRule>();

        if (marketValueBonus == null)
            marketValueBonus = new MuseumMarketValueBonusSettings();

        if (idleIncome == null)
            idleIncome = new MuseumIdleIncomeSettings();

        if (idleIncome.milestoneModifiers == null)
        {
            idleIncome.milestoneModifiers =
                new List<MuseumIdleMilestoneModifier>();
        }

        defaultBaseMuseumPoints = Math.Max(0d, defaultBaseMuseumPoints);
        vanillaPointMultiplier = Math.Max(0d, vanillaPointMultiplier);
        rareSpecialPointMultiplier = Math.Max(0d, rareSpecialPointMultiplier);

        marketValueBonus.minimumMarketValue =
            Math.Max(0d, marketValueBonus.minimumMarketValue);
        marketValueBonus.maximumMarketValue =
            Math.Max(
                marketValueBonus.minimumMarketValue,
                marketValueBonus.maximumMarketValue);
        marketValueBonus.bonusAtMinimumValue =
            Math.Max(0d, marketValueBonus.bonusAtMinimumValue);
        marketValueBonus.maximumBonusPoints =
            Math.Max(
                marketValueBonus.bonusAtMinimumValue,
                marketValueBonus.maximumBonusPoints);

        idleIncome.goldPerMuseumPointPerHour =
            Math.Max(0d, idleIncome.goldPerMuseumPointPerHour);
        idleIncome.unclaimedGoldCapacity =
            Math.Max(0d, idleIncome.unclaimedGoldCapacity);
        idleIncome.diamondsPerHour =
            Math.Max(0d, idleIncome.diamondsPerHour);
        idleIncome.unclaimedDiamondCapacity =
            Math.Max(0d, idleIncome.unclaimedDiamondCapacity);
        idleIncome.maximumOfflineHours =
            Mathf.Max(0f, idleIncome.maximumOfflineHours);
        idleIncome.minimumCalculationIntervalSeconds =
            Mathf.Max(0f, idleIncome.minimumCalculationIntervalSeconds);

        for (int i = idleIncome.milestoneModifiers.Count - 1; i >= 0; i--)
        {
            MuseumIdleMilestoneModifier modifier =
                idleIncome.milestoneModifiers[i];

            if (modifier == null)
            {
                idleIncome.milestoneModifiers.RemoveAt(i);
                continue;
            }

            modifier.milestoneId =
                modifier.milestoneId != null
                    ? modifier.milestoneId.Trim()
                    : "";
            modifier.goldNodeWeight =
                Mathf.Max(0f, modifier.goldNodeWeight);
            modifier.goldCapacityMultiplierBonus =
                Mathf.Max(0f, modifier.goldCapacityMultiplierBonus);
            modifier.offlineHoursBonus =
                Mathf.Max(0f, modifier.offlineHoursBonus);
        }
    }
}
