public static class PriceCalculator
{
    public static float GetPrice(InventoryItem item)
    {
        float price = GetBasePrice(item);

        if (!item.isVanilla)
        {
            price *= GetUniquePatternMultiplier(item);
            price *= GetTierMultiplier(item);
            price *= GetFadeMultiplier(item);
            price *= GetLeadingZeroBonus((float)item.floatValue);
        }

        return price;
    }

    static float GetBasePrice(InventoryItem item)
    {
        if (item.isVanilla)
            return item.statTrak ? item.skin.vanillaStatTrakPrice : item.skin.vanillaPrice;

        int wearIndex = WearUtility.GetWearIndex((float)item.floatValue);

        if (item.souvenir) return item.skin.souvenirExteriorPrices.Get(wearIndex);
        if (item.statTrak)  return item.skin.statTrakExteriorPrices.Get(wearIndex);
        return item.skin.exteriorPrices.Get(wearIndex);
    }

    static float GetUniquePatternMultiplier(InventoryItem item)
    {
        foreach (var sp in item.skin.uniquePatterns)
            if (sp.patternId == item.patternId) return sp.multiplier;
        return 1f;
    }

    static float GetTierMultiplier(InventoryItem item)
    {
        if (item.patternTier == PatternTier.None) return 1f;

        foreach (var group in item.skin.patternTierGroups)
            if (group.tier == item.patternTier) return group.multiplier;
    return 1f;
}

 static float GetFadeMultiplier(InventoryItem item)
    {
        if (item.skin.patternType != PatternType.Fade) return 1f;

        float fadePercent = FadeUtility.GetFadePercent(item.skin, item.patternId);
        if (fadePercent < 80f) return 1f;

        return item.skin.fadeBonusCurve.Evaluate(fadePercent);
    }

    static float GetLeadingZeroBonus(float f)
    {
        if (f < 0.00001f) return 2.25f;
        if (f < 0.0001f) return 1.65f;
        if (f < 0.001f) return 1.30f;
        if (f < 0.01f) return 1.20f;
        return 1f;
    }
}