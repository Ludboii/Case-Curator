using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Paged Trophy inventory selector. Only one page of cards is instantiated, so
/// inventories with hundreds of skins do not create a large one-frame UI spike.
/// </summary>
public sealed class TrophySelectionPopupUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button closeButton;

    [Header("Search and Sort")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private TMP_Dropdown sortDropdown;

    [Header("Paged Inventory")]
    [SerializeField] private RectTransform content;
    [SerializeField] private TrophyInventoryItemCardUI itemCardPrefab;
    [SerializeField, Min(10)] private int itemsPerPage = 60;
    [SerializeField] private Button previousPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private TMP_Text pageText;
    [SerializeField] private TMP_Text emptyText;

    [Header("Selected Item Preview")]
    [SerializeField] private Image selectedIcon;
    [SerializeField] private TMP_Text selectedNameText;
    [SerializeField] private TMP_Text selectedDetailsText;
    [SerializeField] private TMP_Text powerBreakdownText;
    [SerializeField] private TMP_Text projectedBonusText;
    [SerializeField] private Button placeButton;
    [SerializeField] private TMP_Text placeButtonText;

    private readonly List<TrophyInventoryItemCardUI> cardPool =
        new List<TrophyInventoryItemCardUI>();
    private readonly List<InventoryItem> filteredItems =
        new List<InventoryItem>();

    private TrophyRoomService service;
    private TrophyRoomPanelUI owner;
    private InventoryItem selectedItem;
    private int slotIndex = -1;
    private int currentPage;

    private TrophyInventorySortMode SortMode =>
        (TrophyInventorySortMode)Mathf.Clamp(
            sortDropdown != null ? sortDropdown.value : 0,
            0,
            Enum.GetValues(typeof(TrophyInventorySortMode)).Length - 1);

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        BindButton(closeButton, Close);
        BindButton(placeButton, PlaceSelectedItem);
        BindButton(previousPageButton, PreviousPage);
        BindButton(nextPageButton, NextPage);

        if (searchInput != null)
        {
            searchInput.onValueChanged.RemoveListener(HandleFilterChanged);
            searchInput.onValueChanged.AddListener(HandleFilterChanged);
        }

        if (sortDropdown != null)
        {
            sortDropdown.onValueChanged.RemoveListener(HandleSortChanged);
            sortDropdown.onValueChanged.AddListener(HandleSortChanged);
            BuildSortOptions();
        }
    }

    private void OnDestroy()
    {
        UnbindButton(closeButton, Close);
        UnbindButton(placeButton, PlaceSelectedItem);
        UnbindButton(previousPageButton, PreviousPage);
        UnbindButton(nextPageButton, NextPage);

        if (searchInput != null)
            searchInput.onValueChanged.RemoveListener(HandleFilterChanged);

        if (sortDropdown != null)
            sortDropdown.onValueChanged.RemoveListener(HandleSortChanged);
    }

    public void Open(
        int zeroBasedSlotIndex,
        TrophyRoomService trophyService,
        TrophyRoomPanelUI panel)
    {
        service = trophyService;
        owner = panel;
        slotIndex = zeroBasedSlotIndex;
        selectedItem = null;
        currentPage = 0;

        if (root == null)
            root = gameObject;

        root.SetActive(true);

        if (titleText != null)
            titleText.text = $"SELECT TROPHY FOR PEDESTAL {slotIndex + 1}";

        SetResult("");
        RebuildFilteredItems();
        RefreshPreview();
    }

    public void Close()
    {
        if (root != null)
            root.SetActive(false);
    }

    public void SelectItem(InventoryItem item)
    {
        selectedItem = item;
        SetResult("");
        RefreshPage();
        RefreshPreview();
    }

    private void RebuildFilteredItems()
    {
        filteredItems.Clear();

        if (service == null)
        {
            RefreshPage();
            return;
        }

        List<InventoryItem> source = service.GetSelectionItems(SortMode);
        string query = searchInput != null && searchInput.text != null
            ? searchInput.text.Trim()
            : "";

        for (int i = 0; i < source.Count; i++)
        {
            InventoryItem item = source[i];

            if (item == null || item.skin == null)
                continue;

            if (!string.IsNullOrWhiteSpace(query) &&
                !MatchesSearch(item, query))
            {
                continue;
            }

            filteredItems.Add(item);
        }

        int maximumPage = GetMaximumPage();
        currentPage = Mathf.Clamp(currentPage, 0, maximumPage);
        RefreshPage();
    }

    private void RefreshPage()
    {
        EnsureCardPool();

        int safePageSize = Mathf.Max(10, itemsPerPage);
        int start = currentPage * safePageSize;
        int visibleCount = Mathf.Clamp(
            filteredItems.Count - start,
            0,
            safePageSize);

        for (int i = 0; i < cardPool.Count; i++)
        {
            TrophyInventoryItemCardUI card = cardPool[i];
            bool visible = i < visibleCount;
            card.gameObject.SetActive(visible);

            if (!visible)
                continue;

            InventoryItem item = filteredItems[start + i];
            TrophyPowerBreakdown power = service != null
                ? service.EvaluateItem(item, slotIndex)
                : null;

            card.Bind(
                item,
                power,
                this,
                ReferenceEquals(item, selectedItem));
        }

        int pageCount = filteredItems.Count == 0
            ? 1
            : Mathf.CeilToInt(filteredItems.Count / (float)safePageSize);

        if (pageText != null)
        {
            pageText.text =
                $"Page {currentPage + 1} / {pageCount}  •  " +
                $"{filteredItems.Count:N0} eligible items";
        }

        if (previousPageButton != null)
            previousPageButton.interactable = currentPage > 0;

        if (nextPageButton != null)
            nextPageButton.interactable = currentPage < pageCount - 1;

        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(filteredItems.Count == 0);
            emptyText.text = filteredItems.Count == 0
                ? "No inventory items match the current filters."
                : "";
        }
    }

    private void RefreshPreview()
    {
        bool hasSelection = selectedItem != null &&
                            selectedItem.skin != null &&
                            service != null;

        if (selectedIcon != null)
        {
            selectedIcon.sprite = hasSelection
                ? selectedItem.skin.icon
                : null;
            selectedIcon.enabled = selectedIcon.sprite != null;
        }

        if (selectedNameText != null)
        {
            selectedNameText.text = hasSelection
                ? SkinDisplayUtility.GetDisplayName(selectedItem.skin)
                : "Select an inventory item";
        }

        if (!hasSelection)
        {
            if (selectedDetailsText != null)
                selectedDetailsText.text = "";

            if (powerBreakdownText != null)
                powerBreakdownText.text = "";

            if (projectedBonusText != null)
                projectedBonusText.text = "";

            SetPlaceButton(false, "SELECT AN ITEM");
            return;
        }

        TrophyPowerBreakdown power = service.EvaluateItem(
            selectedItem,
            slotIndex);
        TrophyRoomSnapshot room = service.GetSnapshot();
        TrophyRoomSlotSnapshot currentSlot = slotIndex >= 0 &&
                                             slotIndex < room.slots.Count
            ? room.slots[slotIndex]
            : null;
        int currentContribution = currentSlot != null &&
                                  currentSlot.power != null
            ? currentSlot.power.finalContribution
            : 0;
        int projectedPower = Math.Max(
            0,
            room.totalWeightedPower - currentContribution +
            power.finalContribution);

        string variant = selectedItem.statTrak
            ? "StatTrak"
            : selectedItem.souvenir ? "Souvenir" : "Normal";
        string floatText = selectedItem.isVanilla ||
                           selectedItem.floatValue < 0d
            ? "Vanilla"
            : selectedItem.floatValue.ToString("0.000000");

        if (selectedDetailsText != null)
        {
            selectedDetailsText.text =
                $"{selectedItem.skin.rarity} • {variant}\n" +
                $"Float: {floatText}\n" +
                $"Market value: {selectedItem.marketValue:N2} Gold";
        }

        if (powerBreakdownText != null)
        {
            powerBreakdownText.text =
                $"RAW TROPHY POWER: {power.rawTrophyPower:0.##}\n\n" +
                $"Rarity: {power.rarityContribution:0.##}\n" +
                $"Market value: {power.marketValueContribution:0.##}\n" +
                $"Variant: {power.variantContribution:0.##}\n" +
                $"Float: {power.floatContribution:0.##}\n\n" +
                $"Low-float prestige: {power.lowFloatPrestige * 100d:0.##}%\n" +
                $"High-float prestige: {power.highFloatPrestige * 100d:0.##}%\n" +
                $"Pedestal multiplier: x{power.pedestalMultiplier:0.##}\n" +
                $"FINAL CONTRIBUTION: {power.finalContribution:N0}";
        }

        if (projectedBonusText != null)
        {
            TrophyRoomBalanceData balance = SaveManager.Instance != null &&
                                            SaveManager.Instance.database != null
                ? SaveManager.Instance.database.trophyRoomBalance
                : null;
            double currentBonus = room.activeBonusFraction;
            double projectedBonus = balance != null
                ? balance.EvaluateFocusBonus(room.focus, projectedPower)
                : currentBonus;

            projectedBonusText.text =
                $"Current room: {room.totalWeightedPower:N0} power\n" +
                $"Projected room: {projectedPower:N0} power\n\n" +
                $"Current: " +
                TrophyRoomPanelUI.FormatFocusBonus(room.focus, currentBonus) +
                "\nProjected: " +
                TrophyRoomPanelUI.FormatFocusBonus(room.focus, projectedBonus);
        }

        SetPlaceButton(
            true,
            currentSlot != null && currentSlot.occupied
                ? "REPLACE TROPHY"
                : "PLACE ON PEDESTAL");
    }

    private void PlaceSelectedItem()
    {
        if (service == null || selectedItem == null)
            return;

        TrophyRoomOperationResult result = service.PlaceOrReplace(
            slotIndex,
            selectedItem);

        SetResult(result.message);

        if (!result.success)
        {
            RebuildFilteredItems();
            RefreshPreview();
            return;
        }

        if (owner != null)
            owner.NotifySelectionCompleted(result);

        Close();
    }

    private void PreviousPage()
    {
        currentPage = Mathf.Max(0, currentPage - 1);
        RefreshPage();
    }

    private void NextPage()
    {
        currentPage = Mathf.Min(GetMaximumPage(), currentPage + 1);
        RefreshPage();
    }

    private int GetMaximumPage()
    {
        int safePageSize = Mathf.Max(10, itemsPerPage);
        return filteredItems.Count <= 0
            ? 0
            : Mathf.Max(0, Mathf.CeilToInt(
                filteredItems.Count / (float)safePageSize) - 1);
    }

    private void EnsureCardPool()
    {
        if (content == null || itemCardPrefab == null)
            return;

        int target = Mathf.Max(10, itemsPerPage);

        while (cardPool.Count < target)
        {
            TrophyInventoryItemCardUI card = Instantiate(
                itemCardPrefab,
                content);
            card.gameObject.SetActive(false);
            cardPool.Add(card);
        }
    }

    private void HandleFilterChanged(string value)
    {
        currentPage = 0;
        RebuildFilteredItems();
    }

    private void HandleSortChanged(int value)
    {
        currentPage = 0;
        RebuildFilteredItems();
    }

    private void BuildSortOptions()
    {
        sortDropdown.ClearOptions();
        sortDropdown.AddOptions(new List<string>
        {
            "Highest Trophy Power",
            "Highest Value",
            "Highest Rarity",
            "Lowest Float",
            "Newest",
            "Weapon"
        });
    }

    private static bool MatchesSearch(
        InventoryItem item,
        string query)
    {
        if (item == null || item.skin == null)
            return false;

        return Contains(item.skin.skinName, query) ||
               Contains(item.skin.weaponName, query) ||
               Contains(item.skin.collection, query) ||
               Contains(SkinDisplayUtility.GetDisplayName(item.skin), query);
    }

    private static bool Contains(string source, string query)
    {
        return !string.IsNullOrWhiteSpace(source) &&
               source.IndexOf(
                   query,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void SetPlaceButton(bool interactable, string text)
    {
        if (placeButton != null)
            placeButton.interactable = interactable;

        if (placeButtonText != null)
            placeButtonText.text = text ?? "";
    }

    private void SetResult(string message)
    {
        if (resultText == null)
            return;

        resultText.text = message ?? "";
        resultText.gameObject.SetActive(
            !string.IsNullOrWhiteSpace(resultText.text));
    }

    private static void BindButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void UnbindButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }
}
