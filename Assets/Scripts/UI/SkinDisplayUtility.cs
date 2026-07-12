using System;
using System.Globalization;

public static class SkinDisplayUtility
{
    private const string LowGreen = "#55FF88";
    private const string LowSilver = "#D7E2EA";
    private const string LowGold = "#FFD84A";
    private const string LowDiamond = "#69E8FF";

    private const string HighPink = "#FF7FA8";
    private const string HighRed = "#FF3B3B";
    private const string HighDarkRed = "#9E1111";

    public static string GetDisplayName(SkinData skin)
    {
        if (skin == null)
            return "Unknown Skin";

        if (skin.isVanilla)
            return $"{skin.weaponName} | Vanilla";

        return $"{skin.weaponName} | {skin.skinName}";
    }

    public static string GetShortDisplayName(SkinData skin)
    {
        if (skin == null)
            return "Unknown";

        if (skin.isVanilla)
            return skin.weaponName;

        return skin.skinName;
    }

    public static string GetCardFloatDisplay(InventoryItem item)
    {
        if (item == null || item.isVanilla)
            return "Vanilla";

        int decimals = GetCardDecimalCount(item.floatValue);
        string rawValue = FormatFloat(item.floatValue, decimals);

        return ApplyFloatTierStyle(item.floatValue, rawValue);
    }

    public static string GetInspectFloatDisplay(InventoryItem item)
    {
        if (item == null || item.isVanilla)
            return "Vanilla";

        string rawValue = FormatFloat(item.floatValue, 10);
        return ApplyFloatTierStyle(item.floatValue, rawValue);
    }

    private static int GetCardDecimalCount(double floatValue)
    {
        // High floats need additional precision for the same reason very low
        // floats do. Without this, values such as 0.9999952316 round to 1.00000.
        if (floatValue > 0.999d)
            return 10;

        if (floatValue > 0.99d)
            return 8;

        if (floatValue > 0.95d)
            return 6;

        if (floatValue < 0.00001d)
            return 8;

        if (floatValue < 0.0001d)
            return 7;

        if (floatValue < 0.001d)
            return 6;

        return 5;
    }

    private static string FormatFloat(double floatValue, int decimals)
    {
        string format = $"F{decimals}";
        string formatted = floatValue.ToString(format, CultureInfo.InvariantCulture);

        // Avoid visually turning a legitimate sub-1.0 float into exactly 1.0
        // because of decimal rounding at the selected display precision.
        if (floatValue < 1d && formatted.StartsWith("1.", StringComparison.Ordinal))
        {
            double scale = Math.Pow(10d, decimals);
            double truncated = Math.Floor(floatValue * scale) / scale;
            formatted = truncated.ToString(format, CultureInfo.InvariantCulture);
        }

        return formatted;
    }

    private static string ApplyFloatTierStyle(double floatValue, string rawValue)
    {
        // Check the most extreme thresholds first because they also satisfy
        // the broader thresholds below them.
        if (floatValue < 0.00001d)
            return Colorize(LowDiamond, $"✦ {rawValue} ✦");

        if (floatValue < 0.0001d)
            return Colorize(LowGold, rawValue);

        if (floatValue < 0.001d)
            return Colorize(LowSilver, rawValue);

        if (floatValue < 0.01d)
            return Colorize(LowGreen, rawValue);

        if (floatValue > 0.999d)
            return Colorize(HighDarkRed, rawValue);

        if (floatValue > 0.99d)
            return Colorize(HighRed, rawValue);

        if (floatValue > 0.95d)
            return Colorize(HighPink, rawValue);

        return rawValue;
    }

    private static string Colorize(string hexColor, string text)
    {
        return $"<color={hexColor}>{text}</color>";
    }

    public static string GetWearDisplay(InventoryItem item)
    {
        if (item == null || item.isVanilla)
            return "Vanilla";

        return WearUtility.GetWear((float)item.floatValue);
    }

    public static string GetSpecialBadgeText(InventoryItem item)
    {
        if (item == null)
            return "";

        if (item.souvenir)
            return "SV";

        if (item.statTrak)
            return "ST";

        return "";
    }
}
