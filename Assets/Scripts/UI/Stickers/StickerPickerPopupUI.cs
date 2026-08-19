using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
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

    [FormerlySerializedAs("favoriteOnlyToggle")]
    [Tooltip(
        "When enabled, favorited stickers remain visible at the end of the " +
        "picker but cannot be selected. Disable it to hide favorites entirely.")]
    [SerializeField] private Toggle showFavoritesToggle;

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
    private bool isOpen;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        EnsureButtonWiring();

        if (searchInput != null)
        {
            searchInput.onValueChanged.RemoveAllListeners();
            searchInput.onValueChanged.AddListener(_ => Rebuild());
        }

        SetupDropdown(rarityDropdown);
        SetupDropdown(capsuleDropdown);
        SetupDropdown(yearDropdown);
        SetupDropdown(sortDropdown);

        if (showFavoritesToggle != null)
        {
            showFavoritesToggle.onValueChanged.RemoveAllListeners();
            showFavoritesToggle.onValueChanged.AddListener(_ => Rebuild());
            UpdateShowFavoritesLabel();
        }

        PopulateStaticDropdowns();
        SetOpenState(false);
    }

    private void OnEnable()
    {
        EnsureButtonWiring();
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
        isOpen = true;

        EnsureButtonWiring();

        if (root != null && !root.activeSelf)
            root.SetActive(true);

        if (titleText != null)
            titleText.text = $"SELECT STICKER — SLOT {slotIndex + 1}";

        PopulateDynamicDropdowns();
        Rebuild();
        RefreshSelectedPreview();
    }

    public void Close()
    {
        SetOpenState(false);
    }

    private void SetOpenState(bool open)
    {
        isOpen = open;

        if (!open)
        {
            ClearCards();
            skinItem = null;
            selectedStickerItem = null;
            selectedCard = null;
            owner = null;
        }

        if (root != null && root.activeSelf != open)
            root.SetActive(open);
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
        EnsureButtonWiring();
        ResolveLiveSelection();

        StickerData sticker = StickerItemUtility.GetSticker(selectedStickerItem);

        if (service == null)
        {
            ShowResult(StickerActionResult.Failed(
                StickerActionStatus.ServiceUnavailable,
                "Sticker application service is unavailable."));
            return;
        }

        if (skinItem == null)
        {
            ShowResult(StickerActionResult.Failed(
                StickerActionStatus.Invalid,
                "The inspected weapon skin could not be resolved."));
            return;
        }

        if (selectedStickerItem == null || sticker == null)
        {
            ShowResult(StickerActionResult.Failed(
                StickerActionStatus.Invalid,
                "Select an owned sticker before applying it."));
            return;
        }

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

            SellConfirmationPopupUI confirmation =
                SellConfirmationPopupUI.Instance;

            if (confirmation == null)
            {
                ShowResult(StickerActionResult.Failed(
                    StickerActionStatus.ServiceUnavailable,
                    "Expensive stickers require the confirmation popup, but it is unavailable."));
                return;
            }

            confirmation.Show(
                "Apply Expensive Sticker",
                message,
                "Apply",
                "Cancel",
                ConfirmApply,
                HandleApplyCancelled);
            return;
        }

        ConfirmApply();
    }

    private void ConfirmApply()
    {
        ResolveLiveSelection();

        if (service == null)
            service = StickerApplicationService.GetOrCreate();

        if (service == null || skinItem == null || selectedStickerItem == null)
        {
            ShowResult(StickerActionResult.Failed(
                StickerActionStatus.ServiceUnavailable,
                "Sticker application could not resolve the selected skin or sticker."));
            return;
        }

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

    private void HandleApplyCancelled()
    {
        if (root != null && isOpen && !root.activeSelf)
            root.SetActive(true);

        RefreshSelectedPreview();
    }

    private void ResolveLiveSelection()
    {
        if (service == null)
            service = StickerApplicationService.GetOrCreate();

        if (InventoryManager.Instance == null)
            return;

        if (skinItem == null && owner != null)
            skinItem = owner.CurrentSkinItem;

        if (skinItem != null && !string.IsNullOrWhiteSpace(skinItem.instanceId))
        {
            InventoryItem liveSkin = InventoryManager.Instance.GetItemByInstanceId(
                skinItem.instanceId);

            if (liveSkin != null)
                skinItem = liveSkin;
        }

        if (selectedStickerItem != null &&
            !string.IsNullOrWhiteSpace(selectedStickerItem.instanceId))
        {
            InventoryItem liveSticker = InventoryManager.Instance.GetItemByInstanceId(
                selectedStickerItem.instanceId);

            if (liveSticker != null)
                selectedStickerItem = liveSticker;
        }
    }

    private void Rebuild()
    {
        if (!isOpen)
            return;

        ClearCards();
        filtered.Clear();
        quantityByStickerId.Clear();

        if (skinItem == null && owner != null)
            skinItem = owner.CurrentSkinItem;

        if (InventoryManager.Instance == null)
        {
            SetEmpty("Sticker inventory is unavailable.");
            return;
        }

        if (content == null || cardPrefab == null)
        {
            SetEmpty("Sticker picker UI is not fully assigned.");
            return;
        }

        if (skinItem == null)
        {
            SetEmpty("No weapon skin is currently selected.");
            return;
        }

        IReadOnlyList<InventoryItem> items = InventoryManager.Instance.Items;

        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem item = items[i];
            StickerData sticker = StickerItemUtility.GetSticker(item);

            if (sticker == null)
                continue;

            string key = GetStickerQuantityKey(sticker);

            if (!quantityByStickerId.ContainsKey(key))
                quantityByStickerId.Add(key, 0);

            quantityByStickerId[key]++;
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
                               GetStickerQuantityKey(sticker),
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
        if (showFavoritesToggle != null &&
            !showFavoritesToggle.isOn &&
            item.favorite)
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
            int optionIndex = capsuleDropdown.value - 1;

            if (optionIndex < 0 || optionIndex >= capsuleOptions.Count)
                return false;

            string capsule = capsuleOptions[optionIndex];
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
            int optionIndex = yearDropdown.value - 1;

            if (optionIndex < 0 || optionIndex >= yearOptions.Count)
                return false;

            if (sticker.year != yearOptions[optionIndex])
                return false;
        }

        return true;
    }

    private int CompareItems(InventoryItem a, InventoryItem b)
    {
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
               quantityByStickerId.TryGetValue(
                   GetStickerQuantityKey(sticker),
                   out int quantity)
            ? quantity
            : 0;
    }

    private static string GetStickerQuantityKey(StickerData sticker)
    {
        if (sticker == null)
            return "";

        return !string.IsNullOrWhiteSpace(sticker.apiId)
            ? sticker.apiId
            : sticker.DisplayName;
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
            capsuleDropdown.SetValueWithoutNotify(0);
            capsuleDropdown.RefreshShownValue();
        }

        if (yearDropdown != null)
        {
            yearDropdown.ClearOptions();
            List<string> options = new List<string> { "All years" };

            for (int i = 0; i < yearOptions.Count; i++)
                options.Add(yearOptions[i].ToString());

            yearDropdown.AddOptions(options);
            yearDropdown.SetValueWithoutNotify(0);
            yearDropdown.RefreshShownValue();
        }
    }

    private void UpdateShowFavoritesLabel()
    {
        if (showFavoritesToggle == null)
            return;

        TMP_Text[] labels = showFavoritesToggle.GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];

            if (label == null)
                continue;

            string current = (label.text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(current) ||
                string.Equals(current, "Toggle", StringComparison.OrdinalIgnoreCase) ||
                current.IndexOf("favorite", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                label.text = "Show favorites";
                break;
            }
        }
    }

    private void SetupDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown == null)
            return;

        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener(_ => Rebuild());
    }

    private void EnsureButtonWiring()
    {
        SetupButton(closeButton, Close);
        SetupButton(applyButton, ApplySelected);
    }

    private static void SetupButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
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

        if (selectedRuleText != null && !result.success)
            selectedRuleText.text = result.message;

        if (owner != null)
            owner.ShowStatus(result.message, !result.success);
        else if (!result.success)
            Debug.LogWarning(result.message, this);
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
