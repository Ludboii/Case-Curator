using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum InventorySortMode
{
    Newest,
    Oldest,
    HighestValue,
    LowestValue,
    LowestFloat,
    HighestFloat,
    RarityLowToHigh,
    RarityHighToLow,
    WeaponAZ,
    SkinAZ,
    CollectionAZ,
    FavoritesFirst,
    StatTrakFirst,
    SouvenirFirst
}

public enum RarityFilterMode
{
    All,
    OnlySelectedRarity,
    HideSelectedRarity
}

public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }

    [Header("References")]
    public Transform gridContent;
    public InventoryItemCardUI itemCardPrefab;

    [Header("Optional Text")]
    public TMP_Text itemCountText;
    public TMP_Text pageText;
    public TMP_Text inventoryValueText;

    [Header("Selection Text")]
    public TMP_Text selectionCountText;
    public TMP_Text selectedValueText;
    public TMP_Text selectAllButtonText;
    

    [Header("Storage Buttons")]
    public Button storageButton1;
    public Button storageButton2;
    public Button storageButton3;
    public Button addStorageButton;
    

    public TMP_Text storageButton1Text;
    public TMP_Text storageButton2Text;
    public TMP_Text storageButton3Text;

    [Header("Selection Buttons")]
    public Button selectButton;
    public Button sellSelectedButton;
    public Button favoriteSelectedButton;
    public Button unfavoriteSelectedButton;
    public Button cancelSelectionButton;
    public Button selectAllButton;

    [Header("Selection Button Text")]
    public TMP_Text selectButtonText;
    public TMP_Text sellSelectedButtonText;
    public TMP_Text favoriteSelectedButtonText;
    public TMP_Text unfavoriteSelectedButtonText;
    public TMP_Text cancelSelectionButtonText;

    [Header("Sort / Filter Panel")]
    public GameObject sortFilterPanel;
    public Button openSortFilterButton;
    public Button closeSortFilterButton;
    public Button resetSortFilterButton;
    public TMP_Text activeSortFilterText;

   [Header("Selling")]
[Range(0f, 1f)] public float bulkSellMultiplier = 1f;

[Header("Bulk Sell Confirmation")]
public bool confirmBulkSell = true;
public float bulkSellConfirmationThreshold = 0f;

    private int currentPage = 0;

    private InventorySortMode currentSortMode = InventorySortMode.Newest;

private readonly List<Rarity> onlyRarityFilters = new List<Rarity>();
private readonly List<Rarity> hiddenRarityFilters = new List<Rarity>();

    private readonly List<InventoryItemCardUI> spawnedCards =
        new List<InventoryItemCardUI>();

    private readonly List<InventoryItemCardUI> selectedCards =
        new List<InventoryItemCardUI>();

    public bool SelectionModeActive { get; private set; }

    private void Awake()
    {
        Instance = this;

        SetupButtonListeners();
if (selectAllButton != null)
{
    selectAllButton.onClick.RemoveAllListeners();
    selectAllButton.onClick.AddListener(SelectAllVisibleItems);
}
        if (sortFilterPanel != null)
            sortFilterPanel.SetActive(false);

        SetSelectionMode(false);
    }

    private void OnEnable()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= Refresh;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void SetupButtonListeners()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(EnterSelectionMode);
        }

        if (sellSelectedButton != null)
        {
            sellSelectedButton.onClick.RemoveAllListeners();
            sellSelectedButton.onClick.AddListener(SellSelectedItems);
        }

        if (favoriteSelectedButton != null)
        {
            favoriteSelectedButton.onClick.RemoveAllListeners();
            favoriteSelectedButton.onClick.AddListener(FavoriteSelectedItems);
        }

        if (unfavoriteSelectedButton != null)
        {
            unfavoriteSelectedButton.onClick.RemoveAllListeners();
            unfavoriteSelectedButton.onClick.AddListener(UnfavoriteSelectedItems);
        }

        if (cancelSelectionButton != null)
        {
            cancelSelectionButton.onClick.RemoveAllListeners();
            cancelSelectionButton.onClick.AddListener(CancelSelectionMode);
        }

        if (openSortFilterButton != null)
        {
            openSortFilterButton.onClick.RemoveAllListeners();
            openSortFilterButton.onClick.AddListener(OpenSortFilterPanel);
        }

        if (closeSortFilterButton != null)
        {
            closeSortFilterButton.onClick.RemoveAllListeners();
            closeSortFilterButton.onClick.AddListener(CloseSortFilterPanel);
        }

        if (resetSortFilterButton != null)
        {
            resetSortFilterButton.onClick.RemoveAllListeners();
            resetSortFilterButton.onClick.AddListener(ResetSortAndFilters);
        }
    }

    public void Refresh()
    {
        ClearCards();

        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("InventoryUI: No InventoryManager found.");

            UpdateTexts(
                0,
                0,
                1,
                0,
                0,
                0f);

            UpdateStorageButtons(1);
            UpdateSelectionUI();
            UpdateActiveSortFilterText();
            return;
        }

        List<InventoryItem> displayItems =
            InventoryManager.Instance.GetItemsCopy();

        ApplyRarityFilter(displayItems);
        ApplySorting(displayItems);

        int itemsPerPage = InventoryManager.Instance.ItemsPerStoragePage;
        int totalViewPages = Mathf.Max(
            1,
            Mathf.CeilToInt(displayItems.Count / (float)itemsPerPage));

        if (currentPage >= totalViewPages)
            currentPage = totalViewPages - 1;

        if (currentPage < 0)
            currentPage = 0;

        int startIndex = currentPage * itemsPerPage;
        int endIndex = Mathf.Min(startIndex + itemsPerPage, displayItems.Count);

        for (int i = startIndex; i < endIndex; i++)
        {
            InventoryItem item = displayItems[i];

            InventoryItemCardUI card =
                Instantiate(itemCardPrefab, gridContent);

            card.gameObject.SetActive(true);
            card.Setup(item);

            spawnedCards.Add(card);
        }

        float totalInventoryValue = CalculateTotalInventoryValue();

        UpdateTexts(
            InventoryManager.Instance.Count,
            InventoryManager.Instance.TotalCapacity,
            displayItems.Count,
            totalViewPages,
            currentPage,
            totalInventoryValue);

        UpdateStorageButtons(totalViewPages);
        ClearSelection();
        UpdateSelectionUI();
        UpdateActiveSortFilterText();
    }

    private void ApplyRarityFilter(List<InventoryItem> items)
{
    for (int i = items.Count - 1; i >= 0; i--)
    {
        InventoryItem item = items[i];

        if (item == null || item.skin == null)
        {
            items.RemoveAt(i);
            continue;
        }

        Rarity rarity = item.skin.rarity;

        bool passesOnlyFilter =
            onlyRarityFilters.Count == 0 ||
            onlyRarityFilters.Contains(rarity);

        bool passesHiddenFilter =
            !hiddenRarityFilters.Contains(rarity);

        if (!passesOnlyFilter || !passesHiddenFilter)
        {
            items.RemoveAt(i);
        }
    }
}

    private void ApplySorting(List<InventoryItem> items)
    {
        switch (currentSortMode)
        {
            case InventorySortMode.Newest:
                items.Sort((a, b) => GetSafeInstanceId(b).CompareTo(GetSafeInstanceId(a)));
                break;

            case InventorySortMode.Oldest:
                items.Sort((a, b) => GetSafeInstanceId(a).CompareTo(GetSafeInstanceId(b)));
                break;

            case InventorySortMode.HighestValue:
                items.Sort((a, b) => GetItemValue(b).CompareTo(GetItemValue(a)));
                break;

            case InventorySortMode.LowestValue:
                items.Sort((a, b) => GetItemValue(a).CompareTo(GetItemValue(b)));
                break;

            case InventorySortMode.LowestFloat:
                items.Sort((a, b) => GetSortFloat(a).CompareTo(GetSortFloat(b)));
                break;

            case InventorySortMode.HighestFloat:
                items.Sort((a, b) => GetSortFloat(b).CompareTo(GetSortFloat(a)));
                break;

            case InventorySortMode.RarityLowToHigh:
                items.Sort((a, b) => GetRarityRank(a).CompareTo(GetRarityRank(b)));
                break;

            case InventorySortMode.RarityHighToLow:
                items.Sort((a, b) => GetRarityRank(b).CompareTo(GetRarityRank(a)));
                break;

            case InventorySortMode.WeaponAZ:
                items.Sort((a, b) => GetWeaponName(a).CompareTo(GetWeaponName(b)));
                break;

            case InventorySortMode.SkinAZ:
                items.Sort((a, b) => GetSkinName(a).CompareTo(GetSkinName(b)));
                break;

            case InventorySortMode.CollectionAZ:
                items.Sort((a, b) => GetCollectionName(a).CompareTo(GetCollectionName(b)));
                break;

            case InventorySortMode.FavoritesFirst:
                items.Sort((a, b) => GetFavoriteRank(b).CompareTo(GetFavoriteRank(a)));
                break;

            case InventorySortMode.StatTrakFirst:
                items.Sort((a, b) => GetStatTrakRank(b).CompareTo(GetStatTrakRank(a)));
                break;

            case InventorySortMode.SouvenirFirst:
                items.Sort((a, b) => GetSouvenirRank(b).CompareTo(GetSouvenirRank(a)));
                break;
        }
    }

    private string GetSafeInstanceId(InventoryItem item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.instanceId))
            return "";

        return item.instanceId;
    }

    private float GetSortFloat(InventoryItem item)
    {
        if (item == null || item.skin == null)
            return 999f;

        if (item.isVanilla || item.floatValue < 0)
            return 999f;

        return (float)item.floatValue;
    }

    private int GetRarityRank(InventoryItem item)
    {
        if (item == null || item.skin == null)
            return -1;

        return (int)item.skin.rarity;
    }

    private string GetWeaponName(InventoryItem item)
    {
        if (item == null || item.skin == null || item.skin.weaponName == null)
            return "";

        return item.skin.weaponName;
    }

    private string GetSkinName(InventoryItem item)
    {
        if (item == null || item.skin == null)
            return "";

        if (item.skin.isVanilla)
            return "Vanilla";

        return item.skin.skinName ?? "";
    }

    private string GetCollectionName(InventoryItem item)
    {
        if (item == null || item.skin == null)
            return "";

        return item.skin.collection ?? "";
    }

    private int GetFavoriteRank(InventoryItem item)
    {
        return item != null && item.favorite ? 1 : 0;
    }

    private int GetStatTrakRank(InventoryItem item)
    {
        return item != null && item.statTrak ? 1 : 0;
    }

    private int GetSouvenirRank(InventoryItem item)
    {
        return item != null && item.souvenir ? 1 : 0;
    }

    private float CalculateTotalInventoryValue()
    {
        if (InventoryManager.Instance == null)
            return 0f;

        float totalValue = 0f;

        foreach (InventoryItem item in InventoryManager.Instance.Items)
        {
            if (item == null || item.skin == null)
                continue;

            if (item.marketValue <= 0f)
            {
                item.marketValue = PriceCalculator.GetPrice(item);
            }

            totalValue += item.marketValue;
        }

        return totalValue;
    }

    private float GetItemValue(InventoryItem item)
    {
        if (item == null || item.skin == null)
            return 0f;

        if (item.marketValue <= 0f)
        {
            item.marketValue = PriceCalculator.GetPrice(item);
        }

        return item.marketValue;
    }

    private void ClearCards()
    {
        foreach (InventoryItemCardUI card in spawnedCards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }

        spawnedCards.Clear();
        selectedCards.Clear();
    }

    private void UpdateTexts(
        int totalItems,
        int totalCapacity,
        int filteredItems,
        int totalViewPages,
        int currentViewPage,
        float totalInventoryValue)
    {
        if (itemCountText != null)
        {
            bool hasActiveFilters =
    onlyRarityFilters.Count > 0 ||
    hiddenRarityFilters.Count > 0;

if (!hasActiveFilters)
{
    itemCountText.text = $"{totalItems} / {totalCapacity}\nitems";
}
else
{
    itemCountText.text =
        $"{filteredItems} shown\n{totalItems} / {totalCapacity} items";
}
        }

        if (pageText != null)
        {
            pageText.text = $"Page {currentViewPage + 1} / {totalViewPages}";
        }

        if (inventoryValueText != null)
        {
            inventoryValueText.text = $"Value: {totalInventoryValue:0.##}";
        }
    }

    private void UpdateStorageButtons(int totalViewPages)
    {
        UpdateOneStorageButton(storageButton1, storageButton1Text, 0, totalViewPages);
        UpdateOneStorageButton(storageButton2, storageButton2Text, 1, totalViewPages);
        UpdateOneStorageButton(storageButton3, storageButton3Text, 2, totalViewPages);
    }

    private void UpdateOneStorageButton(
        Button button,
        TMP_Text text,
        int pageIndex,
        int totalViewPages)
    {
        bool available = pageIndex < totalViewPages;

        if (button != null)
        {
            button.interactable = available;
        }

        if (text != null)
        {
            if (available)
                text.text = (pageIndex + 1).ToString();
            else
                text.text = "X";
        }
    }

    public void GoToPage(int pageIndex)
    {
        if (InventoryManager.Instance == null)
            return;

        if (pageIndex < 0)
            return;

        List<InventoryItem> displayItems =
            InventoryManager.Instance.GetItemsCopy();

        ApplyRarityFilter(displayItems);

        int itemsPerPage = InventoryManager.Instance.ItemsPerStoragePage;
        int totalViewPages = Mathf.Max(
            1,
            Mathf.CeilToInt(displayItems.Count / (float)itemsPerPage));

        if (pageIndex >= totalViewPages)
            return;

        currentPage = pageIndex;
        Refresh();
    }

    public void UnlockStoragePageForTesting()
    {
        if (InventoryManager.Instance == null)
            return;

        InventoryManager.Instance.UnlockStoragePage();
        Refresh();
    }

    public void IncreaseStorageSizeForTesting()
    {
        if (InventoryManager.Instance == null)
            return;

        InventoryManager.Instance.IncreaseStoragePageSize(10);
        Refresh();
    }

    public void EnterSelectionMode()
    {
        SetSelectionMode(true);
    }

    public void CancelSelectionMode()
    {
        SetSelectionMode(false);
    }

    private void SetSelectionMode(bool active)
    {
        SelectionModeActive = active;

        ClearSelection();
        UpdateSelectionUI();
    }

    public void ToggleSelectedItem(InventoryItemCardUI card)
    {
        if (!SelectionModeActive)
            return;

        if (card == null)
            return;

        InventoryItem item = card.GetItem();

        if (item == null || item.skin == null)
            return;

        if (selectedCards.Contains(card))
        {
            selectedCards.Remove(card);
            card.SetSelected(false);
        }
        else
        {
            selectedCards.Add(card);
            card.SetSelected(true);
        }

        UpdateSelectionUI();
    }

    private void ClearSelection()
    {
        foreach (InventoryItemCardUI card in selectedCards)
        {
            if (card != null)
                card.SetSelected(false);
        }

        selectedCards.Clear();

        foreach (InventoryItemCardUI card in spawnedCards)
        {
            if (card != null)
                card.SetSelected(false);
        }
    }

    private void UpdateSelectionUI()
    {
        int selectedCount = selectedCards.Count;
        float selectedValue = CalculateSelectedValue();

        if (selectButton != null)
            selectButton.gameObject.SetActive(!SelectionModeActive);

        if (sellSelectedButton != null)
            sellSelectedButton.gameObject.SetActive(SelectionModeActive);

        if (favoriteSelectedButton != null)
            favoriteSelectedButton.gameObject.SetActive(SelectionModeActive);

        if (unfavoriteSelectedButton != null)
            unfavoriteSelectedButton.gameObject.SetActive(SelectionModeActive);

        if (cancelSelectionButton != null)
            cancelSelectionButton.gameObject.SetActive(SelectionModeActive);

        if (sellSelectedButton != null)
            sellSelectedButton.interactable = SelectionModeActive && selectedCount > 0;

        if (favoriteSelectedButton != null)
            favoriteSelectedButton.interactable = SelectionModeActive && selectedCount > 0;

        if (unfavoriteSelectedButton != null)
            unfavoriteSelectedButton.interactable = SelectionModeActive && selectedCount > 0;

        if (cancelSelectionButton != null)
            cancelSelectionButton.interactable = SelectionModeActive;

        if (selectButtonText != null)
            selectButtonText.text = "Select";

        if (sellSelectedButtonText != null)
            sellSelectedButtonText.text = $"Sell ({selectedCount})";

        if (favoriteSelectedButtonText != null)
            favoriteSelectedButtonText.text = $"Favorite ({selectedCount})";

        if (unfavoriteSelectedButtonText != null)
            unfavoriteSelectedButtonText.text = $"Unfavorite ({selectedCount})";

        if (cancelSelectionButtonText != null)
            cancelSelectionButtonText.text = "Cancel";

        if (selectAllButton != null)
            selectAllButton.gameObject.SetActive(SelectionModeActive);

if (selectAllButton != null)
    selectAllButton.interactable = SelectionModeActive && spawnedCards.Count > 0;

if (selectAllButtonText != null)
    selectAllButtonText.text = "Select All";

        if (selectionCountText != null)
        {
            selectionCountText.gameObject.SetActive(SelectionModeActive);
            selectionCountText.text = $"Selected: {selectedCount}";
        }

        if (selectedValueText != null)
        {
            selectedValueText.gameObject.SetActive(SelectionModeActive);
            selectedValueText.text = $"Selected Value: {selectedValue:0.##}";
        }
    }

    private float CalculateSelectedValue()
    {
        float total = 0f;

        foreach (InventoryItemCardUI card in selectedCards)
        {
            if (card == null)
                continue;

            InventoryItem item = card.GetItem();

            if (item == null || item.skin == null)
                continue;

            total += GetItemValue(item) * bulkSellMultiplier;
        }

        return total;
    }

    public void FavoriteSelectedItems()
    {
        if (!SelectionModeActive)
            return;

        if (selectedCards.Count == 0)
            return;

        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("InventoryUI: Cannot favorite selected items because InventoryManager is missing.");
            return;
        }

        List<InventoryItem> itemsToFavorite = GetSelectedItemsCopy();

        int favoritedCount = 0;

        foreach (InventoryItem item in itemsToFavorite)
        {
            if (item == null || item.skin == null)
                continue;

            if (!item.favorite)
            {
                InventoryManager.Instance.SetFavorite(item, true);
                favoritedCount++;
            }
        }

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveGame();
        }

        Debug.Log($"Favorited {favoritedCount} selected item(s).");

        SetSelectionMode(false);
        Refresh();
    }

    public void UnfavoriteSelectedItems()
    {
        if (!SelectionModeActive)
            return;

        if (selectedCards.Count == 0)
            return;

        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("InventoryUI: Cannot unfavorite selected items because InventoryManager is missing.");
            return;
        }

        List<InventoryItem> itemsToUnfavorite = GetSelectedItemsCopy();

        int unfavoritedCount = 0;

        foreach (InventoryItem item in itemsToUnfavorite)
        {
            if (item == null || item.skin == null)
                continue;

            if (item.favorite)
            {
                InventoryManager.Instance.SetFavorite(item, false);
                unfavoritedCount++;
            }
        }

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveGame();
        }

        Debug.Log($"Unfavorited {unfavoritedCount} selected item(s).");

        SetSelectionMode(false);
        Refresh();
    }

public void SellSelectedItems()
{
    if (!SelectionModeActive)
        return;

    if (selectedCards.Count == 0)
        return;

    if (InventoryManager.Instance == null)
    {
        Debug.LogWarning("InventoryUI: Cannot sell selected items because InventoryManager is missing.");
        return;
    }

    if (SaveManager.Instance == null)
    {
        Debug.LogWarning("InventoryUI: Cannot sell selected items because SaveManager is missing.");
        return;
    }

    List<InventoryItem> itemsToSell = GetSelectedItemsCopy();

    int sellableCount = 0;
    int skippedFavoriteCount = 0;
    float totalGoldEarned = 0f;

    foreach (InventoryItem item in itemsToSell)
    {
        if (item == null || item.skin == null)
            continue;

        if (item.favorite)
        {
            skippedFavoriteCount++;
            continue;
        }

        totalGoldEarned += GetItemValue(item) * bulkSellMultiplier;
        sellableCount++;
    }

    if (sellableCount == 0)
    {
        Debug.Log($"InventoryUI: No selected items could be sold. Skipped {skippedFavoriteCount} favorited item(s).");
        return;
    }

    bool shouldConfirm =
        confirmBulkSell &&
        totalGoldEarned >= bulkSellConfirmationThreshold;

    if (shouldConfirm && SellConfirmationPopupUI.Instance != null)
    {
        string message =
            $"Sell {sellableCount} selected item(s) for {totalGoldEarned:0.##} gold?";

        if (skippedFavoriteCount > 0)
        {
            message += $"\n\n{skippedFavoriteCount} favorited item(s) will be skipped.";
        }

        SellConfirmationPopupUI.Instance.Show(
            "Confirm Bulk Sell",
            message,
            "Sell",
            "Cancel",
            ExecuteSellSelectedItems);

        return;
    }

    ExecuteSellSelectedItems();
}

private void ExecuteSellSelectedItems()
{
    if (!SelectionModeActive)
        return;

    if (selectedCards.Count == 0)
        return;

    if (InventoryManager.Instance == null || SaveManager.Instance == null)
        return;

    List<InventoryItem> itemsToSell = GetSelectedItemsCopy();

    HashSet<string> instanceIdsToSell = new HashSet<string>();
    float totalGoldEarned = 0f;
    int skippedFavoriteCount = 0;

    foreach (InventoryItem item in itemsToSell)
    {
        if (item == null || item.skin == null)
            continue;

        if (item.favorite)
        {
            skippedFavoriteCount++;
            continue;
        }

        if (string.IsNullOrWhiteSpace(item.instanceId))
            continue;

        instanceIdsToSell.Add(item.instanceId);
        totalGoldEarned += GetItemValue(item) * bulkSellMultiplier;
    }

    if (instanceIdsToSell.Count == 0)
        return;

    int soldCount = InventoryManager.Instance.RemoveItemsByInstanceIds(instanceIdsToSell);

    if (soldCount <= 0)
        return;

    if (totalGoldEarned > 0f)
        SaveManager.Instance.AddGold(totalGoldEarned);

    SaveManager.Instance.SaveGame();

    Debug.Log(
        $"Sold {soldCount} selected item(s) for {totalGoldEarned:0.##} gold. " +
        $"Skipped {skippedFavoriteCount} favorited item(s).");

    SetSelectionMode(false);
    Refresh();
}

    private List<InventoryItem> GetSelectedItemsCopy()
    {
        List<InventoryItem> items = new List<InventoryItem>();

        foreach (InventoryItemCardUI card in selectedCards)
        {
            if (card == null)
                continue;

            InventoryItem item = card.GetItem();

            if (item == null || item.skin == null)
                continue;

            items.Add(item);
        }

        return items;
    }

    public void OpenSortFilterPanel()
    {
        if (sortFilterPanel != null)
            sortFilterPanel.SetActive(true);
    }

    public void CloseSortFilterPanel()
    {
        if (sortFilterPanel != null)
            sortFilterPanel.SetActive(false);
    }

    public void ResetSortAndFilters()
{
    currentSortMode = InventorySortMode.Newest;

    onlyRarityFilters.Clear();
    hiddenRarityFilters.Clear();

    currentPage = 0;

    Refresh();
}
    private void SetSortMode(InventorySortMode sortMode)
    {
        currentSortMode = sortMode;
        currentPage = 0;
        Refresh();
    }

    private void ToggleOnlyRarity(Rarity rarity)
{
    if (onlyRarityFilters.Contains(rarity))
    {
        onlyRarityFilters.Remove(rarity);
    }
    else
    {
        onlyRarityFilters.Add(rarity);

        if (hiddenRarityFilters.Contains(rarity))
            hiddenRarityFilters.Remove(rarity);
    }

    currentPage = 0;
    Refresh();
}

private void ToggleHiddenRarity(Rarity rarity)
{
    if (hiddenRarityFilters.Contains(rarity))
    {
        hiddenRarityFilters.Remove(rarity);
    }
    else
    {
        hiddenRarityFilters.Add(rarity);

        if (onlyRarityFilters.Contains(rarity))
            onlyRarityFilters.Remove(rarity);
    }

    currentPage = 0;
    Refresh();
}

public void ClearRarityFilter()
{
    onlyRarityFilters.Clear();
    hiddenRarityFilters.Clear();

    currentPage = 0;
    Refresh();
}
public void SelectAllVisibleItems()
{
    if (!SelectionModeActive)
        SetSelectionMode(true);

    selectedCards.Clear();

    foreach (InventoryItemCardUI card in spawnedCards)
    {
        if (card == null)
            continue;

        InventoryItem item = card.GetItem();

        if (item == null || item.skin == null)
            continue;

        if (!selectedCards.Contains(card))
            selectedCards.Add(card);

        card.SetSelected(true);
    }

    UpdateSelectionUI();
}

 private void UpdateActiveSortFilterText()
{
    if (activeSortFilterText == null)
        return;

    string onlyText = BuildRarityListText(onlyRarityFilters);
    string hiddenText = BuildRarityListText(hiddenRarityFilters);

    string filterText = "All rarities";

    if (onlyRarityFilters.Count > 0 && hiddenRarityFilters.Count > 0)
    {
        filterText = $"Only: {onlyText}\nHide: {hiddenText}";
    }
    else if (onlyRarityFilters.Count > 0)
    {
        filterText = $"Only: {onlyText}";
    }
    else if (hiddenRarityFilters.Count > 0)
    {
        filterText = $"Hide: {hiddenText}";
    }

    activeSortFilterText.text =
        $"Sort:\n{GetSortDisplayName(currentSortMode)}\nFilter:\n{filterText}";
}

private string BuildRarityListText(List<Rarity> rarities)
{
    if (rarities == null || rarities.Count == 0)
        return "None";

    string text = "";

    for (int i = 0; i < rarities.Count; i++)
    {
        if (i > 0)
            text += ", ";

        text += GetRarityDisplayName(rarities[i]);
    }

    return text;
}

    private string GetSortDisplayName(InventorySortMode sortMode)
    {
        switch (sortMode)
        {
            case InventorySortMode.Newest: return "Newest";
            case InventorySortMode.Oldest: return "Oldest";
            case InventorySortMode.HighestValue: return "Highest Value";
            case InventorySortMode.LowestValue: return "Lowest Value";
            case InventorySortMode.LowestFloat: return "Lowest Float";
            case InventorySortMode.HighestFloat: return "Highest Float";
            case InventorySortMode.RarityLowToHigh: return "Rarity Low → High";
            case InventorySortMode.RarityHighToLow: return "Rarity High → Low";
            case InventorySortMode.WeaponAZ: return "Weapon A-Z";
            case InventorySortMode.SkinAZ: return "Skin A-Z";
            case InventorySortMode.CollectionAZ: return "Collection A-Z";
            case InventorySortMode.FavoritesFirst: return "Favorites First";
            case InventorySortMode.StatTrakFirst: return "StatTrak First";
            case InventorySortMode.SouvenirFirst: return "Souvenir First";
            default: return sortMode.ToString();
        }
    }

    private string GetRarityDisplayName(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Consumer: return "Consumer";
            case Rarity.Industrial: return "Industrial";
            case Rarity.MilSpec: return "Mil-Spec";
            case Rarity.Restricted: return "Restricted";
            case Rarity.Classified: return "Classified";
            case Rarity.Covert: return "Covert";
            case Rarity.RareSpecial: return "Rare Special";
            default: return rarity.ToString();
        }
    }

    public void SortNewest() => SetSortMode(InventorySortMode.Newest);
    public void SortOldest() => SetSortMode(InventorySortMode.Oldest);
    public void SortHighestValue() => SetSortMode(InventorySortMode.HighestValue);
    public void SortLowestValue() => SetSortMode(InventorySortMode.LowestValue);
    public void SortLowestFloat() => SetSortMode(InventorySortMode.LowestFloat);
    public void SortHighestFloat() => SetSortMode(InventorySortMode.HighestFloat);
    public void SortRarityLowToHigh() => SetSortMode(InventorySortMode.RarityLowToHigh);
    public void SortRarityHighToLow() => SetSortMode(InventorySortMode.RarityHighToLow);
    public void SortWeaponAZ() => SetSortMode(InventorySortMode.WeaponAZ);
    public void SortSkinAZ() => SetSortMode(InventorySortMode.SkinAZ);
    public void SortCollectionAZ() => SetSortMode(InventorySortMode.CollectionAZ);
    public void SortFavoritesFirst() => SetSortMode(InventorySortMode.FavoritesFirst);
    public void SortStatTrakFirst() => SetSortMode(InventorySortMode.StatTrakFirst);
    public void SortSouvenirFirst() => SetSortMode(InventorySortMode.SouvenirFirst);

    public void OnlyConsumer() => ToggleOnlyRarity(Rarity.Consumer);
public void OnlyIndustrial() => ToggleOnlyRarity(Rarity.Industrial);
public void OnlyMilSpec() => ToggleOnlyRarity(Rarity.MilSpec);
public void OnlyRestricted() => ToggleOnlyRarity(Rarity.Restricted);
public void OnlyClassified() => ToggleOnlyRarity(Rarity.Classified);
public void OnlyCovert() => ToggleOnlyRarity(Rarity.Covert);
public void OnlyRareSpecial() => ToggleOnlyRarity(Rarity.RareSpecial);

public void HideConsumer() => ToggleHiddenRarity(Rarity.Consumer);
public void HideIndustrial() => ToggleHiddenRarity(Rarity.Industrial);
public void HideMilSpec() => ToggleHiddenRarity(Rarity.MilSpec);
public void HideRestricted() => ToggleHiddenRarity(Rarity.Restricted);
public void HideClassified() => ToggleHiddenRarity(Rarity.Classified);
public void HideCovert() => ToggleHiddenRarity(Rarity.Covert);
public void HideRareSpecial() => ToggleHiddenRarity(Rarity.RareSpecial);
}