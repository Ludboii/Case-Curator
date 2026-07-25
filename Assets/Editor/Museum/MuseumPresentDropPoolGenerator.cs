#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Populates the six Museum Present drop pools from GameDatabase.allCases.
/// Every CaseData container type is supported; tier assignment is based on the
/// existing CaseQuality field and remains editable after generation.
/// </summary>
public static class MuseumPresentDropPoolGenerator
{
    [MenuItem(
        "Tools/Case Curator/Museum/Populate Present Container Drop Pools")]
    public static void Populate()
    {
        GameDatabase database = FindTargetDatabase();

        if (database == null)
            return;

        if (database.museumPresentConfig == null)
        {
            EditorUtility.DisplayDialog(
                "Museum Present Config Missing",
                "Run Tools > Case Curator > Museum > Apply M4.5 Present Rewards first.",
                "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Populate Present Drop Pools",
            "This replaces the six generated Museum Present container pools " +
            "using GameDatabase.allCases and CaseQuality. Manually edited pool " +
            "entries will be replaced. Continue?",
            "Populate",
            "Cancel");

        if (!confirmed)
            return;

        MuseumPresentConfig config = database.museumPresentConfig;
        Undo.RecordObject(config, "Populate Museum Present Drop Pools");

        if (config.tiers == null)
            config.tiers = new List<MuseumPresentTierConfig>();

        for (int i = 0; i < MuseumPresentUtility.AllTiers.Length; i++)
        {
            MuseumPresentTier tier = MuseumPresentUtility.AllTiers[i];
            MuseumPresentTierConfig tierConfig = FindOrCreateTier(config, tier);

            tierConfig.containerDrops =
                MuseumPresentDropPoolUtility.BuildDefaultPool(
                    tier,
                    database.allCases);
            tierConfig.Normalize();
        }

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Populated Museum Present container pools. " +
            "Weapon cases, collection packages, souvenir packages, custom " +
            "cases and sticker capsules are all eligible when their CaseQuality " +
            "matches a tier's configured quality band.");
    }

    private static MuseumPresentTierConfig FindOrCreateTier(
        MuseumPresentConfig config,
        MuseumPresentTier tier)
    {
        for (int i = 0; i < config.tiers.Count; i++)
        {
            MuseumPresentTierConfig existing = config.tiers[i];

            if (existing != null && existing.tier == tier)
                return existing;
        }

        MuseumPresentTierConfig created =
            MuseumPresentConfig.CreateFallbackTier(tier);
        config.tiers.Add(created);
        return created;
    }

    private static GameDatabase FindTargetDatabase()
    {
        if (Selection.activeObject is GameDatabase selected)
            return selected;

        string[] guids = AssetDatabase.FindAssets("t:GameDatabase");

        if (guids == null || guids.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "GameDatabase Not Found",
                "Create or select a GameDatabase asset first.",
                "OK");
            return null;
        }

        if (guids.Length > 1)
        {
            EditorUtility.DisplayDialog(
                "Select GameDatabase",
                "More than one GameDatabase exists. Select the intended asset " +
                "in the Project window, then run the command again.",
                "OK");
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<GameDatabase>(
            AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}
#endif
