using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One-way runtime compatibility for items unboxed before concrete Doppler
/// phase/gem assets existed. Old generic items keep their instance IDs, float,
/// StatTrak state, favorite state and pattern seed; only the SkinData reference
/// is upgraded to a deterministic concrete variant.
/// </summary>
public sealed class DopplerLegacyInventoryMigration : MonoBehaviour
{
    private const float RetryInterval = 0.5f;
    private const float MaximumRetryTime = 20f;

    private float nextAttemptAt;
    private float stopTryingAt;
    private bool completed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<DopplerLegacyInventoryMigration>() != null)
            return;

        GameObject go = new GameObject("DopplerLegacyInventoryMigration");
        DontDestroyOnLoad(go);
        go.AddComponent<DopplerLegacyInventoryMigration>();
    }

    private void Awake()
    {
        stopTryingAt = Time.unscaledTime + MaximumRetryTime;
    }

    private void Update()
    {
        if (completed || Time.unscaledTime < nextAttemptAt)
            return;

        nextAttemptAt = Time.unscaledTime + RetryInterval;

        InventoryManager inventory = InventoryManager.Instance;
        GameDatabase database = DopplerVariantUtility.GetDatabase();

        if (inventory == null || database == null)
        {
            if (Time.unscaledTime >= stopTryingAt)
                completed = true;
            return;
        }

        IReadOnlyList<InventoryItem> items = inventory.Items;
        bool foundGeneric = false;
        bool changed = false;

        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem item = items[i];

            if (item == null || !DopplerVariantUtility.IsGenericParent(item.skin))
                continue;

            foundGeneric = true;
            SkinData concrete = DopplerVariantUtility.ResolveLegacyVariant(
                item.skin,
                item.patternId,
                database);

            if (concrete == null || concrete == item.skin)
                continue;

            item.skin = concrete;
            item.marketValue = PriceCalculator.GetPrice(item);
            changed = true;
        }

        if (changed)
        {
            // Rebuild the same inventory references so all pooled cards/listeners
            // receive one normal inventory refresh and the migrated IDs persist.
            inventory.ReplaceInventory(inventory.GetItemsCopy());

            if (SaveManager.Instance != null)
                SaveManager.Instance.SaveGame();

            Debug.Log(
                "Doppler migration: upgraded legacy generic Doppler inventory " +
                "items to concrete phases/gems.");
        }

        if (!foundGeneric || changed || Time.unscaledTime >= stopTryingAt)
            completed = true;
    }
}
