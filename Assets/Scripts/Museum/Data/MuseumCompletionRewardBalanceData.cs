using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MuseumSkinCompletionRarityRule
{
    public Rarity rarity;

    [Tooltip("Multiplier applied after the variation-count multiplier.")]
    [Min(0f)] public double qualityMultiplier = 1d;

    [Tooltip("Maximum one-time reward for completing one skin in one Museum wing.")]
    [Min(0f)] public double maximumReward = 1000d;
}

[Serializable]
public class MuseumWeaponCompletionRewardTier
{
    [Min(1)] public int minimumSlots = 1;

    [Tooltip("Zero means that this tier has no upper limit.")]
    [Min(0)] public int maximumSlots;

    [Min(0f)] public double rewardMuseumPoints;

    public bool Matches(int slotCount)
    {
        int count = Math.Max(0, slotCount);

        return count >= Math.Max(1, minimumSlots) &&
               (maximumSlots <= 0 || count <= maximumSlots);
    }
}

[Serializable]
public class MuseumCategoryCompletionRewardRule
{
    [Tooltip("Stable Museum wing ID.")]
    public string wingId;

    [Tooltip("Stable Museum category ID.")]
    public string categoryId;

    [Min(0f)] public double rewardMuseumPoints;
}

/// <summary>
/// Data-driven one-time completion rewards for Museum skins, weapons and
/// categories. The asset is loaded from Resources/Museum so the balance can be
/// changed without adding another reference to GameDatabase.
/// </summary>
[CreateAssetMenu(
    fileName = "MuseumCompletionRewardBalance",
    menuName = "Case Curator/Museum/Completion Reward Balance")]
public class MuseumCompletionRewardBalanceData : ScriptableObject
{
    public const string ResourcesPath =
        "Museum/MuseumCompletionRewardBalance";

    [Header("Skin Completion - Normal / StatTrak / Souvenir")]
    [Tooltip(
        "Multiplier indexed by required variation count. Element 0 is one " +
        "variation and element 9 is ten variations.")]
    public List<double> normalVariationMultipliers =
        new List<double>();

    [Header("Skin Completion - Knives / Gloves")]
    [Tooltip(
        "Rare Special variation multipliers. The default ten-variation value " +
        "is x30 so a premium completed knife/glove is normally worth about " +
        "6,000-10,000 MP, subject to its actual highest donation.")]
    public List<double> rareSpecialVariationMultipliers =
        new List<double>();

    [Header("Skin Quality Multipliers + Caps")]
    public List<MuseumSkinCompletionRarityRule> rarityRules =
        new List<MuseumSkinCompletionRarityRule>();

    [Header("Weapon Completion")]
    public List<MuseumWeaponCompletionRewardTier> weaponRewardTiers =
        new List<MuseumWeaponCompletionRewardTier>();

    [Header("Category Completion")]
    [Tooltip(
        "Fixed one-time rewards keyed by wing and category. These are intentionally " +
        "manual because the category sizes differ heavily.")]
    public List<MuseumCategoryCompletionRewardRule> categoryRewardRules =
        new List<MuseumCategoryCompletionRewardRule>();

    [Header("Presentation")]
    [Min(0.25f)] public float claimNotificationSeconds = 2.75f;

    public void ResetToDefaults()
    {
        normalVariationMultipliers = new List<double>
        {
            2d, 2.5d, 3d, 3.5d, 4d,
            4.5d, 5d, 5.5d, 6d, 6.5d
        };

        rareSpecialVariationMultipliers = new List<double>
        {
            8d, 10d, 12d, 14d, 16d,
            18d, 20d, 22d, 25d, 30d
        };

        rarityRules = new List<MuseumSkinCompletionRarityRule>
        {
            RarityRule(Rarity.Consumer, 0.60d, 1000d),
            RarityRule(Rarity.Industrial, 0.75d, 1250d),
            RarityRule(Rarity.MilSpec, 0.90d, 1500d),
            RarityRule(Rarity.Restricted, 1.00d, 1800d),
            RarityRule(Rarity.Classified, 1.15d, 2200d),
            RarityRule(Rarity.Covert, 1.30d, 3000d),
            // Rare Special already has the much larger variation curve.
            RarityRule(Rarity.RareSpecial, 1.00d, 12000d)
        };

        weaponRewardTiers = new List<MuseumWeaponCompletionRewardTier>
        {
            WeaponTier(1, 50, 5000d),
            WeaponTier(51, 250, 25000d),
            WeaponTier(251, 400, 40000d),
            WeaponTier(401, 0, 60000d)
        };

        categoryRewardRules = new List<MuseumCategoryCompletionRewardRule>
        {
            CategoryRule(
                MuseumUnlockProgressionUtility.ArsenalWingId,
                "museum-arsenal-pistols",
                150000d),
            CategoryRule(
                MuseumUnlockProgressionUtility.ArsenalWingId,
                "museum-arsenal-smgs",
                102500d),
            CategoryRule(
                MuseumUnlockProgressionUtility.ArsenalWingId,
                "museum-arsenal-machine-guns",
                37500d),
            CategoryRule(
                MuseumUnlockProgressionUtility.ArsenalWingId,
                "museum-arsenal-shotguns",
                77500d),
            CategoryRule(
                MuseumUnlockProgressionUtility.ArsenalWingId,
                "museum-arsenal-rifles",
                102500d),
            CategoryRule(
                MuseumUnlockProgressionUtility.ArsenalWingId,
                "museum-arsenal-snipers",
                77500d),
            CategoryRule(
                MuseumUnlockProgressionUtility.ArsenalWingId,
                "museum-arsenal-equipment",
                4000d),
            CategoryRule(
                MuseumUnlockProgressionUtility.RareSpecialVaultWingId,
                "museum-rare-vault-knives",
                157500d),
            CategoryRule(
                MuseumUnlockProgressionUtility.RareSpecialVaultWingId,
                "museum-rare-vault-gloves",
                42500d)
        };

        claimNotificationSeconds = 2.75f;
    }

    public double CalculateSkinReward(
        double highestActualDonationPoints,
        int variationCount,
        Rarity rarity)
    {
        double highest = Math.Max(0d, highestActualDonationPoints);

        if (highest <= 0d || variationCount <= 0)
            return 0d;

        bool rareSpecial = rarity == Rarity.RareSpecial;
        double variationMultiplier = GetVariationMultiplier(
            variationCount,
            rareSpecial);
        MuseumSkinCompletionRarityRule rarityRule = GetRarityRule(rarity);
        double qualityMultiplier = rarityRule != null
            ? Math.Max(0d, rarityRule.qualityMultiplier)
            : 1d;
        double cap = rarityRule != null
            ? Math.Max(0d, rarityRule.maximumReward)
            : rareSpecial ? 12000d : 1000d;

        double reward = Math.Ceiling(
            highest * variationMultiplier * qualityMultiplier);

        return cap > 0d
            ? Math.Min(cap, Math.Max(0d, reward))
            : Math.Max(0d, reward);
    }

    public double GetWeaponReward(int totalSlots)
    {
        if (weaponRewardTiers == null || totalSlots <= 0)
            return 0d;

        for (int i = 0; i < weaponRewardTiers.Count; i++)
        {
            MuseumWeaponCompletionRewardTier tier = weaponRewardTiers[i];

            if (tier != null && tier.Matches(totalSlots))
                return Math.Max(0d, tier.rewardMuseumPoints);
        }

        return 0d;
    }

    public double GetCategoryReward(
        string wingId,
        string categoryId)
    {
        if (categoryRewardRules == null ||
            string.IsNullOrWhiteSpace(wingId) ||
            string.IsNullOrWhiteSpace(categoryId))
        {
            return 0d;
        }

        for (int i = 0; i < categoryRewardRules.Count; i++)
        {
            MuseumCategoryCompletionRewardRule rule = categoryRewardRules[i];

            if (rule != null &&
                string.Equals(
                    rule.wingId,
                    wingId,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    rule.categoryId,
                    categoryId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Math.Max(0d, rule.rewardMuseumPoints);
            }
        }

        return 0d;
    }

    private double GetVariationMultiplier(
        int variationCount,
        bool rareSpecial)
    {
        List<double> values = rareSpecial
            ? rareSpecialVariationMultipliers
            : normalVariationMultipliers;

        if (values == null || values.Count == 0)
            return 1d;

        int index = Mathf.Clamp(variationCount - 1, 0, values.Count - 1);
        return Math.Max(0d, values[index]);
    }

    private MuseumSkinCompletionRarityRule GetRarityRule(Rarity rarity)
    {
        if (rarityRules == null)
            return null;

        for (int i = 0; i < rarityRules.Count; i++)
        {
            MuseumSkinCompletionRarityRule rule = rarityRules[i];

            if (rule != null && rule.rarity == rarity)
                return rule;
        }

        return null;
    }

    private static MuseumSkinCompletionRarityRule RarityRule(
        Rarity rarity,
        double multiplier,
        double cap)
    {
        return new MuseumSkinCompletionRarityRule
        {
            rarity = rarity,
            qualityMultiplier = multiplier,
            maximumReward = cap
        };
    }

    private static MuseumWeaponCompletionRewardTier WeaponTier(
        int minimum,
        int maximum,
        double reward)
    {
        return new MuseumWeaponCompletionRewardTier
        {
            minimumSlots = minimum,
            maximumSlots = maximum,
            rewardMuseumPoints = reward
        };
    }

    private static MuseumCategoryCompletionRewardRule CategoryRule(
        string wingId,
        string categoryId,
        double reward)
    {
        return new MuseumCategoryCompletionRewardRule
        {
            wingId = wingId,
            categoryId = categoryId,
            rewardMuseumPoints = reward
        };
    }

    private void OnValidate()
    {
        if (normalVariationMultipliers == null)
            normalVariationMultipliers = new List<double>();

        if (rareSpecialVariationMultipliers == null)
            rareSpecialVariationMultipliers = new List<double>();

        if (rarityRules == null)
            rarityRules = new List<MuseumSkinCompletionRarityRule>();

        if (weaponRewardTiers == null)
            weaponRewardTiers = new List<MuseumWeaponCompletionRewardTier>();

        if (categoryRewardRules == null)
        {
            categoryRewardRules =
                new List<MuseumCategoryCompletionRewardRule>();
        }

        for (int i = 0; i < normalVariationMultipliers.Count; i++)
            normalVariationMultipliers[i] =
                Math.Max(0d, normalVariationMultipliers[i]);

        for (int i = 0; i < rareSpecialVariationMultipliers.Count; i++)
            rareSpecialVariationMultipliers[i] =
                Math.Max(0d, rareSpecialVariationMultipliers[i]);

        for (int i = 0; i < rarityRules.Count; i++)
        {
            MuseumSkinCompletionRarityRule rule = rarityRules[i];

            if (rule == null)
                continue;

            rule.qualityMultiplier = Math.Max(0d, rule.qualityMultiplier);
            rule.maximumReward = Math.Max(0d, rule.maximumReward);
        }

        for (int i = 0; i < weaponRewardTiers.Count; i++)
        {
            MuseumWeaponCompletionRewardTier tier = weaponRewardTiers[i];

            if (tier == null)
                continue;

            tier.minimumSlots = Math.Max(1, tier.minimumSlots);
            tier.maximumSlots = Math.Max(0, tier.maximumSlots);
            tier.rewardMuseumPoints = Math.Max(0d, tier.rewardMuseumPoints);
        }

        for (int i = 0; i < categoryRewardRules.Count; i++)
        {
            MuseumCategoryCompletionRewardRule rule = categoryRewardRules[i];

            if (rule == null)
                continue;

            rule.wingId = rule.wingId != null ? rule.wingId.Trim() : "";
            rule.categoryId =
                rule.categoryId != null ? rule.categoryId.Trim() : "";
            rule.rewardMuseumPoints = Math.Max(0d, rule.rewardMuseumPoints);
        }

        claimNotificationSeconds = Mathf.Max(0.25f, claimNotificationSeconds);
    }
}
