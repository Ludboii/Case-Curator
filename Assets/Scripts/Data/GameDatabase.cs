using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "GameDatabase",
    menuName = "Case Catcher/Game Database")]
public class GameDatabase : ScriptableObject
{
    [Header("Core Content")]
    public List<SkinData> allSkins = new List<SkinData>();
    public List<StickerData> allStickers = new List<StickerData>();
    public List<CaseData> allCases = new List<CaseData>();
    public List<CollectionData> allCollections = new List<CollectionData>();

    [Header("Progression")]
    public UpgradeCatalog upgradeCatalog;

    [Header("Museum")]
    public MuseumBalanceData museumBalance;
    public MuseumCatalogConfig museumCatalog;
    public MuseumPresentConfig museumPresentConfig;
    public TrophyRoomBalanceData trophyRoomBalance;
    public AutoAcquisitionCatalogData autoAcquisitionCatalog;

    public List<MuseumMilestoneData> museumMilestones =
        new List<MuseumMilestoneData>();

    public SkinData GetSkinByApiId(string apiId)
    {
        if (string.IsNullOrWhiteSpace(apiId))
            return null;

        foreach (SkinData skin in allSkins)
        {
            if (skin != null && skin.apiId == apiId)
                return skin;
        }

        return null;
    }

    public StickerData GetStickerByApiId(string apiId)
    {
        if (string.IsNullOrWhiteSpace(apiId))
            return null;

        if (allStickers != null)
        {
            for (int i = 0; i < allStickers.Count; i++)
            {
                StickerData sticker = allStickers[i];

                if (sticker != null &&
                    string.Equals(sticker.apiId, apiId, StringComparison.Ordinal))
                {
                    return sticker;
                }
            }
        }

        return GetSkinByApiId(apiId) as StickerData;
    }

    public CaseData GetCaseByApiId(string apiId)
    {
        foreach (CaseData caseData in allCases)
        {
            if (caseData != null && caseData.apiId == apiId)
                return caseData;
        }

        return null;
    }

    public CollectionData GetCollectionByName(string collectionName)
    {
        foreach (CollectionData collection in allCollections)
        {
            if (collection != null && collection.collectionName == collectionName)
                return collection;
        }

        return null;
    }

    public UpgradeData GetUpgradeById(string upgradeId)
    {
        return upgradeCatalog != null
            ? upgradeCatalog.GetUpgradeById(upgradeId)
            : null;
    }

    public MuseumMilestoneData GetMuseumMilestoneById(string milestoneId)
    {
        if (string.IsNullOrWhiteSpace(milestoneId) ||
            museumMilestones == null)
        {
            return null;
        }

        for (int i = 0; i < museumMilestones.Count; i++)
        {
            MuseumMilestoneData milestone = museumMilestones[i];

            if (milestone != null &&
                string.Equals(
                    milestone.milestoneId,
                    milestoneId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return milestone;
            }
        }

        return null;
    }

    private void OnEnable()
    {
        EnsureCollections();
        NormalizeStickerRegistration();
        NormalizeCompatibilityRarities();
    }

    private void OnValidate()
    {
        EnsureCollections();
        NormalizeStickerRegistration();
        NormalizeCompatibilityRarities();
    }

    private void EnsureCollections()
    {
        if (allSkins == null)
            allSkins = new List<SkinData>();

        if (allStickers == null)
            allStickers = new List<StickerData>();

        if (allCases == null)
            allCases = new List<CaseData>();

        if (allCollections == null)
            allCollections = new List<CollectionData>();

        if (museumMilestones == null)
            museumMilestones = new List<MuseumMilestoneData>();
    }

    private void NormalizeStickerRegistration()
    {
        if (allSkins == null || allStickers == null)
            return;

        for (int i = allSkins.Count - 1; i >= 0; i--)
        {
            StickerData sticker = allSkins[i] as StickerData;

            if (sticker != null && !allStickers.Contains(sticker))
                allStickers.Add(sticker);
        }

        for (int i = allStickers.Count - 1; i >= 0; i--)
        {
            StickerData sticker = allStickers[i];

            if (sticker == null)
            {
                allStickers.RemoveAt(i);
                continue;
            }

            if (!allSkins.Contains(sticker))
                allSkins.Add(sticker);
        }
    }

    /// <summary>
    /// Contraband is not a separate project rarity yet. The M4A4 | Howl can be
    /// imported as RareSpecial by external CS data, which incorrectly sends it
    /// through Rare Special Vault progression. Until Contraband is introduced,
    /// keep this one compatibility item in the Covert tier everywhere.
    /// </summary>
    private void NormalizeCompatibilityRarities()
    {
        if (allSkins == null)
            return;

        for (int i = 0; i < allSkins.Count; i++)
        {
            SkinData skin = allSkins[i];

            if (skin == null ||
                skin is StickerData ||
                skin.rarity != Rarity.RareSpecial ||
                !string.Equals(
                    skin.weaponName,
                    "M4A4",
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    skin.skinName,
                    "Howl",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            skin.rarity = Rarity.Covert;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(skin);
#endif
        }
    }
}
