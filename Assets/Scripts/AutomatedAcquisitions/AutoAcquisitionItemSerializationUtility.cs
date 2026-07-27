using System;

/// <summary>
/// Converts generated Automated Acquisition pulls to the same complete item
/// payload used by normal inventory saving. Intake Vault items remain outside
/// inventory capacity until explicitly claimed.
/// </summary>
public static class AutoAcquisitionItemSerializationUtility
{
    public static InventoryItemSaveData ToSaveData(InventoryItem item)
    {
        if (item == null ||
            item.skin == null ||
            string.IsNullOrWhiteSpace(item.skin.apiId))
        {
            return null;
        }

        return new InventoryItemSaveData
        {
            instanceId = string.IsNullOrWhiteSpace(item.instanceId)
                ? Guid.NewGuid().ToString()
                : item.instanceId,
            skinApiId = item.skin.apiId,
            floatValue = item.floatValue,
            patternId = item.patternId,
            patternTier = item.patternTier,
            acquisitionSequence = item.acquisitionSequence,
            statTrak = item.statTrak && !item.souvenir,
            souvenir = item.souvenir,
            isVanilla = item.isVanilla || item.skin.isVanilla,
            favorite = false,
            marketValue = item.marketValue > 0f
                ? item.marketValue
                : PriceCalculator.GetPrice(item),
            storageIndex = 0
        };
    }

    public static InventoryItem ToRuntimeItem(
        InventoryItemSaveData saved,
        GameDatabase database)
    {
        if (saved == null ||
            database == null ||
            string.IsNullOrWhiteSpace(saved.skinApiId))
        {
            return null;
        }

        SkinData skin = database.GetSkinByApiId(saved.skinApiId);

        if (skin == null)
            return null;

        bool souvenir = saved.souvenir && skin.canBeSouvenir;
        bool vanilla = saved.isVanilla || skin.isVanilla;

        InventoryItem item = new InventoryItem
        {
            instanceId = string.IsNullOrWhiteSpace(saved.instanceId)
                ? Guid.NewGuid().ToString()
                : saved.instanceId,
            skin = skin,
            floatValue = vanilla ? -1d : saved.floatValue,
            patternId = vanilla ? -1 : saved.patternId,
            patternTier = vanilla
                ? PatternTier.None
                : saved.patternTier,
            acquisitionSequence = saved.acquisitionSequence,
            statTrak = !souvenir && saved.statTrak && skin.canBeStatTrak,
            souvenir = souvenir,
            isVanilla = vanilla,
            favorite = false,
            storageIndex = 0
        };

        item.marketValue = PriceCalculator.GetPrice(item);
        return item;
    }
}
