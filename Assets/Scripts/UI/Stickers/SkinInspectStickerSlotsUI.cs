using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Connects the four existing SkinInspect sticker buttons to the sticker picker
/// and filled-slot action menu. It also presents the live base/sticker/total
/// value breakdown requested for sticker crafts.
/// </summary>
public sealed class SkinInspectStickerSlotsUI : MonoBehaviour
{
    [Header("Inspect")]
    [SerializeField] private SkinInspectUI inspectUI;
    [SerializeField] private GameObject stickerSlotsRoot;
    [SerializeField] private TMP_Text stickerSlotsText;
    [SerializeField] private StickerSlotButtonUI[] slots =
        new StickerSlotButtonUI[StickerApplicationService.StickerSlotCount];

    [Header("Popups")]
    [SerializeField] private StickerPickerPopupUI pickerPopup;
    [SerializeField] private StickerSlotActionPopupUI actionPopup;

    [Header("Value Breakdown")]
    [SerializeField] private TMP_Text baseSkinValueText;
    [SerializeField] private TMP_Text stickerValueText;
    [SerializeField] private TMP_Text totalValueText;

    private StickerApplicationService service;
    private string displayedInstanceId;
    private float nextRefreshAt;

    public InventoryItem CurrentSkinItem =>
        inspectUI != null ? inspectUI.GetCurrentItem() : null;

    private void Awake()
    {
        ResolveReferences();

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
            {
                slots[i].slotIndex = i;
                slots[i].Bind(this);
            }
        }
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        RefreshNow();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshAt)
            return;

        nextRefreshAt = Time.unscaledTime + 0.15f;
        InventoryItem item = CurrentSkinItem;
        string id = item != null ? item.instanceId : "";

        if (!string.Equals(id, displayedInstanceId, StringComparison.Ordinal))
        {
            displayedInstanceId = id;
            RefreshNow();
        }
    }

    public void HandleSlotClicked(int slotIndex)
    {
        InventoryItem skinItem = CurrentSkinItem;

        if (!StickerApplicationService.SupportsStickers(skinItem))
            return;

        ResolveReferences();
        AppliedStickerSaveData applied = service != null
            ? service.GetAppliedSticker(skinItem, slotIndex)
            : null;

        if (applied == null)
        {
            if (pickerPopup != null)
                pickerPopup.Open(skinItem, slotIndex, this);
            else
                Debug.LogWarning("Sticker picker popup is not assigned.", this);
        }
        else
        {
            if (actionPopup != null)
                actionPopup.Open(skinItem, slotIndex, this);
            else
                Debug.LogWarning("Sticker action popup is not assigned.", this);
        }
    }

    public void RefreshNow()
    {
        ResolveReferences();
        InventoryItem skinItem = CurrentSkinItem;
        bool supported = StickerApplicationService.SupportsStickers(skinItem);

        if (stickerSlotsRoot != null)
            stickerSlotsRoot.SetActive(supported);

        if (stickerSlotsText != null)
        {
            stickerSlotsText.text = "STICKERS";
            stickerSlotsText.gameObject.SetActive(supported);
        }

        if (!supported || service == null)
        {
            ClearSlots();
            SetValueTexts(0f, 0f, 0f, false);
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            AppliedStickerSaveData applied = service.GetAppliedSticker(
                skinItem,
                i);
            slots[i].SetSticker(service.ResolveSticker(applied));
        }

        float baseValue = PriceCalculator.GetBasePriceWithoutStickers(skinItem);
        float stickerValue = service.GetAppliedStickerValue(skinItem);
        float totalValue = baseValue + stickerValue;
        skinItem.marketValue = totalValue;
        SetValueTexts(baseValue, stickerValue, totalValue, true);
    }

    public void ShowStatus(string message, bool error)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (error)
            Debug.LogWarning(message, this);
        else
            Debug.Log(message, this);
    }

    private void SetValueTexts(
        float baseValue,
        float stickerValue,
        float totalValue,
        bool visible)
    {
        if (baseSkinValueText != null)
        {
            baseSkinValueText.text = $"Base skin value: {baseValue:N2} Gold";
            baseSkinValueText.gameObject.SetActive(visible);
        }

        if (stickerValueText != null)
        {
            stickerValueText.text = $"Applied sticker value: {stickerValue:N2} Gold";
            stickerValueText.gameObject.SetActive(visible);
        }

        if (totalValueText != null)
        {
            totalValueText.text = $"Total market value: {totalValue:N2} Gold";
            totalValueText.gameObject.SetActive(visible);
        }
    }

    private void ClearSlots()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].SetSticker(null);
        }
    }

    private void ResolveReferences()
    {
        if (inspectUI == null)
            inspectUI = GetComponentInParent<SkinInspectUI>(true);

        if (stickerSlotsRoot == null && inspectUI != null)
            stickerSlotsRoot = inspectUI.stickerSlotsRoot;

        if (service == null)
            service = StickerApplicationService.GetOrCreate();
    }

    private void Subscribe()
    {
        if (service != null)
        {
            service.OnStickerStateChanged -= RefreshNow;
            service.OnStickerStateChanged += RefreshNow;
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= RefreshNow;
            InventoryManager.Instance.OnInventoryChanged += RefreshNow;
        }
    }

    private void Unsubscribe()
    {
        if (service != null)
            service.OnStickerStateChanged -= RefreshNow;

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshNow;
    }
}
