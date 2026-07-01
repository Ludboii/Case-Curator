using System.Globalization;

public static class SkinDisplayUtility
{
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

        return item.floatValue.ToString("F5", CultureInfo.InvariantCulture);
    }

    public static string GetInspectFloatDisplay(InventoryItem item)
    {
        if (item == null || item.isVanilla)
            return "Vanilla";

        return item.floatValue.ToString("F10", CultureInfo.InvariantCulture);
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