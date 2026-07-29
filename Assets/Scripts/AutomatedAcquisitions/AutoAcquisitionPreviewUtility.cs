using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class AutoAcquisitionSkinPreview
{
    public SkinData skin;
    public float automatedChancePercent;
    public float manualChancePercent;
    public double expectedAutomatedFloat;
    public double expectedManualFloat;
    public bool canBeStatTrak;
    public bool souvenir;
}

public static class AutoAcquisitionPreviewUtility
{
    public static List<AutoAcquisitionSkinPreview> Build(CaseData container)
    {
        List<AutoAcquisitionSkinPreview> result = new List<AutoAcquisitionSkinPreview>();

        if (container == null || container.dropPool == null || container.dropPool.Count == 0)
            return result;

        Dictionary<Rarity, float> manualRarityWeights = BuildManualRarityWeights(container);
        Dictionary<Rarity, float> automatedRarityWeights = new Dictionary<Rarity, float>();
        Rarity lowest = FindLowestRarity(container);
        float calibration = AutoAcquisitionUpgradeUtility.GetCalibrationMultiplier();
        float manualTotal = 0f;
        float automatedTotal = 0f;

        foreach (KeyValuePair<Rarity, float> pair in manualRarityWeights)
        {
            float manual = Mathf.Max(0f, pair.Value);
            float automated = pair.Key == lowest ? manual : manual * calibration;
            automatedRarityWeights[pair.Key] = automated;
            manualTotal += manual;
            automatedTotal += automated;
        }

        Dictionary<Rarity, float> dropWeightByRarity = new Dictionary<Rarity, float>();
        Dictionary<Rarity, int> dropCountByRarity = new Dictionary<Rarity, int>();

        for (int i = 0; i < container.dropPool.Count; i++)
        {
            WeightedDrop drop = container.dropPool[i];
            if (!IsEligible(container, drop))
                continue;

            Rarity rarity = drop.skin.rarity;
            if (!dropWeightByRarity.ContainsKey(rarity))
            {
                dropWeightByRarity.Add(rarity, 0f);
                dropCountByRarity.Add(rarity, 0);
            }

            dropWeightByRarity[rarity] += Mathf.Max(0f, drop.weight);
            dropCountByRarity[rarity]++;
        }

        bool souvenir = container.forceSouvenirDrops || container.containerType == CaseContainerType.SouvenirPackage;

        for (int i = 0; i < container.dropPool.Count; i++)
        {
            WeightedDrop drop = container.dropPool[i];
            if (!IsEligible(container, drop))
                continue;

            SkinData skin = drop.skin;
            float totalDropWeight = dropWeightByRarity.TryGetValue(skin.rarity, out float weight) ? weight : 0f;
            int rarityCount = dropCountByRarity.TryGetValue(skin.rarity, out int count) ? count : 0;
            float withinRarityShare = totalDropWeight > 0f ? Mathf.Max(0f, drop.weight) / totalDropWeight : rarityCount > 0 ? 1f / rarityCount : 0f;
            float manualRarityShare = manualTotal > 0f && manualRarityWeights.TryGetValue(skin.rarity, out float manualWeight) ? manualWeight / manualTotal : 0f;
            float automatedRarityShare = automatedTotal > 0f && automatedRarityWeights.TryGetValue(skin.rarity, out float automatedWeight) ? automatedWeight / automatedTotal : 0f;

            result.Add(new AutoAcquisitionSkinPreview
            {
                skin = skin,
                manualChancePercent = manualRarityShare * withinRarityShare * 100f,
                automatedChancePercent = automatedRarityShare * withinRarityShare * 100f,
                expectedManualFloat = GetExpectedFloat(skin, 1f),
                expectedAutomatedFloat = GetExpectedFloat(skin, AutoAcquisitionUpgradeUtility.GetFloatCalibrationExponent()),
                canBeStatTrak = !souvenir &&
                    (container.containerType == CaseContainerType.WeaponCase || container.containerType == CaseContainerType.CustomCase) &&
                    container.allowStatTrak && skin.canBeStatTrak,
                souvenir = souvenir && skin.canBeSouvenir
            });
        }

        result.Sort((a, b) =>
        {
            int rarity = ((int)b.skin.rarity).CompareTo((int)a.skin.rarity);
            return rarity != 0 ? rarity : string.Compare(
                SkinDisplayUtility.GetDisplayName(a.skin),
                SkinDisplayUtility.GetDisplayName(b.skin),
                StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    public static double ApplyAutomatedFloatBias(SkinData skin, double value)
    {
        if (skin == null || skin.isVanilla)
            return value;

        double min = Math.Min(skin.minFloat, skin.maxFloat);
        double max = Math.Max(skin.minFloat, skin.maxFloat);
        if (max - min <= 0.0000001d)
            return min;

        double normalised = Math.Max(0d, Math.Min(1d, (value - min) / (max - min)));
        double exponent = AutoAcquisitionUpgradeUtility.GetFloatCalibrationExponent();
        double biased = Math.Pow(normalised, exponent);
        return min + ((max - min) * biased);
    }

    public static double GetExpectedFloat(SkinData skin, float exponent)
    {
        if (skin == null || skin.isVanilla)
            return -1d;

        double min = Math.Min(skin.minFloat, skin.maxFloat);
        double max = Math.Max(skin.minFloat, skin.maxFloat);
        double safeExponent = Math.Max(0.1d, Math.Min(1.15d, exponent));
        double expectedNormalised = 1d / (safeExponent + 1d);
        return min + ((max - min) * expectedNormalised);
    }

    private static Dictionary<Rarity, float> BuildManualRarityWeights(CaseData container)
    {
        Dictionary<Rarity, float> result = new Dictionary<Rarity, float>();

        if (container.rarityChances != null)
        {
            for (int i = 0; i < container.rarityChances.Count; i++)
            {
                RarityChance chance = container.rarityChances[i];
                if (chance == null || chance.chance <= 0f || CountEligibleDropsOfRarity(container, chance.rarity) <= 0)
                    continue;
                result[chance.rarity] = chance.chance;
            }
        }

        if (result.Count > 0)
            return result;

        for (int i = 0; i < container.dropPool.Count; i++)
        {
            WeightedDrop drop = container.dropPool[i];
            if (!IsEligible(container, drop))
                continue;
            if (!result.ContainsKey(drop.skin.rarity))
                result.Add(drop.skin.rarity, 0f);
            result[drop.skin.rarity] += Mathf.Max(0f, drop.weight);
        }

        return result;
    }

    private static Rarity FindLowestRarity(CaseData container)
    {
        Rarity lowest = Rarity.RareSpecial;
        bool found = false;

        for (int i = 0; i < container.dropPool.Count; i++)
        {
            WeightedDrop drop = container.dropPool[i];
            if (!IsEligible(container, drop))
                continue;
            if (!found || (int)drop.skin.rarity < (int)lowest)
            {
                lowest = drop.skin.rarity;
                found = true;
            }
        }

        return found ? lowest : Rarity.Consumer;
    }

    private static int CountEligibleDropsOfRarity(CaseData container, Rarity rarity)
    {
        int count = 0;
        if (container == null || container.dropPool == null)
            return count;

        for (int i = 0; i < container.dropPool.Count; i++)
        {
            WeightedDrop drop = container.dropPool[i];
            if (IsEligible(container, drop) && drop.skin.rarity == rarity)
                count++;
        }

        return count;
    }

    private static bool IsEligible(CaseData container, WeightedDrop drop)
    {
        if (container == null || drop == null || drop.skin == null)
            return false;

        bool souvenir = container.forceSouvenirDrops || container.containerType == CaseContainerType.SouvenirPackage;
        return !souvenir || drop.skin.canBeSouvenir;
    }
}
