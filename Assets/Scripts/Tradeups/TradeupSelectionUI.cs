using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TradeupSelectionUI : MonoBehaviour
{
    private enum TradeupFloatSort
    {
        LowestFloat,
        HighestFloat
    }

    [Header("Tradeup")]
    [SerializeField] private TradeupFlowUI tradeupFlow;

    [Header("Grid")]
    [SerializeField] private Transform gridContent;
    [SerializeField] private InventoryItemCardUI itemCardPrefab;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Search and Sorting")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private TMP_Dropdown floatSortDropdown;

    [Header("Optional Text")]
    [SerializeField] private TMP_Text eligibleItemCountText;
    [SerializeField] private TMP_Text emptyInventoryText;

    private readonly List<SpawnedTradeupCard> spawnedCards =
        new List<SpawnedTradeupCard>();

    private TradeupFloatSort currentSort =
        TradeupFloatSort.LowestFloat;

    private bool inventorySubscribed;
    private bool controlsSubscribed;

    private sealed class SpawnedTradeupCard
    {
        public InventoryItem item;
        public InventoryItemCardUI card;
    }

    private void Awake()
    {
        ConfigureSortDropdown();
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

    private void ConfigureSortDropdown()
    {
        if (floatSortDropdown == null)
            return;

        floatSortDropdown.ClearOptions();

        floatSortDropdown.AddOptions(
            new List<string>
            {
                "Lowest Float",
                "Highest Float"
            });

        floatSortDropdown.SetValueWithoutNotify(
            (int)currentSort);
    }

    private void SubscribeToInventory()
    {
        if (inventorySubscribed ||
            InventoryManager.Instance == null)
        {
            return;
        }

        InventoryManager.Instance.OnInventoryChanged +=
            HandleInventoryChanged;

        inventorySubscribed = true;
    }

    private void UnsubscribeFromInventory()
    {
        if (!inventorySubscribed ||
            InventoryManager.Instance == null)
        {
            inventorySubscribed = false;
            return;
        }

        InventoryManager.Instance.OnInventoryChanged -=
            HandleInventoryChanged;

        inventorySubscribed = false;
    }

    private void SubscribeToControls()
    {
        if (controlsSubscribed)
            return;

        if (searchInput != null)
        {
            searchInput.onValueChanged.AddListener(
                HandleSearchChanged);
        }

        if (floatSortDropdown != null)
        {
            floatSortDropdown.onValueChanged.AddListener(
                HandleSortChanged);
        }

        controlsSubscribed = true;
    }

    private void UnsubscribeFromControls()
    {
        if (!controlsSubscribed)
            return;

        if (searchInput != null)
        {
            searchInput.onValueChanged.RemoveListener(
                HandleSearchChanged);
        }

        if (floatSortDropdown != null)
        {
            floatSortDropdown.onValueChanged.RemoveListener(
                HandleSortChanged);
        }

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
        currentSort = value == 1
            ? TradeupFloatSort.HighestFloat
            : TradeupFloatSort.LowestFloat;

        RebuildGrid();
    }

    public void RebuildGrid()
    {
        ClearSpawnedCards();

        if (tradeupFlow == null)
        {
            Debug.LogError(
                "TradeupSelectionUI: TradeupFlowUI is not assigned.");

            SetEmptyState(true, "Tradeup Flow is missing.");
            return;
        }

        if (InventoryManager.Instance == null)
        {
            Debug.LogError(
                "TradeupSelectionUI: InventoryManager is missing.");

            SetEmptyState(true, "Inventory is unavailable.");
            return;
        }

        if (gridContent == null ||
            itemCardPrefab == null)
        {
            Debug.LogError(
                "TradeupSelectionUI: Grid Content or card prefab " +
                "is not assigned.");

            SetEmptyState(true, "Tradeup grid is not configured.");
            return;
        }

        string searchTerm = searchInput != null
            ? Normalize(searchInput.text)
            : "";

        List<InventoryItem> eligibleItems =
            new List<InventoryItem>();

        IReadOnlyList<InventoryItem> inventoryItems =
            InventoryManager.Instance.Items;

        for (int i = 0; i < inventoryItems.Count; i++)
        {
            InventoryItem item = inventoryItems[i];

            if (!IsVisibleTradeupItem(item))
                continue;

            if (!MatchesSearch(item, searchTerm))
                continue;

            eligibleItems.Add(item);
        }

        SortItems(eligibleItems);

        for (int i = 0; i < eligibleItems.Count; i++)
        {
            SpawnCard(eligibleItems[i]);
        }

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

        RefreshCardStates();

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    private bool IsVisibleTradeupItem(
        InventoryItem item)
    {
        if (item == null || item.skin == null)
            return false;

        // Uses the same fundamental rules as TradeupFlowUI:
        // no favorites, Souvenirs, Vanilla, Rare Special, or
        // items without CollectionData.
        if (!tradeupFlow.IsBasicTradeupCandidate(item))
            return false;

        return true;
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

        string displayName = Normalize(
            SkinDisplayUtility.GetDisplayName(skin));

        string weaponName = Normalize(skin.weaponName);
        string skinName = Normalize(skin.skinName);
        string collectionName = Normalize(skin.collection);

        if (skin.collectionData != null)
        {
            collectionName += " " +
                Normalize(
                    skin.collectionData.collectionName);
        }

        return displayName.Contains(normalizedSearch) ||
               weaponName.Contains(normalizedSearch) ||
               skinName.Contains(normalizedSearch) ||
               collectionName.Contains(normalizedSearch);
    }

    private void SortItems(
        List<InventoryItem> items)
    {
        items.Sort((first, second) =>
        {
            if (first == null && second == null)
                return 0;

            if (first == null)
                return 1;

            if (second == null)
                return -1;

            int result = first.floatValue.CompareTo(
                second.floatValue);

            if (currentSort ==
                TradeupFloatSort.HighestFloat)
            {
                result = -result;
            }

            if (result != 0)
                return result;

            string firstName =
                first.skin != null
                    ? SkinDisplayUtility.GetDisplayName(
                        first.skin)
                    : "";

            string secondName =
                second.skin != null
                    ? SkinDisplayUtility.GetDisplayName(
                        second.skin)
                    : "";

            return string.Compare(
                firstName,
                secondName,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    private void SpawnCard(
        InventoryItem item)
    {
        InventoryItemCardUI card =
            Instantiate(itemCardPrefab, gridContent);

        card.gameObject.SetActive(true);
        card.Setup(item);

        // InventoryItemCardUI normally opens SkinInspectUI or uses
        // the normal inventory selection system. Remove that listener
        // and make the card belong to the Tradeup selection system.
        if (card.button != null)
        {
            card.button.onClick.RemoveAllListeners();

            InventoryItem capturedItem = item;

            card.button.onClick.AddListener(
                () => HandleTradeupCardClicked(
                    capturedItem));
        }

        card.SetSelected(
            tradeupFlow.IsSelected(item));

        spawnedCards.Add(
            new SpawnedTradeupCard
            {
                item = item,
                card = card
            });
    }

    private void HandleTradeupCardClicked(
        InventoryItem item)
    {
        if (item == null)
            return;

        tradeupFlow.ToggleInput(item);
        RefreshCardStates();
    }

    private void RefreshCardStates()
    {
        for (int i = 0;
             i < spawnedCards.Count;
             i++)
        {
            SpawnedTradeupCard entry =
                spawnedCards[i];

            if (entry == null ||
                entry.item == null ||
                entry.card == null)
            {
                continue;
            }

            bool selected =
                tradeupFlow.IsSelected(entry.item);

            bool compatible =
                selected ||
                IsCompatibleWithCurrentSelection(
                    entry.item);

            entry.card.SetSelected(selected);

bool shouldShow =
    selected || compatible;

entry.card.gameObject.SetActive(shouldShow);

if (!shouldShow)
    continue;

entry.card.SetSelected(selected);

if (entry.card.button != null)
    entry.card.button.interactable = true;
        }
    }

    private bool IsCompatibleWithCurrentSelection(
        InventoryItem item)
    {
        if (!IsVisibleTradeupItem(item))
            return false;

        IReadOnlyList<InventoryItem> selected =
            tradeupFlow.SelectedInputs;

        if (selected == null ||
            selected.Count == 0)
        {
            return true;
        }

        InventoryItem first = selected[0];

        if (first == null || first.skin == null)
            return false;

        if (item.skin.rarity != first.skin.rarity)
            return false;

        if (item.statTrak != first.statTrak)
            return false;

        int requiredCount =
            first.skin.rarity == Rarity.Covert
                ? 5
                : 10;

        return selected.Count < requiredCount;
    }

    public void ClearTradeupSelection()
    {
        if (tradeupFlow == null)
            return;

        tradeupFlow.ClearSelection();
        RefreshCardStates();
    }

    private void ClearSpawnedCards()
    {
        for (int i = 0;
             i < spawnedCards.Count;
             i++)
        {
            SpawnedTradeupCard entry =
                spawnedCards[i];

            if (entry != null &&
                entry.card != null)
            {
                Destroy(entry.card.gameObject);
            }
        }

        spawnedCards.Clear();
    }

    private void SetEmptyState(
        bool visible,
        string message)
    {
        if (emptyInventoryText == null)
            return;

        emptyInventoryText.gameObject.SetActive(visible);
        emptyInventoryText.text = visible
            ? message
            : "";
    }

    private static string Normalize(
        string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }
}