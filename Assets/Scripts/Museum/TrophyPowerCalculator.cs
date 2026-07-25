using System;
using UnityEngine;

/// <summary>
/// Side-effect-free Trophy Power evaluation. The four main components use the
/// approved 15/35/15/35 weighting, while float prestige combines cap-relative
/// and absolute rarity so float-capped skins remain competitive.
/// </summary>
public static class TrophyPowerCalculator
{
    public static TrophyPowerBreakdown Evaluate(
        InventoryItem item,
        TrophyRoomBalanceData balance,
        int zeroBasedSlotIndex = 0)
    {
        TrophyPowerBreakdown result = new TrophyPowerBreakdown();

        if (item == null || item.skin == null)
            return result;

        TrophyRoomBalanceData active = balance;

        double rarityScore = GetRarityScore(item.skin.rarity);
        double marketValue = item.marketValue > 0f
            ? item.marketValue
            : PriceCalculator.GetPrice(item);
        double marketValueScore = GetMarketValueScore(
            marketValue,
            active != null ? active.marketValueAtFullScore : 10000f);
        double variantScore = GetVariantScore(item, active);
        double floatScore = GetFloatScore(item, active, result);

        double rarityWeight = active != null ? active.rarityWeight : 15d;
        double marketWeight = active != null ? active.marketValueWeight : 35d;
        double variantWeight = active != null ? active.variantWeight : 15d;
        double floatWeight = active != null ? active.floatWeight : 35d;

        result.rarityScore = rarityScore;
        result.marketValueScore = marketValueScore;
        result.variantScore = variantScore;
        result.floatScore = floatScore;

        result.rarityContribution = rarityScore * rarityWeight;
        result.marketValueContribution = marketValueScore * marketWeight;
        result.variantContribution = variantScore * variantWeight;
        result.floatContribution = floatScore * floatWeight;

        result.rawTrophyPower = Math.Max(
            0d,
            result.rarityContribution +
            result.marketValueContribution +
            result.variantContribution +
            result.floatContribution);

        result.pedestalMultiplier = active != null
            ? active.GetPedestalMultiplier(zeroBasedSlotIndex)
            : GetDefaultPedestalMultiplier(zeroBasedSlotIndex);

        result.finalContribution = Math.Max(
            0,
            (int)Math.Ceiling(
                result.rawTrophyPower * result.pedestalMultiplier - 0.0000001d));

        return result;
    }

    private static double GetRarityScore(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Industrial:
                return 0.15d;
            case Rarity.MilSpec:
                return 0.35d;
            case Rarity.Restricted:
                return 0.55d;
            case Rarity.Classified:
                return 0.75d;
            case Rarity.Covert:
                return 0.90d;
            case Rarity.RareSpecial:
                return 1d;
            default:
                return 0d;
        }
    }

    private static double GetMarketValueScore(
        double marketValue,
        double fullScoreValue)
    {
        double safeValue = Math.Max(0d, marketValue);
        double reference = Math.Max(1d, fullScoreValue);
        double denominator = Math.Log10(1d + reference);

        if (denominator <= 0d)
            return 0d;

        return Clamp01(Math.Log10(1d + safeValue) / denominator);
    }

    private static double GetVariantScore(
        InventoryItem item,
        TrophyRoomBalanceData balance)
    {
        if (item.statTrak && !item.souvenir)
        {
            return Clamp01(
                balance != null ? balance.statTrakVariantScore : 1d);
        }

        if (item.souvenir)
        {
            return Clamp01(
                balance != null ? balance.souvenirVariantScore : 0.8d);
        }

        return Clamp01(
            balance != null ? balance.normalVariantScore : 0d);
    }

    private static double GetFloatScore(
        InventoryItem item,
        TrophyRoomBalanceData balance,
        TrophyPowerBreakdown result)
    {
        if (item.isVanilla || item.floatValue < 0d || item.skin == null)
            return 0d;

        double minimum = Clamp01(item.skin.minFloat);
        double maximum = Clamp01(item.skin.maxFloat);

        if (maximum < minimum)
        {
            double swap = minimum;
            minimum = maximum;
            maximum = swap;
        }

        double value = Math.Max(minimum, Math.Min(maximum, item.floatValue));
        double range = Math.Max(0.0000001d, maximum - minimum);
        double floorGap = Math.Max(0d, value - minimum);
        double ceilingGap = Math.Max(0d, maximum - value);
        double positionFromMinimum = Clamp01(floorGap / range);
        double positionFromMaximum = Clamp01(ceilingGap / range);

        double floorGapScore = EvaluateCurve(
            balance != null ? balance.floorGapCurve : null,
            floorGap,
            DefaultFloorGapScore);
        double lowRangeScore = EvaluateCurve(
            balance != null ? balance.lowRangePositionCurve : null,
            positionFromMinimum,
            DefaultLowRangePositionScore);
        double absoluteLowScore = EvaluateCurve(
            balance != null ? balance.absoluteLowFloatCurve : null,
            value,
            DefaultAbsoluteLowScore);

        double ceilingGapScore = EvaluateCurve(
            balance != null ? balance.ceilingGapCurve : null,
            ceilingGap,
            DefaultFloorGapScore);
        double highRangeScore = EvaluateCurve(
            balance != null ? balance.highRangePositionCurve : null,
            positionFromMaximum,
            DefaultLowRangePositionScore);
        double absoluteHighScore = EvaluateCurve(
            balance != null ? balance.absoluteHighFloatCurve : null,
            value,
            DefaultAbsoluteHighScore);

        double gapWeight = balance != null ? balance.floorGapWeight : 0.70d;
        double rangeWeight = balance != null
            ? balance.rangePositionWeight
            : 0.20d;
        double absoluteWeight = balance != null
            ? balance.absoluteFloatWeight
            : 0.10d;
        double weightTotal = Math.Max(
            0.0001d,
            gapWeight + rangeWeight + absoluteWeight);

        gapWeight /= weightTotal;
        rangeWeight /= weightTotal;
        absoluteWeight /= weightTotal;

        double lowPrestige = Clamp01(
            floorGapScore * gapWeight +
            lowRangeScore * rangeWeight +
            absoluteLowScore * absoluteWeight);

        double highBase = Clamp01(
            ceilingGapScore * gapWeight +
            highRangeScore * rangeWeight +
            absoluteHighScore * absoluteWeight);
        double highStrength = Clamp01(
            balance != null ? balance.highFloatStrength : 0.70d);
        double highPrestige = highBase * highStrength;

        result.lowFloatPrestige = lowPrestige;
        result.highFloatPrestige = highPrestige;
        result.rangeRelativePrestige = Math.Max(
            floorGapScore * gapWeight + lowRangeScore * rangeWeight,
            (ceilingGapScore * gapWeight + highRangeScore * rangeWeight) *
            highStrength);
        result.absoluteFloatPrestige = Math.Max(
            absoluteLowScore,
            absoluteHighScore * highStrength);

        return Math.Max(lowPrestige, highPrestige);
    }

    private static double EvaluateCurve(
        AnimationCurve curve,
        double x,
        Func<double, double> fallback)
    {
        if (curve != null && curve.length > 0)
            return Clamp01(curve.Evaluate((float)x));

        return Clamp01(fallback != null ? fallback(x) : 0d);
    }

    private static double DefaultFloorGapScore(double gap)
    {
        double[] x =
        {
            0.00001d, 0.00010d, 0.001d, 0.005d, 0.01d,
            0.03d, 0.05d, 0.10d, 0.20d, 0.30d
        };
        double[] y =
        {
            1d, 0.98d, 0.94d, 0.90d, 0.86d,
            0.80d, 0.74d, 0.55d, 0.25d, 0d
        };

        return Interpolate(gap, x, y);
    }

    private static double DefaultLowRangePositionScore(double position)
    {
        double[] x = { 0d, 0.001d, 0.01d, 0.05d, 0.10d, 0.25d, 0.50d };
        double[] y = { 1d, 1d, 0.98d, 0.90d, 0.80d, 0.50d, 0d };
        return Interpolate(position, x, y);
    }

    private static double DefaultAbsoluteLowScore(double value)
    {
        double[] x =
        {
            0.00001d, 0.00010d, 0.001d, 0.01d,
            0.03d, 0.06d, 0.10d
        };
        double[] y = { 1d, 0.98d, 0.90d, 0.65d, 0.35d, 0.10d, 0d };
        return Interpolate(value, x, y);
    }

    private static double DefaultAbsoluteHighScore(double value)
    {
        double[] x = { 0.70d, 0.85d, 0.93d, 0.97d, 0.99d, 0.999d };
        double[] y = { 0d, 0.15d, 0.35d, 0.60d, 0.85d, 1d };
        return Interpolate(value, x, y);
    }

    private static double Interpolate(
        double value,
        double[] x,
        double[] y)
    {
        if (x == null || y == null || x.Length == 0 || x.Length != y.Length)
            return 0d;

        if (value <= x[0])
            return y[0];

        for (int i = 1; i < x.Length; i++)
        {
            if (value > x[i])
                continue;

            double span = Math.Max(0.0000000001d, x[i] - x[i - 1]);
            double t = Clamp01((value - x[i - 1]) / span);
            return y[i - 1] + (y[i] - y[i - 1]) * t;
        }

        return y[y.Length - 1];
    }

    private static double GetDefaultPedestalMultiplier(int slotIndex)
    {
        if (slotIndex < 5)
            return 1d;

        if (slotIndex < 10)
            return 1.2d;

        return 1.5d;
    }

    private static double Clamp01(double value)
    {
        return Math.Max(0d, Math.Min(1d, value));
    }
}
