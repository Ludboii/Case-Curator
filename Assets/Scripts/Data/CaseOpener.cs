using UnityEngine;
using System.Collections.Generic;

public static class CaseOpener
{
    public static InventoryItem OpenCase(CaseData caseData)
    {
        InventoryItem item = new InventoryItem();
        item.instanceId = System.Guid.NewGuid().ToString();
        SkinData selectedSkin = GetRandomSkin(caseData);
        item.skin = selectedSkin;

        bool forceSouvenir =
    caseData.forceSouvenirDrops ||
    caseData.containerType == CaseContainerType.SouvenirPackage;

item.souvenir = forceSouvenir;

item.statTrak =
    !forceSouvenir &&
    caseData.allowStatTrak &&
    selectedSkin.canBeStatTrak &&
    Random.value < 0.10f;

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
    item.patternTier = PatternResolver.ResolveTier(selectedSkin, item.patternId);
    item.isVanilla = false;
}

return item;

}
    
    private static SkinData GetRandomSkin(CaseData caseData)
    {
        Rarity rolledRarity = RollRarity(caseData.rarityChances);

        List<SkinData> matchingSkins = new List<SkinData>();
        foreach (WeightedDrop drop in caseData.dropPool)
        {
            if (drop.skin.rarity == rolledRarity)
                matchingSkins.Add(drop.skin);
        }

        if (matchingSkins.Count == 0)
{
    Debug.LogWarning($"No skins of rarity {rolledRarity} in {caseData.caseName} — FALLING BACK to dropPool[0]: {caseData.dropPool[0].skin.skinName}");
    return caseData.dropPool[0].skin;
}

        return matchingSkins[Random.Range(0, matchingSkins.Count)];
    }

    private static Rarity RollRarity(List<RarityChance> rarityChances)
    {
        float totalChance = 0f;
        foreach (RarityChance rc in rarityChances)
            totalChance += rc.chance;

        float roll = Random.Range(0f, totalChance);
        float current = 0f;
        foreach (RarityChance rc in rarityChances)
        {
            current += rc.chance;
            if (roll <= current)
                return rc.rarity;
        }
        return rarityChances[rarityChances.Count - 1].rarity;
    }
}