using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum StickerPickerSortMode
{
    NameAZ,
    NameZA,
    RarityHighToLow,
    ValueHighToLow,
    ValueLowToHigh,
    QuantityHighToLow,
    QuantityLowToHigh,
    Newest,
    Oldest
}

public sealed class StickerPickerPopupUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button closeButton;

    [Header("Filters")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private TMP_Dropdown rarityDropdown;
    [SerializeField] private TMP_Dropdown capsuleDropdown;
    [SerializeField] private TMP_Dropdown yearDropdown;
    [SerializeField] private Toggle favoriteOnlyToggle;
    [SerializeField] private TMP_Dropdown sortDropdown;

    [Header("Items")]
    [SerializeField] private Transform content;
    [SerializeField] private StickerPickerItemCardUI cardPrefab;
    [SerializeField] private TMP_Text emptyText;

    [Header("Selected Preview")]
    [SerializeField] private GameObject selectedRoot;
    [SerializeField] private Image selectedIcon;
    [SerializeField] private TMP_Text selectedNameText;
    [SerializeField] private TMP_Text selectedValueText;
    [SerializeField] private TMP_Text selectedAddedValueText;
    [SerializeField] private TMP_Text selectedRuleText;
    [SerializeField] private Button applyButton;
    [SerializeField] private TMP_Text applyButtonText;

    private readonly List<StickerPickerItemCardUI> spawned =
        new List<StickerPickerItemCardUI>();
    private readonly List<InventoryItem> filtered =
        new List<InventoryItem>();
    private readonly Dictionary<string, int> quantityByStickerId =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly List<string> capsuleOptions = new List<string>();
    private readonly List<int> yearOptions = new List<int>();

    private InventoryItem skinItem;
    private InventoryItem selectedStickerItem;
    private StickerPickerItemCardUI selectedCard;
    private SkinInspectStickerSlotsUI owner;
    private StickerApplicationService service;
    private int slotIndex;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        SetupButton(closeButton, Close);
        SetupButton(applyButton, ApplySelected);

        if (searchInput != null)
        {
            searchInput.onValueChanged.RemoveAllListeners();
            searchInput.onValueChanged.AddListener(_ => Rebuild());
        }

        SetupDropdown(rarityDropdown);
        SetupDropdown(capsuleDropdown);
        SetupDropdown(yearDropdown);
        SetupDropdown(sortDropdown);

        if (favoriteOnlyToggle != null)
        {
            favoriteOnlyToggle.onValueChanged.RemoveAllListeners();
            favoriteOnlyToggle.onValueChanged.AddListener(_ => Rebuild());
        }

        PopulateStaticDropdowns();
        Close();
    }

    public void Open(
        InventoryItem targetSkin,
        int targetSlotIndex,
        SkinInspectStickerSlotsUI targetOwner)
    {
        skinItem = targetSkin;
        slotIndex = Mathf.Clamp(
            targetSlotIndex,
            0,
            StickerApplicationService.StickerSlotCount - 1);
        owner = targetOwner;
        service = StickerApplicationService.GetOrCreate();
        selectedStickerItem = null;
        selectedCard = null;

        if (root != null)
            root.SetActive(true);

        if (titleText != null)
            titleText.text = $"SELECT STICKER — SLOT {slotIndex + 1}";

        PopulateDynamicDropdowns();
        Rebuild();
        RefreshSelectedPreview();
    }

    public void Close()
    {
        ClearCards();
        skinItem = null;
        selectedStickerItem = null;
        selectedCard = null;

        if (root != null)
            root.SetActive(false);
    }

    public void Select(
        InventoryItem stickerItem,
        StickerPickerItemCardUI card)
    {
        if (stickerItem == null || stickerItem.favorite)
            return;

        if (selectedCard != null)
            selectedCard.SetSelected(false);

        selectedStickerItem = stickerItem;
        selectedCard = card;

        if (selectedCard != null)
            selectedCard.SetSelected(true);

        RefreshSelectedPreview();
    }

    private void ApplySelected()
    {
        StickerData sticker = StickerItemUtility.GetSticker(selectedStickerItem);

        if (service == null || skinItem == null || sticker == null)
            return;

        if (selectedStickerItem.favorite)
        {
            ShowResult(StickerActionResult.Failed(
                StickerActionStatus.Favorited,
                "Favorited stickers cannot be applied."));
            return;
        }

        if (sticker.marketValue >=
            StickerApplicationService.ExpensiveConfirmationThreshold)
        {
            float addedValue = CalculateEstimatedAddedValue(sticker);
            string message =
                $"Apply {sticker.DisplayName}?\n\n" +
                $"Sticker market value: {sticker.marketValue:N2} Gold\n" +
                $"Estimated value added: {addedValue:N2} Gold\n\n" +
                "This individual sticker will leave your inventory.";

            if (SellConfirmationPopupUI.Instance != null)
            {
                SellConfirmationPopupUI.Instance.Show(
                    "Apply Expensive Sticker",
                    message,
                    "Apply",
                    "Cancel",
                    ConfirmApply);
                return;
            }
        }

        ConfirmApply();
    }

    private void ConfirmApply()
    {
        if (service == null || skinItem == null || selectedStickerItem == null)
            return;

        StickerActionResult result = service.ApplySticker(
            skinItem,
            selectedStickerItem,
            slotIndex);
        ShowResult(result);

        if (!result.success)
            return;

        if (owner != null)
            owner.RefreshNow();

        Close();
    }

    private void Rebuild()
    {
        ClearCards();
        filtered.Clear();
        quantityByStickerId.Clear();

        if (InventoryManager.Instance == null ||
            content == null ||
            cardPrefab == null ||
            skinItem == null)
        {
            SetEmpty("Sticker inventory is unavailable.");
            return;
        }

        IReadOnlyList<InventoryItem> items = InventoryManager.Instance.Items;

        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem item = items[i];
            StickerData sticker = StickerItemUtility.GetSticker(item);

            if (sticker == null)
                continue;

            if (!quantityByStickerId.ContainsKey(sticker.apiId))
                quantityByStickerId.Add(sticker.apiId, 0);

            quantityByStickerId[sticker.apiId]++;
        }

        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem item = items[i];
            StickerData sticker = StickerItemUtility.GetSticker(item);

            if (sticker != null && PassesFilters(item, sticker))
                filtered.Add(item);
        }

        filtered.Sort(CompareItems);

        for (int i = 0; i < filtered.Count; i++)
        {
            InventoryItem item = filtered[i];
            StickerData sticker = StickerItemUtility.GetSticker(item);
            StickerPickerItemCardUI card = Instantiate(cardPrefab, content);
            int quantity = sticker != null &&
                           quantityByStickerId.TryGetValue(
                               sticker.apiId,
                               out int owned)
                ? owned
                : 1;
            card.Setup(
                item,
                this,
                sticker != null ? CalculateEstimatedAddedValue(sticker) : 0f,
                quantity);
            spawned.Add(card);
        }

        SetEmpty(filtered.Count == 0
            ? "No owned stickers match these filters."
            : "");
    }

    private bool PassesFilters(InventoryItem item, StickerData sticker)
    {
        if (favoriteOnlyToggle != null &&
            favoriteOnlyToggle.isOn &&
            !item.favorite)
        {
            return false;
        }

        string search = searchInput != null
            ? (searchInput.text ?? "").Trim()
            : "";

        if (!string.IsNullOrWhiteSpace(search))
        {
            string haystack =
                sticker.DisplayName + " " +
                sticker.PrimaryCapsuleName + " " +
                sticker.tournamentEvent + " " +
                sticker.teamName + " " +
                sticker.playerName + " " +
                sticker.year;

            if (haystack.IndexOf(
                    search,
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        if (rarityDropdown != null && rarityDropdown.value > 0)
        {
            StickerRarity rarity =
                (StickerRarity)(rarityDropdown.value - 1);

            if (sticker.stickerRarity != rarity)
                return false;
        }

        if (capsuleDropdown != null && capsuleDropdown.value > 0)
        {
            string capsule = capsuleOptions[capsuleDropdown.value - 1];
            bool match = false;

            if (sticker.capsules != null)
            {
                for (int i = 0; i < sticker.capsules.Count; i++)
                {
                    CaseData source = sticker.capsules[i];

                    if (source != null && string.Equals(
                            source.caseName,
                            capsule,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        match = true;
                        break;
                    }
                }
            }

            if (!match)
                return false;
        }

        if (yearDropdown != null && yearDropdown.value > 0)
        {
            int year = yearOptions[yearDropdown.value - 1];

            if (sticker.year != year)
                return false;
        }

        return true;
    }

    private int CompareItems(InventoryItem a, InventoryItem b)
    {
        // Favorited stickers remain visible for inspection but always sit at the
        // end and cannot be selected.
        int favoriteCompare = a.favorite.CompareTo(b.favorite);

        if (favoriteCompare != 0)
            return favoriteCompare;

        StickerData stickerA = StickerItemUtility.GetSticker(a);
        StickerData stickerB = StickerItemUtility.GetSticker(b);
        StickerPickerSortMode mode = sortDropdown != null
            ? (StickerPickerSortMode)sortDropdown.value
            : StickerPickerSortMode.NameAZ;

        switch (mode)
        {
            case StickerPickerSortMode.NameZA:
                return CompareName(stickerB, stickerA);
            case StickerPickerSortMode.RarityHighToLow:
                return CompareThenName(
                    ((int)stickerB.stickerRarity)
                        .CompareTo((int)stickerA.stickerRarity),
                    stickerA,
                    stickerB);
            case StickerPickerSortMode.ValueHighToLow:
                return CompareThenName(
                    stickerB.marketValue.CompareTo(stickerA.marketValue),
                    stickerA,
                    stickerB);
            case StickerPickerSortMode.ValueLowToHigh:
                return CompareThenName(
                    stickerA.marketValue.CompareTo(stickerB.marketValue),
                    stickerA,
                    stickerB);
            case StickerPickerSortMode.QuantityHighToLow:
                return CompareThenName(
                    GetQuantity(stickerB).CompareTo(GetQuantity(stickerA)),
                    stickerA,
                    stickerB);
            case StickerPickerSortMode.QuantityLowToHigh:
                return CompareThenName(
                    GetQuantity(stickerA).CompareTo(GetQuantity(stickerB)),
                    stickerA,
                    stickerB);
            case StickerPickerSortMode.Newest:
                return b.acquisitionSequence.CompareTo(a.acquisitionSequence);
            case StickerPickerSortMode.Oldest:
                return a.acquisitionSequence.CompareTo(b.acquisitionSequence);
            default:
                return CompareName(stickerA, stickerB);
        }
    }

    private int GetQuantity(StickerData sticker)
    {
        return sticker != null &&
               quantityByStickerId.TryGetValue(sticker.apiId, out int quantity)
            ? quantity
            : 0;
    }

    private static int CompareThenName(
        int primary,
        StickerData a,
        StickerData b)
    {
        return primary != 0 ? primary : CompareName(a, b);
    }

    private static int CompareName(StickerData a, StickerData b)
    {
        return string.Compare(
            a != null ? a.DisplayName : "",
            b != null ? b.DisplayName : "",
            StringComparison.OrdinalIgnoreCase);
    }

    private float CalculateEstimatedAddedValue(StickerData selected)
    {
        if (selected == null)
            return 0f;

        float selectedBase = Mathf.Max(0f, selected.marketValue) *
                             StickerApplicationService.AppliedValuePercent;

        if (service == null || skinItem == null)
            return selectedBase;

        IReadOnlyList<AppliedStickerSaveData> current =
            service.GetAppliedStickers(skinItem);

        if (current.Count != 3)
            return selectedBase;

        float currentContribution = 0f;

        for (int i = 0; i < current.Count; i++)
        {
            StickerData existing = service.ResolveSticker(current[i]);

            if (existing == null || !string.Equals(
                    existing.apiId,
                    selected.apiId,
                    StringComparison.Ordinal))
            {
                return selectedBase;
            }

            currentContribution += Mathf.Max(0f, existing.marketValue) *
                                   StickerApplicationService.AppliedValuePercent;
        }

        float afterBonus =
            (currentContribution + selectedBase) *
            StickerApplicationService.FourIdenticalCraftMultiplier;
        return afterBonus - currentContribution;
    }

    private void RefreshSelectedPreview()
    {
        StickerData sticker = StickerItemUtility.GetSticker(selectedStickerItem);
        bool selected = sticker != null;

        if (selectedRoot != null)
            selectedRoot.SetActive(selected);

        if (!selected)
        {
            if (applyButton != null)
                applyButton.interactable = false;
            return;
        }

        float addedValue = CalculateEstimatedAddedValue(sticker);

        if (selectedIcon != null)
        {
            selectedIcon.sprite = sticker.icon;
            selectedIcon.enabled = sticker.icon != null;
            selectedIcon.preserveAspect = true;
        }

        if (selectedNameText != null)
            selectedNameText.text = sticker.DisplayName;
        if (selectedValueText != null)
            selectedValueText.text =
                $"Sticker value: {sticker.marketValue:N2} Gold";
        if (selectedAddedValueText != null)
            selectedAddedValueText.text =
                $"Estimated added value: {addedValue:N2} Gold";
        if (selectedRuleText != null)
        {
            selectedRuleText.text =
                "Applied value: 20% of sticker price. Four identical stickers " +
                "receive a 5% total craft bonus.";
        }

        if (applyButton != null)
            applyButton.interactable = !selectedStickerItem.favorite;
        if (applyButtonText != null)
            applyButtonText.text = $"APPLY TO SLOT {slotIndex + 1}";
    }

    private void PopulateStaticDropdowns()
    {
        if (rarityDropdown != null)
        {
            rarityDropdown.ClearOptions();
            List<string> options = new List<string> { "All rarities" };

            for (int i = 0; i <= (int)StickerRarity.Contraband; i++)
            {
                options.Add(StickerRarityUtility.GetDisplayName(
                    (StickerRarity)i));
            }

            rarityDropdown.AddOptions(options);
        }

        if (sortDropdown != null)
        {
            sortDropdown.ClearOptions();
            sortDropdown.AddOptions(new List<string>
            {
                "A to Z",
                "Z to A",
                "Rarity",
                "Value high to low",
                "Value low to high",
                "Quantity high to low",
                "Quantity low to high",
                "Newest",
                "Oldest"
            });
        }
    }

    private void PopulateDynamicDropdowns()
    {
        capsuleOptions.Clear();
        yearOptions.Clear();
        HashSet<string> capsules =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<int> years = new HashSet<int>();

        if (InventoryManager.Instance != null)
        {
            IReadOnlyList<InventoryItem> items = InventoryManager.Instance.Items;

            for (int i = 0; i < items.Count; i++)
            {
                StickerData sticker = StickerItemUtility.GetSticker(items[i]);

                if (sticker == null)
                    continue;

                if (sticker.capsules != null)
                {
                    for (int j = 0; j < sticker.capsules.Count; j++)
                    {
                        CaseData capsule = sticker.capsules[j];

                        if (capsule != null &&
                            !string.IsNullOrWhiteSpace(capsule.caseName))
                        {
                            capsules.Add(capsule.caseName.Trim());
                        }
                    }
                }

                if (sticker.year > 0)
                    years.Add(sticker.year);
            }
        }

        capsuleOptions.AddRange(capsules);
        capsuleOptions.Sort(StringComparer.OrdinalIgnoreCase);
        yearOptions.AddRange(years);
        yearOptions.Sort((a, b) => b.CompareTo(a));

        if (capsuleDropdown != null)
        {
            capsuleDropdown.ClearOptions();
            List<string> options = new List<string> { "All capsules" };
            options.AddRange(capsuleOptions);
            capsuleDropdown.AddOptions(options);
            capsuleDropdown.value = 0;
        }

        if (yearDropdown != null)
        {
            yearDropdown.ClearOptions();
            List<string> options = new List<string> { "All years" };

            for (int i = 0; i < yearOptions.Count; i++)
                options.Add(yearOptions[i].ToString());

            yearDropdown.AddOptions(options);
            yearDropdown.value = 0;
        }
    }

    private static void SetupDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown == null)
            return;

        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener(_ =>
        {
            StickerPickerPopupUI popup = dropdown.GetComponentInParent<StickerPickerPopupUI>(true);

            if (popup != null)
                popup.Rebuild();
        });
    }

    private static void SetupButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void SetEmpty(string message)
    {
        if (emptyText == null)
            return;

        emptyText.text = message ?? "";
        emptyText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
    }

    private void ShowResult(StickerActionResult result)
    {
        if (result == null)
            return;

        if (owner != null)
            owner.ShowStatus(result.message, !result.success);
    }

    private void ClearCards()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
                Destroy(spawned[i].gameObject);
        }

        spawned.Clear();
    }
}
