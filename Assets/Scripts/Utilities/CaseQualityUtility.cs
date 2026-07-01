using UnityEngine;

public static class CaseQualityUtility
{
    public static CaseQuality GetQualityFromGoldPrice(float price)
    {
        if (price <= 15f)
            return CaseQuality.Consumer;

        if (price <= 30f)
            return CaseQuality.Industrial;

        if (price <= 50f)
            return CaseQuality.MilSpec;

        if (price <= 65f)
            return CaseQuality.Restricted;

        if (price <= 90f)
            return CaseQuality.Classified;

        if (price <= 114f)
            return CaseQuality.Covert;

        return CaseQuality.Gold;
    }

    public static Color GetColor(CaseQuality quality)
    {
        switch (quality)
        {
            case CaseQuality.Consumer:
                return new Color(0.69f, 0.76f, 0.85f); // #B0C3D9

            case CaseQuality.Industrial:
                return new Color(0.37f, 0.60f, 0.85f); // #5E98D9

            case CaseQuality.MilSpec:
                return new Color(0.29f, 0.41f, 1f); // #4B69FF

            case CaseQuality.Restricted:
                return new Color(0.53f, 0.28f, 1f); // #8847FF

            case CaseQuality.Classified:
                return new Color(0.83f, 0.18f, 0.90f); // #D32EE6

            case CaseQuality.Covert:
                return new Color(0.92f, 0.29f, 0.29f); // #EB4B4B

            case CaseQuality.Gold:
                return new Color(1f, 0.84f, 0f); // #FFD700

            default:
                return Color.gray;
        }
    }

    public static string GetDisplayName(CaseQuality quality)
    {
        switch (quality)
        {
            case CaseQuality.Consumer:
                return "Consumer";

            case CaseQuality.Industrial:
                return "Industrial";

            case CaseQuality.MilSpec:
                return "Mil-Spec";

            case CaseQuality.Restricted:
                return "Restricted";

            case CaseQuality.Classified:
                return "Classified";

            case CaseQuality.Covert:
                return "Covert";

            case CaseQuality.Gold:
                return "Gold";

            default:
                return quality.ToString();
        }
    }
}