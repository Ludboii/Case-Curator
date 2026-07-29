#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps imported sticker assets separated from weapon-skin catalogues and
/// ensures every Sticker Capsule has an editable dedicated sticker-rarity table.
/// </summary>
public sealed class StickerDatabaseGuard : AssetPostprocessor
{
    private static bool queued;

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        bool relevant = false;

        for (int i = 0; i < importedAssets.Length; i++)
        {
            string path = importedAssets[i];

            if (path.Contains("/Stickers/") ||
                path.EndsWith("GameDatabase.asset"))
            {
                relevant = true;
                break;
            }
        }

        if (!relevant || queued)
            return;

        queued = true;
        EditorApplication.delayCall += RunDelayed;
    }

    [MenuItem("Tools/Case Curator/Stickers/Validate Sticker Database Lists")]
    public static void ValidateNow()
    {
        int changed = NormalizeAllDatabases();
        AssetDatabase.SaveAssets();
        Debug.Log(
            changed > 0
                ? $"Normalized sticker data in {changed} GameDatabase asset(s)."
                : "Sticker database lists and capsule rarity tables are valid.");
    }

    private static void RunDelayed()
    {
        queued = false;
        int changed = NormalizeAllDatabases();

        if (changed > 0)
            AssetDatabase.SaveAssets();
    }

    private static int NormalizeAllDatabases()
    {
        int changedDatabases = 0;
        string[] guids = AssetDatabase.FindAssets("t:GameDatabase");

        for (int i = 0; i < guids.Length; i++)
        {
            GameDatabase database = AssetDatabase.LoadAssetAtPath<GameDatabase>(
                AssetDatabase.GUIDToAssetPath(guids[i]));

            if (database == null)
                continue;

            if (database.allSkins == null)
                database.allSkins = new List<SkinData>();
            if (database.allStickers == null)
                database.allStickers = new List<StickerData>();
            if (database.allCases == null)
                database.allCases = new List<CaseData>();

            bool changed = false;

            for (int index = database.allSkins.Count - 1; index >= 0; index--)
            {
                StickerData sticker = database.allSkins[index] as StickerData;

                if (sticker == null)
                    continue;

                if (!database.allStickers.Contains(sticker))
                    database.allStickers.Add(sticker);

                database.allSkins.RemoveAt(index);
                changed = true;
            }

            for (int index = database.allStickers.Count - 1; index >= 0; index--)
            {
                if (database.allStickers[index] == null)
                {
                    database.allStickers.RemoveAt(index);
                    changed = true;
                }
            }

            for (int index = 0; index < database.allCases.Count; index++)
            {
                CaseData capsule = database.allCases[index];

                if (capsule == null ||
                    capsule.containerType != CaseContainerType.StickerCapsule)
                {
                    continue;
                }

                string before = GetRarityTableSignature(capsule);
                StickerCapsuleRollUtility.EnsureDefaultRarityTable(capsule);
                string after = GetRarityTableSignature(capsule);

                if (before != after)
                {
                    EditorUtility.SetDirty(capsule);
                    changed = true;
                }
            }

            if (!changed)
                continue;

            EditorUtility.SetDirty(database);
            changedDatabases++;
        }

        return changedDatabases;
    }

    private static string GetRarityTableSignature(CaseData capsule)
    {
        if (capsule == null || capsule.stickerRarityChances == null)
            return "null";

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < capsule.stickerRarityChances.Count; i++)
        {
            StickerRarityChance chance = capsule.stickerRarityChances[i];

            if (chance == null)
            {
                builder.Append("null;");
                continue;
            }

            builder.Append((int)chance.rarity);
            builder.Append(':');
            builder.Append(chance.chance.ToString("R"));
            builder.Append(';');
        }

        return builder.ToString();
    }
}
#endif
