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

    [Header("Legacy Variant Parents")]
    [Tooltip(
        "Generic Doppler / Gamma Doppler assets remain referenced by CaseData " +
        "as weighted family entries. Concrete phase/gem assets live in allSkins.")]
    public List<SkinData> legacyDopplerParents = new List<SkinData>();

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

        if (allSkins != null)
        {
            for (int i = 0; i < allSkins.Count; i++)
            {
                SkinData skin = allSkins[i];

                if (skin != null &&
                    string.Equals(skin.apiId, apiId, StringComparison.Ordinal))
                {
                    return skin;
                }
            }
        }

        // Old saves may still point at the generic Doppler family asset. Those
        // parents are kept outside allSkins so Museum/catalog systems only see
        // the actual phases and gems, but save loading can still resolve them.
        if (legacyDopplerParents != null)
        {
            for (int i = 0; i < legacyDopplerParents.Count; i++)
            {
                SkinData parent = legacyDopplerParents[i];

                if (parent != null &&
                    string.Equals(parent.apiId, apiId, StringComparison.Ordinal))
                {
                    return parent;
                }
            }
        }

        // SaveManager historically resolves every inventory item through this
        // method. StickerData remains a SkinData subclass for inventory/save
        // compatibility, but is intentionally kept out of allSkins so Museum,
        // Tradeup and weapon-skin catalogues cannot ingest stickers.
        return GetStickerByApiIdInternal(apiId);
    }

    public StickerData GetStickerByApiId(string apiId)
    {
        if (string.IsNullOrWhiteSpace(apiId))
            return null;

        return GetStickerByApiIdInternal(apiId);
    }

    private StickerData GetStickerByApiIdInternal(string apiId)
    {
        if (allStickers == null)
            return null;

        for (int i = 0; i < allStickers.Count; i++)
        {
            StickerData sticker = allStickers[i];

            if (sticker != null &&
                string.Equals(sticker.apiId, apiId, StringComparison.Ordinal))
            {
                return sticker;
            }
        }

        return null;
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
        NormalizeDopplerRegistration();
        NormalizeCompatibilityRarities();
    }

    private void OnValidate()
    {
        EnsureCollections();
        NormalizeStickerRegistration();
        NormalizeDopplerRegistration();
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

        if (legacyDopplerParents == null)
            legacyDopplerParents = new List<SkinData>();

        if (museumMilestones == null)
            museumMilestones = new List<MuseumMilestoneData>();
    }

    private void NormalizeStickerRegistration()
    {
        if (allSkins == null || allStickers == null)
            return;

        // Migrate stickers created by the earliest implementation into their
        // dedicated list, then remove them from allSkins. Inventory save/load still
        // resolves them through GetSkinByApiId's sticker fallback.
        for (int i = allSkins.Count - 1; i >= 0; i--)
        {
            StickerData sticker = allSkins[i] as StickerData;

            if (sticker == null)
                continue;

            if (!allStickers.Contains(sticker))
                allStickers.Add(sticker);

            allSkins.RemoveAt(i);
        }

        for (int i = allStickers.Count - 1; i >= 0; i--)
        {
            if (allStickers[i] == null)
                allStickers.RemoveAt(i);
        }
    }

    private void NormalizeDopplerRegistration()
    {
        if (allSkins == null || legacyDopplerParents == null)
            return;

        for (int i = allSkins.Count - 1; i >= 0; i--)
        {
            SkinData skin = allSkins[i];

            if (!IsGenericDopplerParent(skin))
                continue;

            if (!legacyDopplerParents.Contains(skin))
                legacyDopplerParents.Add(skin);

            allSkins.RemoveAt(i);
        }

        for (int i = legacyDopplerParents.Count - 1; i >= 0; i--)
        {
            SkinData parent = legacyDopplerParents[i];

            if (parent == null)
            {
                legacyDopplerParents.RemoveAt(i);
                continue;
            }

            if (!IsGenericDopplerParent(parent))
            {
                legacyDopplerParents.RemoveAt(i);

                if (!allSkins.Contains(parent))
                    allSkins.Add(parent);
            }
        }
    }

    private static bool IsGenericDopplerParent(SkinData skin)
    {
        if (skin == null || skin.isVanilla)
            return false;

        string name = (skin.skinName ?? "").Trim();
        return string.Equals(name, "Doppler", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "Gamma Doppler", StringComparison.OrdinalIgnoreCase);
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
