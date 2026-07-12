public static class PriceCalculator
{
    public static float GetPrice(InventoryItem item)
    {
        if (item == null || item.skin == null)
            return 0f;

        float price = GetBasePrice(item);

        if (!item.isVanilla)
        {
            price *= GetUniquePatternMultiplier(item);
            price *= GetTierMultiplier(item);
            price *= GetFadeMultiplier(item);
            price *= GetFloatMultiplier((float)item.floatValue);
        }

        return price;
    }

    private static float GetBasePrice(InventoryItem item)
    {
        if (item.isVanilla)
        {
            return item.statTrak
                ? item.skin.vanillaStatTrakPrice
                : item.skin.vanillaPrice;
        }

        int wearIndex =
            WearUtility.GetWearIndex((float)item.floatValue);

        if (item.souvenir)
            return item.skin.souvenirExteriorPrices.Get(wearIndex);

        if (item.statTrak)
            return item.skin.statTrakExteriorPrices.Get(wearIndex);

        return item.skin.exteriorPrices.Get(wearIndex);
    }

    private static float GetUniquePatternMultiplier(
        InventoryItem item)
    {
        if (item.skin.uniquePatterns == null)
            return 1f;

        foreach (var specialPattern
                 in item.skin.uniquePatterns)
        {
            if (specialPattern.patternId == item.patternId)
                return specialPattern.multiplier;
        }

        return 1f;
    }

    private static float GetTierMultiplier(InventoryItem item)
    {
        if (item.patternTier == PatternTier.None ||
            item.skin.patternTierGroups == null)
        {
            return 1f;
        }

        foreach (PatternTierGroup group
                 in item.skin.patternTierGroups)
        {
            if (group != null &&
                group.tier == item.patternTier)
            {
                return group.multiplier;
            }
        }

        return 1f;
    }

    private static float GetFadeMultiplier(InventoryItem item)
    {
        if (item.skin.patternType != PatternType.Fade)
            return 1f;

        float fadePercent =
            FadeUtility.GetFadePercent(
                item.skin,
                item.patternId);

        if (fadePercent < 80f)
            return 1f;

        return item.skin.fadeBonusCurve.Evaluate(fadePercent);
    }

    private static float GetFloatMultiplier(float floatValue)
    {
        // Evaluate the rarest thresholds first because they also satisfy every
        // broader low-float threshold below them.
        if (floatValue < 0.000001f)
            return 10.00f;

        if (floatValue < 0.00001f)
            return 4.50f;

        if (floatValue < 0.0001f)
            return 2.80f;

        if (floatValue < 0.001f)
            return 1.95f;

        if (floatValue < 0.01f)
            return 1.30f;

        // GDD / balance workbook high-float multipliers.
        if (floatValue > 0.999f)
            return 1.75f;

        if (floatValue > 0.99f)
            return 1.30f;

        if (floatValue > 0.98f)
            return 1.15f;

        if (floatValue > 0.95f)
            return 1.05f;

        return 1f;
    }
}
