using System;
using System.Collections.Generic;

[Serializable]
public class StickerSystemSaveData
{
    public List<StickerCraftSaveData> crafts =
        new List<StickerCraftSaveData>();
}

[Serializable]
public class StickerCraftSaveData
{
    public string skinInventoryItemInstanceId;
    public List<AppliedStickerSaveData> appliedStickers =
        new List<AppliedStickerSaveData>();
}

[Serializable]
public class AppliedStickerSaveData
{
    public int slotIndex;
    public string stickerApiId;

    // The exact unapplied inventory identity is retained. Removing the sticker
    // restores this identity, acquisition order, favourite flag and storage.
    public string stickerInstanceId;
    public long acquisitionSequence;
    public bool favorite;
    public int originalStorageIndex;

    // Reserved for a possible future scraping phase. This phase always keeps it
    // at 1.0 and does not expose scraping controls.
    public float condition = 1f;
}
