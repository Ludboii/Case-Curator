using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TradeupSelectionUI : MonoBehaviour
{
    private enum TradeupSortMode
    {
        LowestFloat,
        HighestFloat,
        LowestPrice,
        HighestPrice
    }

    [Header("Tradeup")]
    [SerializeField] private TradeupFlowUI tradeupFlow;

    [Header("Grid")]
    [SerializeField] private Transform gridContent;
    [SerializeField] private InventoryItemCardUI itemCardPrefab;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Search and Sorting")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private TMP_Dropdown sortDropdown;

    [Header("Buttons")]
    [SerializeField] private Button clearSelectedButton;

    [Header("Optional Text")]
    [SerializeField] private TMP_Text eligibleItemCountText;
    [SerializeField] private TMP_Text emptyInventoryText;

    private readonly List<InventoryItemCardUI> cardPool =
        new List<InventoryItemCardUI>();

    private readonly List<InventoryItem> eligibleItems =
        new List<InventoryItem>();

    private TradeupSortMode currentSort = TradeupSortMode.LowestFloat;
    private int activeCardCount;
    private bool inventorySubscribed;
    private bool controlsSubscribed;

    private void Awake()
    {
        ConfigureSortDropdown();
        SetupButtons();
    }

    private void OnEnable()
    {
        SubscribeToInventory();
        SubscribeToControls();
        RebuildGrid();
    }

    private void OnDisable()
    {
        UnsubscribeFromInventory();
        UnsubscribeFromControls();
    }

    private void SetupButtons()
    {
        if (clearSelectedButton == null)
            return;

        clearSelectedButton.onClick.RemoveListener(ClearTradeupSelection);
        clearSelectedButton.onClick.AddListener(ClearTradeupSelection);
    }

    private void ConfigureSortDropdown()
    {
        if (sortDropdown == null)
            return;

        sortDropdown.ClearOptions();
        sortDropdown.AddOptions(
            new List<string>
            {
                "Lowest Float",
                "Highest Float",
                "Lowest Price",
                "Highest Price"
            });

        sortDropdown.SetValueWithoutNotify((int)currentSort);
    }

    private void SubscribeToInventory()
    {
        if (inventorySubscribed || InventoryManager.Instance == null)
            return;

        InventoryManager.Instance.OnInventoryChanged += HandleInventoryChanged;
        inventorySubscribed = true;
    }

    private void UnsubscribeFromInventory()
    {
        if (!inventorySubscribed || InventoryManager.Instance == null)
        {
            inventorySubscribed = false;
            return;
        }

        InventoryManager.Instance.OnInventoryChanged -= HandleInventoryChanged;
        inventorySubscribed = false;
    }

    private void SubscribeToControls()
    {
        if (controlsSubscribed)
            return;

        if (searchInput != null)
            searchInput.onValueChanged.AddListener(HandleSearchChanged);

        if (sortDropdown != null)
            sortDropdown.onValueChanged.AddListener(HandleSortChanged);

        controlsSubscribed = true;
    }

    private void UnsubscribeFromControls()
    {
        if (!controlsSubscribed)
            return;

        if (searchInput != null)
            searchInput.onValueChanged.RemoveListener(HandleSearchChanged);

        if (sortDropdown != null)
            sortDropdown.onValueChanged.RemoveListener(HandleSortChanged);

        controlsSubscribed = false;
    }

    private void HandleInventoryChanged()
    {
        RebuildGrid();
    }

    private void HandleSearchChanged(string value)
    {
        RebuildGrid();
    }

    private void HandleSortChanged(int value)
    {
        currentSort = (TradeupSortMode)Mathf.Clamp(
            value,
            0,
            Enum.GetValues(typeof(TradeupSortMode)).Length - 1);

        RebuildGrid();
    }

    public void RebuildGrid()
    {
        if (tradeupFlow == null)
        {
            Debug.LogError(
                "TradeupSelectionUI: TradeupFlowUI is not assigned.");
            SetEmptyState(true, "Tradeup Flow is missing.");
            SetActiveCardCount(0);
            return;
        }

        if (InventoryManager.Instance == null)
        {
            Debug.LogError(
                "TradeupSelectionUI: InventoryManager is missing.");
            SetEmptyState(true, "Inventory is unavailable.");
            SetActiveCardCount(0);
            return;
        }

        if (gridContent == null || itemCardPrefab == null)
        {
            Debug.LogError(
                "TradeupSelectionUI: Grid Content or card prefab is not assigned.");
            SetEmptyState(true, "Tradeup grid is not configured.");
            SetActiveCardCount(0);
            return;
        }

        string searchTerm = searchInput != null
            ? Normalize(searchInput.text)
            : "";

        eligibleItems.Clear();

        IReadOnlyList<InventoryItem> inventoryItems =
            InventoryManager.Instance.Items;

        for (int i = 0; i < inventoryItems.Count; i++)
        {
            InventoryItem item = inventoryItems[i];

            if (!IsVisibleTradeupItem(item))
                continue;

            if (!IsCompatibleWithCurrentSelection(item))
                continue;

            if (!MatchesSearch(item, searchTerm))
                continue;

            eligibleItems.Add(item);
        }

        SortItems(eligibleItems);
        EnsurePoolSize(eligibleItems.Count);

        for (int i = 0; i < eligibleItems.Count; i++)
        {
            InventoryItemCardUI card = cardPool[i];
            card.gameObject.SetActive(true);
            card.SetExternalClickHandler(HandleTradeupCardClicked);
            card.Setup(eligibleItems[i]);
            card.SetSelected(tradeupFlow.IsSelected(eligibleItems[i]));
        }

        SetActiveCardCount(eligibleItems.Count);

        if (eligibleItemCountText != null)
        {
            eligibleItemCountText.text =
                $"{eligibleItems.Count} eligible items";
        }

        SetEmptyState(
            eligibleItems.Count == 0,
            string.IsNullOrWhiteSpace(searchTerm)
                ? "No tradeupable items."
                : "No matching tradeupable items.");

        UpdateClearButton();
    }

    private void EnsurePoolSize(int requiredCount)
    {
        while (cardPool.Count < requiredCount)
        {
            InventoryItemCardUI card =
                Instantiate(itemCardPrefab, gridContent);

            card.SetExternalClickHandler(HandleTradeupCardClicked);
            card.gameObject.SetActive(false);
            cardPool.Add(card);
        }
    }

    private void SetActiveCardCount(int count)
    {
        activeCardCount = Mathf.Clamp(count, 0, cardPool.Count);

        for (int i = activeCardCount; i < cardPool.Count; i++)
        {
            InventoryItemCardUI card = cardPool[i];

            if (card == null)
                continue;

            card.SetSelected(false);
            card.gameObject.SetActive(false);
        }
    }

    private bool IsVisibleTradeupItem(InventoryItem item)
    {
        return item != null &&
               item.skin != null &&
               tradeupFlow.IsBasicTradeupCandidate(item);
    }

    private bool MatchesSearch(
        InventoryItem item,
        string normalizedSearch)
    {
        if (string.IsNullOrWhiteSpace(normalizedSearch))
            return true;

        if (item == null || item.skin == null)
            return false;

        SkinData skin = item.skin;

        string searchable =
            Normalize(SkinDisplayUtility.GetDisplayName(skin)) + " " +
            Normalize(skin.weaponName) + " " +
            Normalize(skin.skinName) + " " +
            Normalize(skin.collection);

        if (skin.collectionData != null)
            searchable += " " + Normalize(skin.collectionData.collectionName);

        return searchable.Contains(normalizedSearch);
    }

    private void SortItems(List<InventoryItem> items)
    {
        items.Sort((first, second) =>
        {
            if (first == null && second == null)
                return 0;
            if (first == null)
                return 1;
            if (second == null)
                return -1;

            int result;

            switch (currentSort)
            {
                case TradeupSortMode.HighestFloat:
                    result = second.floatValue.CompareTo(first.floatValue);
                    break;

                case TradeupSortMode.LowestPrice:
                    result = GetItemValue(first).CompareTo(GetItemValue(second));
                    break;

                case TradeupSortMode.HighestPrice:
                    result = GetItemValue(second).CompareTo(GetItemValue(first));
                    break;

                default:
                    result = first.floatValue.CompareTo(second.floatValue);
                    break;
            }

            if (result != 0)
                return result;

            string firstName = first.skin != null
                ? SkinDisplayUtility.GetDisplayName(first.skin)
                : "";

            string secondName = second.skin != null
                ? SkinDisplayUtility.GetDisplayName(second.skin)
                : "";

            return string.Compare(
                firstName,
                secondName,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    private static float GetItemValue(InventoryItem item)
    {
        if (item == null || item.skin == null)
            return 0f;

        if (item.marketValue <= 0f)
            item.marketValue = PriceCalculator.GetPrice(item);

        return item.marketValue;
    }

    private void HandleTradeupCardClicked(InventoryItemCardUI card)
    {
        if (card == null)
            return;

        InventoryItem item = card.GetItem();

        if (item == null)
            return;

        tradeupFlow.ToggleInput(item);

        // Pool objects remain alive. Rebuilding only rebinds cards and removes
        // incompatible items without Instantiate/Destroy spikes.
        RebuildGrid();
    }

    private bool IsCompatibleWithCurrentSelection(InventoryItem item)
    {
        if (!IsVisibleTradeupItem(item))
            return false;

        IReadOnlyList<InventoryItem> selected = tradeupFlow.SelectedInputs;

        if (selected == null || selected.Count == 0)
            return true;

        if (tradeupFlow.IsSelected(item))
            return true;

        InventoryItem first = selected[0];

        if (first == null || first.skin == null)
            return false;

        if (item.skin.rarity != first.skin.rarity)
            return false;

        if (item.statTrak != first.statTrak)
            return false;

        int requiredCount = first.skin.rarity == Rarity.Covert ? 5 : 10;
        return selected.Count < requiredCount;
    }

    public void ClearTradeupSelection()
    {
        if (tradeupFlow == null)
            return;

        tradeupFlow.ClearSelection();
        RebuildGrid();
    }

    private void UpdateClearButton()
    {
        if (clearSelectedButton == null)
            return;

        clearSelectedButton.interactable =
            tradeupFlow != null &&
            tradeupFlow.SelectedInputs.Count > 0;
    }

    private void SetEmptyState(bool visible, string message)
    {
        if (emptyInventoryText == null)
            return;

        emptyInventoryText.gameObject.SetActive(visible);
        emptyInventoryText.text = visible ? message : "";
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }
}
