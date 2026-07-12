public static class WearUtility
{
    public static string GetWear(float value)
    {
        if (value < 0.07f) return "Factory New";
        if (value < 0.15f) return "Minimal Wear";
        if (value < 0.38f) return "Field-Tested";
        if (value < 0.45f) return "Well-Worn";
        return "Battle-Scarred";
    }

    public static int GetWearIndex(float value)
    {
        if (value < 0.07f) return 0; // Factory New
        if (value < 0.15f) return 1; // Minimal Wear
        if (value < 0.38f) return 2; // Field-Tested
        if (value < 0.45f) return 3; // Well-Worn
        return 4;                    // Battle-Scarred
    }
}
