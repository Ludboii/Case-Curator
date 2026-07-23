using System;

/// <summary>
/// Pure Museum point calculation. It has no inventory, save, UI or random
/// dependencies, which keeps donation balancing deterministic and testable.
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

        breakdown.basePoints =
            balance.GetBasePoints(item.skin.rarity);

        breakdown.wearMultiplier = vanilla
            ? 1d
            : balance.GetWearMultiplier(wear);

        breakdown.variantMultiplier =
            balance.GetVariantMultiplier(variant);

        breakdown.rareSpecialMultiplier =
            item.skin.rarity == Rarity.RareSpecial
                ? Math.Max(0d, balance.rareSpecialPointMultiplier)
                : 1d;

        breakdown.vanillaMultiplier = vanilla
            ? Math.Max(0d, balance.vanillaPointMultiplier)
            : 1d;

        breakdown.totalPoints = Math.Max(
            0d,
            breakdown.basePoints *
            breakdown.wearMultiplier *
            breakdown.variantMultiplier *
            breakdown.rareSpecialMultiplier *
            breakdown.vanillaMultiplier);

        return breakdown;
    }
}
