using System;

/// <summary>
/// Stable identity helpers for Museum collection slots. Save data stores the
/// resulting key, so the format must remain backwards-compatible after release.
/// </summary>
public static class MuseumDonationKeyUtility
{
    private const string KeyVersion = "museum-slot-v1";

    public static MuseumDonationVariant GetVariant(InventoryItem item)
    {
        if (item != null && item.souvenir)
            return MuseumDonationVariant.Souvenir;

        if (item != null && item.statTrak)
            return MuseumDonationVariant.StatTrak;

        return MuseumDonationVariant.Normal;
    }

    public static int GetWearIndex(InventoryItem item)
    {
        // SkinData is the authoritative source. InventoryItem.isVanilla is a
        // legacy mirror and older knife-generation paths can mark every knife
        // item vanilla, including finishes such as Stained.
        if (item == null || (item.skin != null && item.skin.isVanilla))
            return -1;

        return WearUtility.GetWearIndex((float)item.floatValue);
    }

    public static MuseumWearTier GetWearTier(InventoryItem item)
    {
        int wearIndex = GetWearIndex(item);
        return GetWearTier(wearIndex);
    }

    public static MuseumWearTier GetWearTier(int wearIndex)
    {
        if (wearIndex < 0)
            return MuseumWearTier.FactoryNew;

        switch (wearIndex)
        {
            case 0: return MuseumWearTier.FactoryNew;
            case 1: return MuseumWearTier.MinimalWear;
            case 2: return MuseumWearTier.FieldTested;
            case 3: return MuseumWearTier.WellWorn;
            default: return MuseumWearTier.BattleScarred;
        }
    }

    public static string Build(InventoryItem item)
    {
        if (item == null || item.skin == null)
            return "";

        bool vanilla = item.skin.isVanilla;

        return Build(
            item.skin,
            vanilla ? -1 : GetWearIndex(item),
            GetVariant(item),
            vanilla);
    }

    public static string Build(
        SkinData skin,
        int wearIndex,
        MuseumDonationVariant variant,
        bool isVanilla)
    {
        if (skin == null || string.IsNullOrWhiteSpace(skin.apiId))
            return "";

        int normalizedWear = isVanilla
            ? -1
            : Math.Max(0, Math.Min(4, wearIndex));

        return string.Concat(
            KeyVersion,
            "|skin:", Escape(skin.apiId),
            "|wear:", normalizedWear,
            "|variant:", (int)variant,
            "|vanilla:", isVanilla ? "1" : "0");
    }

    public static string Build(MuseumDonationRecordSaveData record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.skinApiId))
            return "";

        MuseumDonationVariant variant = record.souvenir
            ? MuseumDonationVariant.Souvenir
            : record.statTrak
                ? MuseumDonationVariant.StatTrak
                : MuseumDonationVariant.Normal;

        int normalizedWear = record.isVanilla
            ? -1
            : Math.Max(0, Math.Min(4, record.wearIndex));

        return string.Concat(
            KeyVersion,
            "|skin:", Escape(record.skinApiId),
            "|wear:", normalizedWear,
            "|variant:", (int)variant,
            "|vanilla:", record.isVanilla ? "1" : "0");
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim()
            .Replace("%", "%25")
            .Replace("|", "%7C")
            .Replace(":", "%3A");
    }
}
