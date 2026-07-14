using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    public event Action OnInventoryChanged;
    public event Action<int> OnActiveStorageChanged;

    [Header("Storage Settings")]
    [SerializeField] private int unlockedStoragePages = 1;
    [SerializeField] private int itemsPerStoragePage = 50;
    [SerializeField] private int activeStorageIndex;

    [SerializeField]
    private List<InventoryItem> items = new List<InventoryItem>();

    [Header("Diagnostics")]
    [SerializeField] private bool verboseItemLogging;

    private readonly Dictionary<string, InventoryItem> itemByInstanceId =
        new Dictionary<string, InventoryItem>(StringComparer.Ordinal);

    private int[] storageItemCounts = Array.Empty<int>();
    private float cachedTotalMarketValue;
    private long nextAcquisitionSequence = 1;

    public IReadOnlyList<InventoryItem> Items => items;
    public int Count => items.Count;
    public int UnlockedStoragePages => unlockedStoragePages;
    public int ItemsPerStoragePage => itemsPerStoragePage;
    public int ActiveStorageIndex => activeStorageIndex;
    public int TotalCapacity => unlockedStoragePages * itemsPerStoragePage;
    public float TotalMarketValue => cachedTotalMarketValue;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        unlockedStoragePages = Mathf.Max(1, unlockedStoragePages);
        itemsPerStoragePage = Mathf.Max(1, itemsPerStoragePage);
        activeStorageIndex = Mathf.Clamp(
            activeStorageIndex,
            0,
            unlockedStoragePages - 1);

        NormalizeStorageAssignments();
        NormalizeAcquisitionSequences();
        RebuildRuntimeCaches();
        RecalculateCachedTotalMarketValue();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool HasSpace()
    {
        return GetFirstStorageWithSpace() >= 0;
    }

    public bool HasSpaceInStorage(
        int storageIndex,
        int additionalItems = 1)
    {
        if (!IsStorageUnlocked(storageIndex))
            return false;

        if (additionalItems <= 0)
            return true;

        EnsureStorageCountCache();

        return storageItemCounts[storageIndex] + additionalItems <=
               itemsPerStoragePage;
    }

    public bool IsStorageUnlocked(int storageIndex)
    {
        return storageIndex >= 0 && storageIndex < unlockedStoragePages;
    }

    public int GetStorageItemCount(int storageIndex)
    {
        if (!IsStorageUnlocked(storageIndex))
            return 0;

        EnsureStorageCountCache();
        return storageItemCounts[storageIndex];
    }

    public int GetFirstStorageWithSpace(int preferredStorageIndex = -1)
    {
        EnsureStorageCountCache();

        if (IsStorageUnlocked(preferredStorageIndex) &&
            storageItemCounts[preferredStorageIndex] < itemsPerStoragePage)
        {
            return preferredStorageIndex;
        }

        for (int i = 0; i < unlockedStoragePages; i++)
        {
            if (storageItemCounts[i] < itemsPerStoragePage)
                return i;
        }

        return -1;
    }

    public int FindNextStorageWithSpace(int fromStorageIndex)
    {
        if (unlockedStoragePages <= 1)
            return -1;

        EnsureStorageCountCache();

        int start = Mathf.Clamp(
            fromStorageIndex,
            0,
            unlockedStoragePages - 1);

        for (int offset = 1; offset < unlockedStoragePages; offset++)
        {
            int index = (start + offset) % unlockedStoragePages;

            if (storageItemCounts[index] < itemsPerStoragePage)
                return index;
        }

        return -1;
    }

    public void AddItem(InventoryItem item)
    {
        if (item == null)
        {
            Debug.LogWarning("Tried to add null InventoryItem.");
            return;
        }

        int destination = GetFirstStorageWithSpace(item.storageIndex);

        if (destination < 0)
        {
            Debug.LogWarning(
                $"Inventory is full. Items: {items.Count}/{TotalCapacity}");
            return;
        }

        PrepareItemForInventory(item);
        item.storageIndex = destination;
        items.Add(item);
        RegisterItem(item);
        cachedTotalMarketValue += Mathf.Max(0f, item.marketValue);

        NotifyInventoryChanged(true);

        if (verboseItemLogging)
        {
            Debug.Log(
                $"Added item to Storage {destination + 1}: " +
                $"{SkinDisplayUtility.GetDisplayName(item.skin)}. " +
                $"Inventory count: {items.Count}/{TotalCapacity}");
        }
    }

    public int AddItems(List<InventoryItem> itemsToAdd)
    {
        if (itemsToAdd == null || itemsToAdd.Count == 0)
            return 0;

        EnsureStorageCountCache();

        int addedCount = 0;
        float addedValue = 0f;

        for (int i = 0; i < itemsToAdd.Count; i++)
        {
            InventoryItem item = itemsToAdd[i];

            if (item == null)
                continue;

            int destination = GetFirstStorageWithSpace(item.storageIndex);

            if (destination < 0)
                break;

            PrepareItemForInventory(item);
            item.storageIndex = destination;
            items.Add(item);
            RegisterItem(item);
            addedValue += Mathf.Max(0f, item.marketValue);
            addedCount++;
        }

        if (addedCount > 0)
        {
            cachedTotalMarketValue += addedValue;
            NotifyInventoryChanged(true);
        }

        return addedCount;
    }

    private void PrepareItemForInventory(InventoryItem item)
    {
        if (item == null)
            return;

        if (string.IsNullOrWhiteSpace(item.instanceId) ||
            itemByInstanceId.ContainsKey(item.instanceId))
        {
            item.instanceId = Guid.NewGuid().ToString();
        }

        if (item.acquisitionSequence <= 0)
        {
            item.acquisitionSequence = nextAcquisitionSequence;
            nextAcquisitionSequence++;
        }
        else if (item.acquisitionSequence >= nextAcquisitionSequence)
        {
            nextAcquisitionSequence = item.acquisitionSequence + 1;
        }

        item.marketValue = PriceCalculator.GetPrice(item);
    }

    private void NormalizeAcquisitionSequences()
    {
        long highestSequence = 0;

        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem item = items[i];

            if (item == null)
                continue;

            if (item.acquisitionSequence <= 0)
                item.acquisitionSequence = i + 1;

            if (item.acquisitionSequence > highestSequence)
                highestSequence = item.acquisitionSequence;
        }

        nextAcquisitionSequence = highestSequence >= 1
            ? highestSequence + 1
            : 1;
    }

    public bool RemoveItem(InventoryItem item)
    {
        if (item == null)
            return false;

        bool removed = items.Remove(item);

        if (!removed)
            return false;

        UnregisterItem(item);
        cachedTotalMarketValue -= Mathf.Max(0f, item.marketValue);
        cachedTotalMarketValue = Mathf.Max(0f, cachedTotalMarketValue);

        NotifyInventoryChanged(true);
        return true;
    }

    public int RemoveItemsByInstanceIds(HashSet<string> instanceIds)
    {
        if (instanceIds == null || instanceIds.Count == 0)
            return 0;

        int removedCount = 0;
        float removedValue = 0f;

        for (int i = items.Count - 1; i >= 0; i--)
        {
            InventoryItem item = items[i];

            if (item == null ||
                string.IsNullOrWhiteSpace(item.instanceId) ||
                !instanceIds.Contains(item.instanceId))
            {
                continue;
            }

            removedValue += Mathf.Max(0f, item.marketValue);
            UnregisterItem(item);
            items.RemoveAt(i);
            removedCount++;
        }

        if (removedCount <= 0)
            return 0;

        cachedTotalMarketValue -= removedValue;
        cachedTotalMarketValue = Mathf.Max(0f, cachedTotalMarketValue);

        NotifyInventoryChanged(true);
        return removedCount;
    }

    public InventoryItem GetItemByInstanceId(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return null;

        itemByInstanceId.TryGetValue(instanceId, out InventoryItem item);
        return item;
    }

    public List<InventoryItem> GetItemsCopy()
    {
        return new List<InventoryItem>(items);
    }

    public List<InventoryItem> GetItemsInStorageCopy(int storageIndex)
    {
        List<InventoryItem> storageItems =
            new List<InventoryItem>(GetStorageItemCount(storageIndex));

        if (!IsStorageUnlocked(storageIndex))
            return storageItems;

        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem item = items[i];

            if (item != null && item.storageIndex == storageIndex)
                storageItems.Add(item);
        }

        return storageItems;
    }

    public List<InventoryItem> GetItemsOnStoragePage(int pageIndex)
    {
        return GetItemsInStorageCopy(pageIndex);
    }

    public bool SetActiveStorage(int storageIndex)
    {
        if (!IsStorageUnlocked(storageIndex))
            return false;

        if (activeStorageIndex == storageIndex)
            return true;

        activeStorageIndex = storageIndex;
        OnActiveStorageChanged?.Invoke(activeStorageIndex);
        return true;
    }

    public bool MoveItemToStorage(
        InventoryItem item,
        int destinationStorageIndex)
    {
        if (!IsOwnedItem(item))
            return false;

        if (!IsStorageUnlocked(destinationStorageIndex))
            return false;

        if (item.storageIndex == destinationStorageIndex)
            return true;

        EnsureStorageCountCache();

        if (storageItemCounts[destinationStorageIndex] >= itemsPerStoragePage)
            return false;

        int previousStorage = item.storageIndex;

        if (IsStorageUnlocked(previousStorage))
        {
            storageItemCounts[previousStorage] =
                Mathf.Max(0, storageItemCounts[previousStorage] - 1);
        }

        item.storageIndex = destinationStorageIndex;
        storageItemCounts[destinationStorageIndex]++;

        NotifyInventoryChanged(true);

        if (verboseItemLogging)
        {
            Debug.Log(
                $"Moved {SkinDisplayUtility.GetDisplayName(item.skin)} from " +
                $"Storage {previousStorage + 1} to " +
                $"Storage {destinationStorageIndex + 1}.");
        }

        return true;
    }

    public int MoveItemToNextStorage(InventoryItem item)
    {
        if (item == null)
            return -1;

        int destination = FindNextStorageWithSpace(item.storageIndex);

        if (destination < 0)
            return -1;

        return MoveItemToStorage(item, destination)
            ? destination
            : -1;
    }

    public bool SetFavorite(InventoryItem item, bool favorite)
    {
        if (item == null || item.favorite == favorite)
            return false;

        item.favorite = favorite;
        NotifyInventoryChanged(true);
        return true;
    }

    public int SetFavoriteBatch(
        List<InventoryItem> itemsToUpdate,
        bool favorite)
    {
        if (itemsToUpdate == null || itemsToUpdate.Count == 0)
            return 0;

        int changedCount = 0;

        for (int i = 0; i < itemsToUpdate.Count; i++)
        {
            InventoryItem item = itemsToUpdate[i];

            if (item == null || item.favorite == favorite)
                continue;

            item.favorite = favorite;
            changedCount++;
        }

        if (changedCount > 0)
            NotifyInventoryChanged(true);

        return changedCount;
    }

    public bool ToggleFavorite(InventoryItem item)
    {
        if (item == null)
            return false;

        item.favorite = !item.favorite;
        NotifyInventoryChanged(true);
        return true;
    }

    public bool SetFavoriteByInstanceId(
        string instanceId,
        bool favorite)
    {
        InventoryItem item = GetItemByInstanceId(instanceId);
        return item != null && SetFavorite(item, favorite);
    }

    public void ClearInventory()
    {
        items.Clear();
        itemByInstanceId.Clear();
        storageItemCounts = new int[Mathf.Max(1, unlockedStoragePages)];
        cachedTotalMarketValue = 0f;
        nextAcquisitionSequence = 1;
        NotifyInventoryChanged(true);
    }

    public void ReplaceInventory(List<InventoryItem> loadedItems)
    {
        items.Clear();

        if (loadedItems != null)
        {
            for (int i = 0; i < loadedItems.Count; i++)
            {
                InventoryItem item = loadedItems[i];

                if (item != null && item.skin != null)
                    items.Add(item);
            }
        }

        NormalizeStorageAssignments();
        NormalizeAcquisitionSequences();
        RebuildRuntimeCaches();
        RecalculateCachedTotalMarketValue();
        NotifyInventoryChanged(false);

        Debug.Log(
            $"Inventory loaded. Item count: {items.Count}/{TotalCapacity}");
    }

    public void SetStorageData(
        int loadedPages,
        int loadedItemsPerPage)
    {
        unlockedStoragePages = Mathf.Max(1, loadedPages);
        itemsPerStoragePage = Mathf.Max(1, loadedItemsPerPage);
        activeStorageIndex = Mathf.Clamp(
            activeStorageIndex,
            0,
            unlockedStoragePages - 1);

        NormalizeStorageAssignments();
        RebuildRuntimeCaches();
        NotifyInventoryChanged(false);
    }

    public void UnlockStoragePage()
    {
        unlockedStoragePages++;
        NormalizeStorageAssignments();
        RebuildRuntimeCaches();
        NotifyInventoryChanged(true);

        Debug.Log(
            $"Unlocked Storage {unlockedStoragePages}. " +
            $"Total capacity: {TotalCapacity}");
    }

    public void IncreaseStoragePageSize(int amount)
    {
        if (amount <= 0)
            return;

        itemsPerStoragePage += amount;
        NormalizeStorageAssignments();
        RebuildRuntimeCaches();
        NotifyInventoryChanged(true);

        Debug.Log(
            $"Storage container size increased to {itemsPerStoragePage}. " +
            $"Total capacity: {TotalCapacity}");
    }

    public void RecalculateCachedTotalMarketValue()
    {
        cachedTotalMarketValue = 0f;

        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem item = items[i];

            if (item == null || item.skin == null)
                continue;

            if (item.marketValue <= 0f)
                item.marketValue = PriceCalculator.GetPrice(item);

            cachedTotalMarketValue += Mathf.Max(0f, item.marketValue);
        }
    }

    private void NormalizeStorageAssignments()
    {
        unlockedStoragePages = Mathf.Max(1, unlockedStoragePages);
        itemsPerStoragePage = Mathf.Max(1, itemsPerStoragePage);

        int[] counts = new int[unlockedStoragePages];
        int overflowCount = 0;

        for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            InventoryItem item = items[itemIndex];

            if (item == null)
                continue;

            int desired = Mathf.Clamp(
                item.storageIndex,
                0,
                unlockedStoragePages - 1);

            if (counts[desired] < itemsPerStoragePage)
            {
                item.storageIndex = desired;
                counts[desired]++;
                continue;
            }

            int fallback = -1;

            for (int i = 0; i < unlockedStoragePages; i++)
            {
                if (counts[i] < itemsPerStoragePage)
                {
                    fallback = i;
                    break;
                }
            }

            if (fallback >= 0)
            {
                item.storageIndex = fallback;
                counts[fallback]++;
            }
            else
            {
                item.storageIndex = desired;
                counts[desired]++;
                overflowCount++;
            }
        }

        if (overflowCount > 0)
        {
            Debug.LogWarning(
                $"Inventory contains {overflowCount} item(s) above the current " +
                "storage capacity. No items were deleted.");
        }
    }

    private void RebuildRuntimeCaches()
    {
        itemByInstanceId.Clear();
        storageItemCounts = new int[Mathf.Max(1, unlockedStoragePages)];

        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem item = items[i];

            if (item == null)
                continue;

            if (string.IsNullOrWhiteSpace(item.instanceId) ||
                itemByInstanceId.ContainsKey(item.instanceId))
            {
                item.instanceId = Guid.NewGuid().ToString();
            }

            itemByInstanceId[item.instanceId] = item;

            if (IsStorageUnlocked(item.storageIndex))
                storageItemCounts[item.storageIndex]++;
        }
    }

    private void RegisterItem(InventoryItem item)
    {
        if (item == null)
            return;

        EnsureStorageCountCache();

        if (!string.IsNullOrWhiteSpace(item.instanceId))
            itemByInstanceId[item.instanceId] = item;

        if (IsStorageUnlocked(item.storageIndex))
            storageItemCounts[item.storageIndex]++;
    }

    private void UnregisterItem(InventoryItem item)
    {
        if (item == null)
            return;

        if (!string.IsNullOrWhiteSpace(item.instanceId))
            itemByInstanceId.Remove(item.instanceId);

        EnsureStorageCountCache();

        if (IsStorageUnlocked(item.storageIndex))
        {
            storageItemCounts[item.storageIndex] =
                Mathf.Max(0, storageItemCounts[item.storageIndex] - 1);
        }
    }

    private bool IsOwnedItem(InventoryItem item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.instanceId))
            return false;

        return itemByInstanceId.TryGetValue(
                   item.instanceId,
                   out InventoryItem owned) &&
               ReferenceEquals(owned, item);
    }

    private void EnsureStorageCountCache()
    {
        if (storageItemCounts == null ||
            storageItemCounts.Length != unlockedStoragePages)
        {
            RebuildRuntimeCaches();
        }
    }

    private void NotifyInventoryChanged(bool markSaveDirty)
    {
        if (markSaveDirty && SaveManager.Instance != null)
            SaveManager.Instance.MarkDirty();

        OnInventoryChanged?.Invoke();
    }
}
