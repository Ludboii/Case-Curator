using System;

[Serializable]
public class InventoryItem
{
    public string instanceId;

    public SkinData skin;
    public double floatValue;
    public int patternId;
    public PatternTier patternTier;
    public long acquisitionSequence;
    public bool statTrak;
    public bool favorite;
    public bool souvenir;
    public bool isVanilla;
    public float marketValue;

    // Zero-based index of the storage container that owns this item.
    // Existing SaveData 2.0 files do not contain this field, so Unity's JSON
    // deserializer safely places those items in Storage 1 (index 0).
    public int storageIndex;
}
