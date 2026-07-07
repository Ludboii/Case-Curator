using System;

[Serializable]
public class InventoryItem
{
    public string instanceId;

    public SkinData skin;
    public double floatValue;
    public int patternId;
    public PatternTier patternTier;
    public bool statTrak;
    public bool favorite;
    public bool souvenir;
    public bool isVanilla;
    public float marketValue;
}