using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps cached InventoryItem values synchronized with live StickerData prices
/// and removes crafts after their owning skin is permanently consumed by sell,
/// tradeup or Museum donation. Trophy Room items are explicitly retained.
/// </summary>
public sealed class StickerCraftRuntimeMaintenance : MonoBehaviour
{
    private StickerApplicationService service;
    private float cleanupAt;
    private bool inventorySubscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<StickerCraftRuntimeMaintenance>() != null)
            return;

        GameObject go = new GameObject("StickerCraftRuntimeMaintenance");
        DontDestroyOnLoad(go);
        go.AddComponent<StickerCraftRuntimeMaintenance>();
    }

    private void Start()
    {
        service = StickerApplicationService.GetOrCreate();

        if (service != null)
            service.OnStickerStateChanged += HandleStickerStateChanged;

        TrySubscribeInventory();
        RecalculateLiveValues();
        cleanupAt = Time.unscaledTime + 3f;
    }

    private void Update()
    {
        TrySubscribeInventory();

        if (Time.unscaledTime < cleanupAt)
            return;

        cleanupAt = float.PositiveInfinity;
        CleanupOrphanedCrafts();
    }

    private void OnDestroy()
    {
        if (service != null)
            service.OnStickerStateChanged -= HandleStickerStateChanged;

        if (inventorySubscribed && InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= HandleInventoryChanged;
    }

    private void TrySubscribeInventory()
    {
        if (inventorySubscribed || InventoryManager.Instance == null)
            return;

        InventoryManager.Instance.OnInventoryChanged += HandleInventoryChanged;
        inventorySubscribed = true;
    }

    private void HandleStickerStateChanged()
    {
        RecalculateLiveValues();
        cleanupAt = Time.unscaledTime + 0.5f;
    }

    private void HandleInventoryChanged()
    {
        RecalculateLiveValues();

        // Trophy placement and retrieval can remove/add an item during one
        // method call. The delay lets TrophyRoomSaveData settle before cleanup.
        cleanupAt = Time.unscaledTime + 0.75f;
    }

    private static void RecalculateLiveValues()
    {
        if (InventoryManager.Instance == null)
            return;

        IReadOnlyList<InventoryItem> items = InventoryManager.Instance.Items;

        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem item = items[i];

            if (item != null && item.skin != null)
                item.marketValue = PriceCalculator.GetPrice(item);
        }

        InventoryManager.Instance.RecalculateCachedTotalMarketValue();
    }

    private void CleanupOrphanedCrafts()
    {
        if (SaveManager.Instance == null ||
            SaveManager.Instance.Museum == null ||
            SaveManager.Instance.Museum.stickerSystem == null ||
            SaveManager.Instance.Museum.stickerSystem.crafts == null ||
            InventoryManager.Instance == null)
        {
            return;
        }

        HashSet<string> retained = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<InventoryItem> items = InventoryManager.Instance.Items;

        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem item = items[i];

            if (item != null && !string.IsNullOrWhiteSpace(item.instanceId))
                retained.Add(item.instanceId);
        }

        TrophyRoomSaveData trophy = SaveManager.Instance.Museum.trophyRoom;

        if (trophy != null && trophy.displayedItems != null)
        {
            for (int i = 0; i < trophy.displayedItems.Count; i++)
            {
                TrophyDisplaySlotSaveData display = trophy.displayedItems[i];

                if (display == null)
                    continue;

                string id = display.storedItem != null
                    ? display.storedItem.instanceId
                    : display.inventoryItemInstanceId;

                if (!string.IsNullOrWhiteSpace(id))
                    retained.Add(id);
            }
        }

        List<string> remove = new List<string>();
        List<StickerCraftSaveData> crafts =
            SaveManager.Instance.Museum.stickerSystem.crafts;

        for (int i = 0; i < crafts.Count; i++)
        {
            StickerCraftSaveData craft = crafts[i];

            if (craft != null &&
                !string.IsNullOrWhiteSpace(craft.skinInventoryItemInstanceId) &&
                !retained.Contains(craft.skinInventoryItemInstanceId))
            {
                remove.Add(craft.skinInventoryItemInstanceId);
            }
        }

        for (int i = 0; i < remove.Count; i++)
        {
            if (service != null)
                service.DestroyCraft(remove[i]);
        }
    }
}
