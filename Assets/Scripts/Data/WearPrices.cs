using System;

[Serializable]
public struct WearPrices
{
    public float factoryNew;
    public float minimalWear;
    public float fieldTested;
    public float wellWorn;
    public float battleScarred;

    public float Get(int wearIndex)
    {
        switch (wearIndex)
        {
            case 0: return factoryNew;
            case 1: return minimalWear;
            case 2: return fieldTested;
            case 3: return wellWorn;
            default: return battleScarred;
        }
    }
}