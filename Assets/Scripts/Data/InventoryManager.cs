using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    public event Action OnInventoryChanged;

    [Header("Storage Settings")]
    [SerializeField] private int unlockedStoragePages = 1;
    [SerializeField] private int itemsPerStoragePage = 50;

    [SerializeField]
    private List<InventoryItem> items = new List<InventoryItem>();

    public IReadOnlyList<InventoryItem> Items => items;

    public int Count => items.Count;

    public int UnlockedStoragePages => unlockedStoragePages;
    public int ItemsPerStoragePage => itemsPerStoragePage;

    public int TotalCapacity => unlockedStoragePages * itemsPerStoragePage;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool HasSpace()
    {
        return items.Count < TotalCapacity;
    }

    public void AddItem(InventoryItem item)
    {
        if (item == null)
        {
            Debug.LogWarning("Tried to add null InventoryItem.");
            return;
        }

        if (!HasSpace())
        {
            Debug.LogWarning(
                $"Inventory is full. Items: {items.Count}/{TotalCapacity}");
            return;
        }

        if (string.IsNullOrWhiteSpace(item.instanceId))
        {
            item.instanceId = Guid.NewGuid().ToString();
        }

        item.marketValue = PriceCalculator.GetPrice(item);

        items.Add(item);

        OnInventoryChanged?.Invoke();

        Debug.Log(
            $"Added item to inventory: " +
            $"{SkinDisplayUtility.GetDisplayName(item.skin)}. " +
            $"Inventory count: {items.Count}/{TotalCapacity}");
    }

    public bool RemoveItem(InventoryItem item)
    {
        if (item == null)
            return false;

        bool removed = items.Remove(item);

        if (removed)
            OnInventoryChanged?.Invoke();

        return removed;
    }

    public bool RemoveItemByInstanceId(string instanceId)
    {
        InventoryItem item = GetItemByInstanceId(instanceId);

        if (item == null)
            return false;

        return RemoveItem(item);
    }

    public InventoryItem GetItemByInstanceId(string instanceId)
    {
        foreach (InventoryItem item in items)
        {
            if (item.instanceId == instanceId)
                return item;
        }

        return null;
    }

    public List<InventoryItem> GetItemsCopy()
    {
        return new List<InventoryItem>(items);
    }

    public List<InventoryItem> GetItemsOnStoragePage(int pageIndex)
    {
        List<InventoryItem> pageItems = new List<InventoryItem>();

        if (pageIndex < 0)
            return pageItems;

        if (pageIndex >= unlockedStoragePages)
            return pageItems;

        int startIndex = pageIndex * itemsPerStoragePage;
        int endIndex = Mathf.Min(startIndex + itemsPerStoragePage, items.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            pageItems.Add(items[i]);
        }

        return pageItems;
    }
    public bool SetFavorite(InventoryItem item, bool favorite)
{
    if (item == null)
        return false;

    item.favorite = favorite;
    OnInventoryChanged?.Invoke();

    return true;
}

public bool ToggleFavorite(InventoryItem item)
{
    if (item == null)
        return false;

    item.favorite = !item.favorite;
    OnInventoryChanged?.Invoke();

    return true;
}

public bool SetFavoriteByInstanceId(string instanceId, bool favorite)
{
    InventoryItem item = GetItemByInstanceId(instanceId);

    if (item == null)
        return false;

    return SetFavorite(item, favorite);
}

    public void ClearInventory()
    {
        items.Clear();
        OnInventoryChanged?.Invoke();
    }

    public void ReplaceInventory(List<InventoryItem> loadedItems)
    {
        items.Clear();

        if (loadedItems != null)
        {
            items.AddRange(loadedItems);
        }

        OnInventoryChanged?.Invoke();

        Debug.Log($"Inventory loaded. Item count: {items.Count}/{TotalCapacity}");
    }

    public void SetStorageData(int loadedPages, int loadedItemsPerPage)
    {
        unlockedStoragePages = Mathf.Max(1, loadedPages);
        itemsPerStoragePage = Mathf.Max(1, loadedItemsPerPage);

        OnInventoryChanged?.Invoke();
    }

    public void UnlockStoragePage()
    {
        unlockedStoragePages++;

        OnInventoryChanged?.Invoke();

        Debug.Log($"Unlocked storage page {unlockedStoragePages}. Total capacity: {TotalCapacity}");
    }

    public void IncreaseStoragePageSize(int amount)
    {
        if (amount <= 0)
            return;

        itemsPerStoragePage += amount;

        OnInventoryChanged?.Invoke();

        Debug.Log($"Storage page size increased to {itemsPerStoragePage}. Total capacity: {TotalCapacity}");
    }
}