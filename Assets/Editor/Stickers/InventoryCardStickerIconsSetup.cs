#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures every prefab containing InventoryItemCardUI also carries the applied
/// sticker icon companion. The companion generates its four tiny overlay Images
/// at runtime, so no manual prefab hierarchy or Image assignments are required.
/// </summary>
public static class InventoryCardStickerIconsSetup
{
    private const string SessionKey =
        "CaseCurator.InventoryCardStickerIconsSetup.RanThisSession";

    [InitializeOnLoadMethod]
    private static void ScheduleAutomaticSetup()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += EnsureAllInventoryCardPrefabs;
    }

    [MenuItem(
        "Tools/Case Curator/Stickers/Auto-wire Inventory Card Sticker Icons")]
    public static void EnsureAllInventoryCardPrefabs()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int changedPrefabs = 0;
        int changedCards = 0;

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;

            try
            {
                InventoryItemCardUI[] cards =
                    prefabRoot.GetComponentsInChildren<InventoryItemCardUI>(true);

                for (int j = 0; j < cards.Length; j++)
                {
                    InventoryItemCardUI card = cards[j];

                    if (card == null ||
                        card.GetComponent<InventoryCardStickerIconsUI>() != null)
                    {
                        continue;
                    }

                    card.gameObject.AddComponent<InventoryCardStickerIconsUI>();
                    changed = true;
                    changedCards++;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    changedPrefabs++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        if (changedPrefabs > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Sticker inventory-card setup added applied-sticker overlays " +
                $"to {changedCards} card component(s) across " +
                $"{changedPrefabs} prefab(s).");
        }
    }
}
#endif
