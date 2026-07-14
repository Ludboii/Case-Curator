using UnityEngine;

public static class CaseOpener
{
    private const float StatTrakChance = 0.10f;

    public static InventoryItem OpenCase(CaseData caseData)
    {
        if (caseData == null)
        {
            Debug.LogWarning(
                "CaseOpener: Tried to open a null case.");
            return null;
        }

        if (caseData.dropPool == null || caseData.dropPool.Count == 0)
        {
            Debug.LogWarning(
                $"CaseOpener: {caseData.caseName} has no drops.");
            return null;
        }

        SkinData selectedSkin = GetRandomSkin(caseData);

        if (selectedSkin == null)
        {
            Debug.LogWarning(
                $"CaseOpener: Could not select a valid skin " +
                $"from {caseData.caseName}.");
            return null;
        }

        InventoryItem item = new InventoryItem
        {
            instanceId = System.Guid.NewGuid().ToString(),
            skin = selectedSkin,
            favorite = false
        };

        bool forceSouvenir =
            caseData.forceSouvenirDrops ||
            caseData.containerType == CaseContainerType.SouvenirPackage;

        item.souvenir = forceSouvenir && selectedSkin.canBeSouvenir;

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
            item.floatValue = -1d;
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
        if (!TryRollRarity(caseData, out Rarity rolledRarity))
        {
            Debug.LogWarning(
                $"CaseOpener: Could not roll rarity for " +
                $"{caseData.caseName}. Using weighted fallback " +
                "from all valid drops.");

            return RollWeightedDrop(caseData, false, Rarity.MilSpec);
        }

        SkinData selectedSkin =
            RollWeightedDrop(caseData, true, rolledRarity);

        if (selectedSkin != null)
            return selectedSkin;

        Debug.LogWarning(
            $"CaseOpener: No valid skins of rarity {rolledRarity} in " +
            $"{caseData.caseName}. Using weighted fallback from all valid drops.");

        return RollWeightedDrop(caseData, false, rolledRarity);
    }

    private static bool TryRollRarity(
        CaseData caseData,
        out Rarity rolledRarity)
    {
        rolledRarity = Rarity.MilSpec;

        if (caseData == null ||
            caseData.rarityChances == null ||
            caseData.rarityChances.Count == 0)
        {
            return false;
        }

        float totalChance = 0f;
        Rarity lastValidRarity = rolledRarity;
        bool foundValid = false;

        for (int i = 0; i < caseData.rarityChances.Count; i++)
        {
            RarityChance rarityChance = caseData.rarityChances[i];

            if (!IsValidRarityChance(caseData, rarityChance))
                continue;

            totalChance += rarityChance.chance;
            lastValidRarity = rarityChance.rarity;
            foundValid = true;
        }

        if (!foundValid || totalChance <= 0f)
            return false;

        float roll = Random.Range(0f, totalChance);
        float current = 0f;

        for (int i = 0; i < caseData.rarityChances.Count; i++)
        {
            RarityChance rarityChance = caseData.rarityChances[i];

            if (!IsValidRarityChance(caseData, rarityChance))
                continue;

            current += rarityChance.chance;

            if (roll <= current)
            {
                rolledRarity = rarityChance.rarity;
                return true;
            }
        }

        rolledRarity = lastValidRarity;
        return true;
    }

    private static bool IsValidRarityChance(
        CaseData caseData,
        RarityChance rarityChance)
    {
        return rarityChance != null &&
               rarityChance.chance > 0f &&
               IsRarityAllowedForCase(caseData, rarityChance.rarity) &&
               HasValidSkinOfRarity(caseData, rarityChance.rarity);
    }

    /// <summary>
    /// Performs a two-pass weighted roll directly against CaseData. This avoids
    /// allocating temporary drop lists for every opened container.
    /// </summary>
    private static SkinData RollWeightedDrop(
        CaseData caseData,
        bool filterByRarity,
        Rarity rarity)
    {
        if (caseData == null || caseData.dropPool == null)
            return null;

        float totalWeight = 0f;
        int validCount = 0;

        for (int i = 0; i < caseData.dropPool.Count; i++)
        {
            WeightedDrop drop = caseData.dropPool[i];

            if (!IsValidCandidate(caseData, drop, filterByRarity, rarity))
                continue;

            validCount++;

            if (drop.weight > 0f)
                totalWeight += drop.weight;
        }

        if (validCount == 0)
            return null;

        if (totalWeight <= 0f)
        {
            int targetIndex = Random.Range(0, validCount);
            int currentIndex = 0;

            for (int i = 0; i < caseData.dropPool.Count; i++)
            {
                WeightedDrop drop = caseData.dropPool[i];

                if (!IsValidCandidate(
                        caseData,
                        drop,
                        filterByRarity,
                        rarity))
                {
                    continue;
                }

                if (currentIndex == targetIndex)
                    return drop.skin;

                currentIndex++;
            }

            return null;
        }

        float roll = Random.Range(0f, totalWeight);
        float currentWeight = 0f;
        SkinData lastValidSkin = null;

        for (int i = 0; i < caseData.dropPool.Count; i++)
        {
            WeightedDrop drop = caseData.dropPool[i];

            if (!IsValidCandidate(caseData, drop, filterByRarity, rarity))
                continue;

            lastValidSkin = drop.skin;

            if (drop.weight <= 0f)
                continue;

            currentWeight += drop.weight;

            if (roll <= currentWeight)
                return drop.skin;
        }

        return lastValidSkin;
    }

    private static bool IsValidCandidate(
        CaseData caseData,
        WeightedDrop drop,
        bool filterByRarity,
        Rarity rarity)
    {
        if (!IsDropValidForCase(caseData, drop))
            return false;

        return !filterByRarity || drop.skin.rarity == rarity;
    }

    private static bool HasValidSkinOfRarity(
        CaseData caseData,
        Rarity rarity)
    {
        if (caseData == null || caseData.dropPool == null)
            return false;

        for (int i = 0; i < caseData.dropPool.Count; i++)
        {
            WeightedDrop drop = caseData.dropPool[i];

            if (IsDropValidForCase(caseData, drop) &&
                drop.skin.rarity == rarity)
            {
                return true;
            }
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

        return !forceSouvenir || skin.canBeSouvenir;
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
