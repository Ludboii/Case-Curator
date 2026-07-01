using System;

[Serializable]
public class InventoryItemSaveData
{
    public string instanceId;

    public string skinApiId;

    public double floatValue;
    public int patternId;
    public PatternTier patternTier;

    public bool statTrak;
    public bool souvenir;
    public bool isVanilla;
    public bool favorite;

    public float marketValue;
}