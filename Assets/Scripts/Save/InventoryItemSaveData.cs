using System;

[Serializable]
public class InventoryItemSaveData
{
    public string instanceId;

    public string skinApiId;

    public double floatValue;
    public int patternId;
    public PatternTier patternTier;
    public long acquisitionSequence;

    public bool statTrak;
    public bool souvenir;
    public bool isVanilla;
    public bool favorite;

    public float marketValue;

    // Zero-based storage container index. Missing values in older SaveData 2.0
    // files deserialize as 0, keeping every existing item in Storage 1.
    public int storageIndex;
}
