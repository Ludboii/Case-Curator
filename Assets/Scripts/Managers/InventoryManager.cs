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

    private float cachedTotalMarketValue;

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

    public bool HasSpaceInStorage(int storageIndex, int additionalItems = 1)
    {
        if (!IsStorageUnlocked(storageIndex))
            return false;

        if (additionalItems <= 0)
            return true;

        return GetStorageItemCount(storageIndex) + additionalItems <=
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

        int count = 0;

        foreach (InventoryItem item in items)
        {
            if (item != null && item.storageIndex == storageIndex)
                count++;
        }

        return count;
    }

    public int GetFirstStorageWithSpace(int preferredStorageIndex = -1)
    {
        if (IsStorageUnlocked(preferredStorageIndex) &&
            HasSpaceInStorage(preferredStorageIndex))
        {
            return preferredStorageIndex;
        }

        for (int i = 0; i < unlockedStoragePages; i++)
        {
            if (HasSpaceInStorage(i))
                return i;
        }

        return -1;
    }

    public int FindNextStorageWithSpace(int fromStorageIndex)
    {
        if (unlockedStoragePages <= 1)
            return -1;

        int start = Mathf.Clamp(fromStorageIndex, 0, unlockedStoragePages - 1);

        for (int offset = 1; offset < unlockedStoragePages; offset++)
        {
            int index = (start + offset) % unlockedStoragePages;

            if (HasSpaceInStorage(index))
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
        cachedTotalMarketValue += item.marketValue;

        NotifyInventoryChanged(true);

        Debug.Log(
            $"Added item to Storage {destination + 1}: " +
            $"{SkinDisplayUtility.GetDisplayName(item.skin)}. " +
            $"Inventory count: {items.Count}/{TotalCapacity}");
    }

    public int AddItems(List<InventoryItem> itemsToAdd)
    {
        if (itemsToAdd == null || itemsToAdd.Count == 0)
            return 0;

        int addedCount = 0;
        float addedValue = 0f;

        foreach (InventoryItem item in itemsToAdd)
        {
            if (item == null)
                continue;

            int destination = GetFirstStorageWithSpace(item.storageIndex);

            if (destination < 0)
                break;

            PrepareItemForInventory(item);
            item.storageIndex = destination;
            items.Add(item);
            addedValue += item.marketValue;
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

        if (string.IsNullOrWhiteSpace(item.instanceId))
            item.instanceId = Guid.NewGuid().ToString();

        item.marketValue = PriceCalculator.GetPrice(item);
    }

    public bool RemoveItem(InventoryItem item)
    {
        if (item == null)
            return false;

        bool removed = items.Remove(item);

        if (!removed)
            return false;

        cachedTotalMarketValue -= Mathf.Max(0f, item.marketValue);

        if (cachedTotalMarketValue < 0f)
            cachedTotalMarketValue = 0f;

        NotifyInventoryChanged(true);
        return true;
    }

    public int RemoveItemsByInstanceIds(HashSet<string> instanceIds)
    {
        if (instanceIds == null || instanceIds.Count == 0)
            return 0;

        float removedValue = 0f;

        int removedCount = items.RemoveAll(item =>
        {
            bool remove =
                item != null &&
                !string.IsNullOrWhiteSpace(item.instanceId) &&
                instanceIds.Contains(item.instanceId);

            if (remove)
                removedValue += Mathf.Max(0f, item.marketValue);

            return remove;
        });

        if (removedCount <= 0)
            return 0;

        cachedTotalMarketValue -= removedValue;

        if (cachedTotalMarketValue < 0f)
            cachedTotalMarketValue = 0f;

        NotifyInventoryChanged(true);
        return removedCount;
    }

    public InventoryItem GetItemByInstanceId(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return null;

        foreach (InventoryItem item in items)
        {
            if (item != null && item.instanceId == instanceId)
                return item;
        }

        return null;
    }

    public List<InventoryItem> GetItemsCopy()
    {
        return new List<InventoryItem>(items);
    }

    public List<InventoryItem> GetItemsInStorageCopy(int storageIndex)
    {
        List<InventoryItem> storageItems = new List<InventoryItem>();

        if (!IsStorageUnlocked(storageIndex))
            return storageItems;

        foreach (InventoryItem item in items)
        {
            if (item != null && item.storageIndex == storageIndex)
                storageItems.Add(item);
        }

        return storageItems;
    }

    // Compatibility method retained for existing callers. Storage containers
    // are now explicit ownership tabs rather than slices of one global list.
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

    public bool MoveItemToStorage(InventoryItem item, int destinationStorageIndex)
    {
        if (item == null || !items.Contains(item))
            return false;

        if (!IsStorageUnlocked(destinationStorageIndex))
            return false;

        if (item.storageIndex == destinationStorageIndex)
            return true;

        if (!HasSpaceInStorage(destinationStorageIndex))
            return false;

        int previousStorage = item.storageIndex;
        item.storageIndex = destinationStorageIndex;

        NotifyInventoryChanged(true);

        Debug.Log(
            $"Moved {SkinDisplayUtility.GetDisplayName(item.skin)} from " +
            $"Storage {previousStorage + 1} to Storage {destinationStorageIndex + 1}.");

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

        foreach (InventoryItem item in itemsToUpdate)
        {
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

    public bool SetFavoriteByInstanceId(string instanceId, bool favorite)
    {
        InventoryItem item = GetItemByInstanceId(instanceId);
        return item != null && SetFavorite(item, favorite);
    }

    public void ClearInventory()
    {
        items.Clear();
        cachedTotalMarketValue = 0f;
        NotifyInventoryChanged(true);
    }

    public void ReplaceInventory(List<InventoryItem> loadedItems)
    {
        items.Clear();

        if (loadedItems != null)
        {
            foreach (InventoryItem item in loadedItems)
            {
                if (item != null && item.skin != null)
                    items.Add(item);
            }
        }

        NormalizeStorageAssignments();
        RecalculateCachedTotalMarketValue();
        NotifyInventoryChanged(false);

        Debug.Log(
            $"Inventory loaded. Item count: {items.Count}/{TotalCapacity}");
    }

    public void SetStorageData(int loadedPages, int loadedItemsPerPage)
    {
        unlockedStoragePages = Mathf.Max(1, loadedPages);
        itemsPerStoragePage = Mathf.Max(1, loadedItemsPerPage);
        activeStorageIndex = Mathf.Clamp(
            activeStorageIndex,
            0,
            unlockedStoragePages - 1);

        NormalizeStorageAssignments();
        NotifyInventoryChanged(false);
    }

    public void UnlockStoragePage()
    {
        unlockedStoragePages++;
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
        NotifyInventoryChanged(true);

        Debug.Log(
            $"Storage container size increased to {itemsPerStoragePage}. " +
            $"Total capacity: {TotalCapacity}");
    }

    public void RecalculateCachedTotalMarketValue()
    {
        cachedTotalMarketValue = 0f;

        foreach (InventoryItem item in items)
        {
            if (item == null || item.skin == null)
                continue;

            if (item.marketValue <= 0f)
                item.marketValue = PriceCalculator.GetPrice(item);

            cachedTotalMarketValue += item.marketValue;
        }
    }

    private void NormalizeStorageAssignments()
    {
        unlockedStoragePages = Mathf.Max(1, unlockedStoragePages);
        itemsPerStoragePage = Mathf.Max(1, itemsPerStoragePage);

        int[] counts = new int[unlockedStoragePages];
        int overflowCount = 0;

        foreach (InventoryItem item in items)
        {
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
                // Never delete an item from an over-capacity legacy/debug save.
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

    private void NotifyInventoryChanged(bool markSaveDirty)
    {
        if (markSaveDirty && SaveManager.Instance != null)
            SaveManager.Instance.MarkDirty();

        OnInventoryChanged?.Invoke();
    }
}
