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
    [Min(0f)]
    public double vanillaPointMultiplier = 1d;

    [Tooltip("Additional multiplier applied to knife and glove slots.")]
    [Min(0f)]
    public double rareSpecialPointMultiplier = 1d;

    [Header("Market Value Bonus")]
    public MuseumMarketValueBonusSettings marketValueBonus =
        new MuseumMarketValueBonusSettings();

    [Header("Legacy Point Fields")]
    [Tooltip("Retained so existing assets deserialize safely. M3 uses the matrix above.")]
    public List<MuseumRarityPointRule> rarityPointRules =
        new List<MuseumRarityPointRule>();

    public List<MuseumWearPointRule> wearPointRules =
        new List<MuseumWearPointRule>();

    [Min(0f)]
    public double defaultBaseMuseumPoints = 1d;

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

        // Logarithmic interpolation keeps the reward increasing while its
        // effective percentage diminishes: 100 Gold -> 25 MP, 10,000 -> 100 MP.
        double denominator = Math.Log10(maximumValue / minimumValue);

        if (denominator <= 0d)
            return maximumBonus;

        double progress = Math.Log10(marketValue / minimumValue) / denominator;
        progress = Math.Max(0d, Math.Min(1d, progress));

        return minimumBonus + (maximumBonus - minimumBonus) * progress;
    }

    public double GetEffectiveMarketBonusRate(double marketValue)
    {
        if (marketValue <= 0d)
            return 0d;

        return CalculateMarketValueBonus(marketValue) / marketValue;
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
    }
}
