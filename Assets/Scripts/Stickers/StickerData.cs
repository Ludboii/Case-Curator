using System;
using System.Collections.Generic;
using UnityEngine;

public enum StickerRarity
{
    HighGrade = 0,
    Remarkable = 1,
    Exotic = 2,
    Extraordinary = 3,
    Contraband = 4
}

/// <summary>
/// Sticker content is a specialised SkinData asset so every unapplied sticker
/// can use the existing inventory, storage, favourite, transaction and save
/// systems without introducing a second inventory implementation.
/// </summary>
[CreateAssetMenu(
    fileName = "NewSticker",
    menuName = "Case Curator/Stickers/Sticker Data")]
public class StickerData : SkinData
{
    [Header("Sticker Identity")]
    public string displayName;
    public StickerRarity stickerRarity = StickerRarity.HighGrade;
    [Min(0f)] public float marketValue;

    [Header("Source")]
    public List<CaseData> capsules = new List<CaseData>();
    public string tournamentEvent;
    public string teamName;
    public string playerName;
    [Min(0)] public int year;

    [Header("Import Metadata")]
    public string effect;
    public string marketHashName;
    [TextArea(1, 3)] public string sourceImageUrl;

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName.Trim();

            if (!string.IsNullOrWhiteSpace(skinName))
                return skinName.Trim();

            return name;
        }
    }

    public string PrimaryCapsuleName
    {
        get
        {
            if (capsules == null)
                return "";

            for (int i = 0; i < capsules.Count; i++)
            {
                CaseData capsule = capsules[i];

                if (capsule != null &&
                    !string.IsNullOrWhiteSpace(capsule.caseName))
                {
                    return capsule.caseName.Trim();
                }
            }

            return "";
        }
    }

    private void OnEnable()
    {
        NormalizeCompatibilityFields();
    }

    private void OnValidate()
    {
        NormalizeCompatibilityFields();
    }

    private void NormalizeCompatibilityFields()
    {
        weaponName = "Sticker";
        skinName = DisplayName;
        isVanilla = true;
        canBeStatTrak = false;
        canBeSouvenir = false;
        minFloat = 0f;
        maxFloat = 0f;
        vanillaPrice = Mathf.Max(0f, marketValue);
        vanillaStatTrakPrice = 0f;

        // Existing inventory/card systems still expect the weapon-skin rarity
        // enum. The dedicated sticker rarity above remains the player-facing one.
        rarity = GetCompatibilityRarity(stickerRarity);

        if (capsules == null)
            capsules = new List<CaseData>();
    }

    public static Rarity GetCompatibilityRarity(StickerRarity value)
    {
        switch (value)
        {
            case StickerRarity.Remarkable:
                return Rarity.Restricted;
            case StickerRarity.Exotic:
                return Rarity.Classified;
            case StickerRarity.Extraordinary:
            case StickerRarity.Contraband:
                return Rarity.Covert;
            default:
                return Rarity.MilSpec;
        }
    }
}

public static class StickerRarityUtility
{
    public static string GetDisplayName(StickerRarity rarity)
    {
        switch (rarity)
        {
            case StickerRarity.HighGrade: return "High Grade";
            case StickerRarity.Remarkable: return "Remarkable";
            case StickerRarity.Exotic: return "Exotic";
            case StickerRarity.Extraordinary: return "Extraordinary";
            case StickerRarity.Contraband: return "Contraband";
            default: return rarity.ToString();
        }
    }

    public static Color GetColor(StickerRarity rarity)
    {
        switch (rarity)
        {
            case StickerRarity.HighGrade:
                return new Color32(75, 105, 255, 255);
            case StickerRarity.Remarkable:
                return new Color32(136, 71, 255, 255);
            case StickerRarity.Exotic:
                return new Color32(211, 44, 230, 255);
            case StickerRarity.Extraordinary:
                return new Color32(235, 75, 75, 255);
            case StickerRarity.Contraband:
                return new Color32(228, 174, 57, 255);
            default:
                return Color.white;
        }
    }

    public static bool TryParse(string value, out StickerRarity rarity)
    {
        string normalized = (value ?? "")
            .Replace(" ", "")
            .Replace("-", "")
            .Trim();

        if (Enum.TryParse(normalized, true, out rarity))
            return true;

        rarity = StickerRarity.HighGrade;
        return false;
    }
}

public static class StickerItemUtility
{
    public static bool IsSticker(InventoryItem item)
    {
        return item != null && item.skin is StickerData;
    }

    public static bool IsSticker(SkinData data)
    {
        return data is StickerData;
    }

    public static StickerData GetSticker(InventoryItem item)
    {
        return item != null ? item.skin as StickerData : null;
    }
}
