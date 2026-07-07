using UnityEngine;

public static class RarityColorUtility
{
    public static Color GetColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Consumer:
                return FromHex("#B0C3D9");
            case Rarity.Industrial:
                return FromHex("#5E98D9");
            case Rarity.MilSpec:
                return FromHex("#4B69FF");
            case Rarity.Restricted:
                return FromHex("#8847FF");
            case Rarity.Classified:
                return FromHex("#D32EE6");
            case Rarity.Covert:
                return FromHex("#EB4B4B");
            case Rarity.RareSpecial:
                return FromHex("#FFD700");
            default:
                return Color.white;
        }
    }

    static Color FromHex(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out Color color))
            return color;

        return Color.white;
    }
}