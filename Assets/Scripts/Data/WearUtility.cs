public static class WearUtility
{
    public static string GetWear(float value)
    {
        if (value <= 0.07f) return "Factory New";
        if (value <= 0.15f) return "Minimal Wear";
        if (value <= 0.38f) return "Field-Tested";
        if (value <= 0.45f) return "Well-Worn";
        return "Battle-Scarred";
    }

    public static int GetWearIndex(float value)
    {
        if (value <= 0.07f) return 0; // FactoryNew
        if (value <= 0.15f) return 1; // MinimalWear
        if (value <= 0.38f) return 2; // FieldTested
        if (value <= 0.45f) return 3; // WellWorn
        return 4;                     // BattleScarred
    }
}