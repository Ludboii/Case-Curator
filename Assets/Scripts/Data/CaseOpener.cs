using UnityEngine;
using System.Collections.Generic;

public static class CaseOpener
{
    private const float StatTrakChance = 0.10f;

    public static InventoryItem OpenCase(CaseData caseData)
    {
        if (caseData == null)
        {
            Debug.LogWarning("CaseOpener: Tried to open a null case.");
            return null;
        }

        if (caseData.dropPool == null || caseData.dropPool.Count == 0)
        {
            Debug.LogWarning($"CaseOpener: {caseData.caseName} has no drops.");
            return null;
        }

        SkinData selectedSkin = GetRandomSkin(caseData);

        if (selectedSkin == null)
        {
            Debug.LogWarning($"CaseOpener: Could not select a valid skin from {caseData.caseName}.");
            return null;
        }

        InventoryItem item = new InventoryItem();
        item.instanceId = System.Guid.NewGuid().ToString();
        item.skin = selectedSkin;
        item.favorite = false;

        bool forceSouvenir =
            caseData.forceSouvenirDrops ||
            caseData.containerType == CaseContainerType.SouvenirPackage;

        item.souvenir =
            forceSouvenir &&
            selectedSkin.canBeSouvenir;

        bool containerAllowsStatTrak =
            caseData.containerType == CaseContainerType.WeaponCase ||
            caseData.containerType == CaseContainerType.CustomCase;

        item.statTrak =
            !item.souvenir &&
            containerAllowsStatTrak &&
            caseData.allowStatTrak &&
            selectedSkin.canBeStatTrak &&
            Random.value < StatTrakChance;

        if (selectedSkin.isVanilla)
        {
            item.floatValue = -1;
            item.patternId = -1;
            item.patternTier = PatternTier.None;
            item.isVanilla = true;
        }
        else
        {
            item.floatValue = RandomUtility.RangeDouble(
                selectedSkin.minFloat,
                selectedSkin.maxFloat);

            item.patternId = Random.Range(0, 1001);
            item.patternTier = PatternResolver.ResolveTier(
                selectedSkin,
                item.patternId);

            item.isVanilla = false;
        }

        return item;
    }

    private static SkinData GetRandomSkin(CaseData caseData)
    {
        bool rarityRolled =
            TryRollRarity(caseData, out Rarity rolledRarity);

        if (!rarityRolled)
        {
            Debug.LogWarning(
                $"CaseOpener: Could not roll rarity for {caseData.caseName}. " +
                "Using weighted fallback from all valid drops.");

            return GetWeightedRandomSkinFromAllValidDrops(caseData);
        }

        SkinData selectedSkin =
            GetWeightedRandomSkinFromRarity(caseData, rolledRarity);

        if (selectedSkin != null)
            return selectedSkin;

        Debug.LogWarning(
            $"CaseOpener: No valid skins of rarity {rolledRarity} in {caseData.caseName}. " +
            "Using weighted fallback from all valid drops.");

        return GetWeightedRandomSkinFromAllValidDrops(caseData);
    }

    private static bool TryRollRarity(CaseData caseData, out Rarity rolledRarity)
    {
        rolledRarity = Rarity.MilSpec;

        if (caseData == null ||
            caseData.rarityChances == null ||
            caseData.rarityChances.Count == 0)
        {
            return false;
        }

        List<RarityChance> validRarityChances = new List<RarityChance>();

        foreach (RarityChance rarityChance in caseData.rarityChances)
        {
            if (rarityChance == null)
                continue;

            if (rarityChance.chance <= 0f)
                continue;

            if (!IsRarityAllowedForCase(caseData, rarityChance.rarity))
                continue;

            if (!HasValidSkinOfRarity(caseData, rarityChance.rarity))
                continue;

            validRarityChances.Add(rarityChance);
        }

        if (validRarityChances.Count == 0)
            return false;

        float totalChance = 0f;

        foreach (RarityChance rarityChance in validRarityChances)
        {
            totalChance += rarityChance.chance;
        }

        if (totalChance <= 0f)
            return false;

        float roll = Random.Range(0f, totalChance);
        float current = 0f;

        foreach (RarityChance rarityChance in validRarityChances)
        {
            current += rarityChance.chance;

            if (roll <= current)
            {
                rolledRarity = rarityChance.rarity;
                return true;
            }
        }

        rolledRarity = validRarityChances[validRarityChances.Count - 1].rarity;
        return true;
    }

    private static SkinData GetWeightedRandomSkinFromRarity(
        CaseData caseData,
        Rarity rarity)
    {
        List<WeightedDrop> validDrops = new List<WeightedDrop>();

        foreach (WeightedDrop drop in caseData.dropPool)
        {
            if (!IsDropValidForCase(caseData, drop))
                continue;

            if (drop.skin.rarity != rarity)
                continue;

            validDrops.Add(drop);
        }

        return RollWeightedDrop(validDrops);
    }

    private static SkinData GetWeightedRandomSkinFromAllValidDrops(
        CaseData caseData)
    {
        List<WeightedDrop> validDrops = new List<WeightedDrop>();

        foreach (WeightedDrop drop in caseData.dropPool)
        {
            if (!IsDropValidForCase(caseData, drop))
                continue;

            validDrops.Add(drop);
        }

        return RollWeightedDrop(validDrops);
    }

    private static SkinData RollWeightedDrop(List<WeightedDrop> validDrops)
    {
        if (validDrops == null || validDrops.Count == 0)
            return null;

        float totalWeight = 0f;

        foreach (WeightedDrop drop in validDrops)
        {
            if (drop == null || drop.skin == null)
                continue;

            if (drop.weight > 0f)
                totalWeight += drop.weight;
        }

        if (totalWeight <= 0f)
        {
            WeightedDrop randomDrop =
                validDrops[Random.Range(0, validDrops.Count)];

            return randomDrop != null ? randomDrop.skin : null;
        }

        float roll = Random.Range(0f, totalWeight);
        float current = 0f;

        foreach (WeightedDrop drop in validDrops)
        {
            if (drop == null || drop.skin == null)
                continue;

            if (drop.weight <= 0f)
                continue;

            current += drop.weight;

            if (roll <= current)
                return drop.skin;
        }

        return validDrops[validDrops.Count - 1].skin;
    }

    private static bool HasValidSkinOfRarity(
        CaseData caseData,
        Rarity rarity)
    {
        if (caseData == null || caseData.dropPool == null)
            return false;

        foreach (WeightedDrop drop in caseData.dropPool)
        {
            if (!IsDropValidForCase(caseData, drop))
                continue;

            if (drop.skin.rarity == rarity)
                return true;
        }

        return false;
    }

    private static bool IsDropValidForCase(
        CaseData caseData,
        WeightedDrop drop)
    {
        if (caseData == null || drop == null || drop.skin == null)
            return false;

        SkinData skin = drop.skin;

        if (!IsRarityAllowedForCase(caseData, skin.rarity))
            return false;

        bool forceSouvenir =
            caseData.forceSouvenirDrops ||
            caseData.containerType == CaseContainerType.SouvenirPackage;

        if (forceSouvenir && !skin.canBeSouvenir)
            return false;

        return true;
    }

    private static bool IsRarityAllowedForCase(
        CaseData caseData,
        Rarity rarity)
    {
        if (caseData == null)
            return false;

        if (rarity != Rarity.RareSpecial)
            return true;

        bool containerCanHaveRareSpecial =
            caseData.containerType == CaseContainerType.WeaponCase ||
            caseData.containerType == CaseContainerType.CustomCase;

        bool rulesAllowRareSpecial =
            caseData.allowRareSpecialItem &&
            caseData.shouldHaveRareSpecial;

        return containerCanHaveRareSpecial && rulesAllowRareSpecial;
    }
}
