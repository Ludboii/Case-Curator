using System;

/// <summary>
/// Converts complete inventory instances to and from Trophy Room save records.
/// Trophy-stored items are removed from InventoryManager, so their full state must
/// remain inside MuseumStateSaveData until the player retrieves them.
/// </summary>
public static class TrophyItemSerializationUtility
{
    public static InventoryItemSaveData CreateSave(InventoryItem item)
    {
        if (item == null || item.skin == null ||
            string.IsNullOrWhiteSpace(item.skin.apiId))
        {
            return null;
        }

        return new InventoryItemSaveData
        {
            instanceId = item.instanceId,
            skinApiId = item.skin.apiId,
            floatValue = item.floatValue,
            patternId = item.patternId,
            patternTier = item.patternTier,
            acquisitionSequence = item.acquisitionSequence,
            statTrak = item.statTrak && !item.souvenir,
            souvenir = item.souvenir,
            isVanilla = item.isVanilla,
            favorite = item.favorite,
            marketValue = item.marketValue,
            storageIndex = item.storageIndex
        };
    }

    public static InventoryItem CreateRuntimeItem(
        InventoryItemSaveData saved,
        GameDatabase database)
    {
        if (saved == null || database == null ||
            string.IsNullOrWhiteSpace(saved.skinApiId))
        {
            return null;
        }

        SkinData skin = database.GetSkinByApiId(saved.skinApiId);

        if (skin == null)
            return null;

        InventoryItem item = new InventoryItem
        {
            instanceId = string.IsNullOrWhiteSpace(saved.instanceId)
                ? Guid.NewGuid().ToString()
                : saved.instanceId,
            skin = skin,
            floatValue = saved.floatValue,
            patternId = saved.patternId,
            patternTier = saved.patternTier,
            acquisitionSequence = saved.acquisitionSequence,
            statTrak = saved.statTrak && !saved.souvenir,
            souvenir = saved.souvenir,
            isVanilla = saved.isVanilla || skin.isVanilla,
            favorite = saved.favorite,
            storageIndex = Math.Max(0, saved.storageIndex)
        };

        if (item.isVanilla)
        {
            item.floatValue = -1d;
            item.patternId = -1;
            item.patternTier = PatternTier.None;
        }

        item.marketValue = PriceCalculator.GetPrice(item);
        return item;
    }

    public static InventoryItemSaveData Clone(InventoryItemSaveData source)
    {
        if (source == null)
            return null;

        return new InventoryItemSaveData
        {
            instanceId = source.instanceId,
            skinApiId = source.skinApiId,
            floatValue = source.floatValue,
            patternId = source.patternId,
            patternTier = source.patternTier,
            acquisitionSequence = source.acquisitionSequence,
            statTrak = source.statTrak && !source.souvenir,
            souvenir = source.souvenir,
            isVanilla = source.isVanilla,
            favorite = source.favorite,
            marketValue = source.marketValue,
            storageIndex = Math.Max(0, source.storageIndex)
        };
    }
}
