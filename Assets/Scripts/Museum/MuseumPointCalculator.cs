using System;

/// <summary>
/// Pure Museum point calculation. M3 uses the rarity/wear matrix, variant
/// multiplier, optional Rare Special/vanilla modifiers, and the capped
/// logarithmic market-value bonus. Low-float bonuses are intentionally omitted.
/// </summary>
public sealed class MuseumPointCalculator
{
    private readonly MuseumBalanceData balance;

    public MuseumPointCalculator(MuseumBalanceData balanceData)
    {
        balance = balanceData;
    }

    public MuseumPointBreakdown Calculate(InventoryItem item)
    {
        MuseumPointBreakdown breakdown = new MuseumPointBreakdown();

        if (item == null || item.skin == null || balance == null)
            return breakdown;

        bool vanilla = item.isVanilla || item.skin.isVanilla;
        MuseumWearTier wear = vanilla
            ? MuseumWearTier.FactoryNew
            : MuseumDonationKeyUtility.GetWearTier(item);

        MuseumDonationVariant variant =
            MuseumDonationKeyUtility.GetVariant(item);

        breakdown.rarityWearPoints =
            balance.GetRarityWearPoints(item.skin.rarity, wear, vanilla);
        breakdown.basePoints = breakdown.rarityWearPoints;
        breakdown.wearMultiplier = 1d;
        breakdown.variantMultiplier =
            balance.GetVariantMultiplier(variant);
        breakdown.rareSpecialMultiplier =
            item.skin.rarity == Rarity.RareSpecial
                ? Math.Max(0d, balance.rareSpecialPointMultiplier)
                : 1d;
        breakdown.vanillaMultiplier = vanilla
            ? Math.Max(0d, balance.vanillaPointMultiplier)
            : 1d;

        breakdown.pointsBeforeMarketBonus = Math.Max(
            0d,
            breakdown.rarityWearPoints *
            breakdown.variantMultiplier *
            breakdown.rareSpecialMultiplier *
            breakdown.vanillaMultiplier);

        double marketValue = Math.Max(0d, item.marketValue);
        breakdown.marketValueBonus =
            balance.CalculateMarketValueBonus(marketValue);
        breakdown.marketValueBonusRate =
            balance.GetEffectiveMarketBonusRate(marketValue);

        breakdown.totalPoints = Math.Max(
            0d,
            breakdown.pointsBeforeMarketBonus +
            breakdown.marketValueBonus);

        return breakdown;
    }
}
