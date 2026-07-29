using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StickerSlotActionPopupUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image stickerIcon;
    [SerializeField] private TMP_Text stickerNameText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private TMP_Text slotText;

    [Header("Actions")]
    [SerializeField] private Button inspectButton;
    [SerializeField] private Button replaceButton;
    [SerializeField] private Button removeButton;
    [SerializeField] private TMP_Text removeButtonText;
    [SerializeField] private Button moveButton;
    [SerializeField] private Button cancelButton;

    [Header("Move / Swap")]
    [SerializeField] private GameObject moveRoot;
    [SerializeField] private Button[] targetSlotButtons = new Button[4];
    [SerializeField] private TMP_Text[] targetSlotTexts = new TMP_Text[4];

    [Header("Sticker Inspect")]
    [SerializeField] private StickerInspectUI stickerInspectUI;

    private InventoryItem skinItem;
    private AppliedStickerSaveData applied;
    private StickerData sticker;
    private int slotIndex;
    private SkinInspectStickerSlotsUI owner;
    private StickerApplicationService service;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        SetupButton(inspectButton, InspectSticker);
        SetupButton(replaceButton, ExplainReplace);
        SetupButton(removeButton, RequestRemove);
        SetupButton(moveButton, ShowMoveTargets);
        SetupButton(cancelButton, Close);

        for (int i = 0; i < targetSlotButtons.Length; i++)
        {
            int captured = i;
            SetupButton(targetSlotButtons[i], () => MoveTo(captured));
        }

        Close();
    }

    public void Open(
        InventoryItem targetSkin,
        int targetSlot,
        SkinInspectStickerSlotsUI targetOwner)
    {
        skinItem = targetSkin;
        slotIndex = Mathf.Clamp(targetSlot, 0, 3);
        owner = targetOwner;
        service = StickerApplicationService.GetOrCreate();
        applied = service != null
            ? service.GetAppliedSticker(skinItem, slotIndex)
            : null;
        sticker = service != null ? service.ResolveSticker(applied) : null;

        if (applied == null || sticker == null)
        {
            Close();
            return;
        }

        if (root != null)
            root.SetActive(true);
        if (moveRoot != null)
            moveRoot.SetActive(false);

        RefreshPresentation();
    }

    public void Close()
    {
        skinItem = null;
        applied = null;
        sticker = null;

        if (moveRoot != null)
            moveRoot.SetActive(false);
        if (root != null)
            root.SetActive(false);
    }

    private void RefreshPresentation()
    {
        if (stickerIcon != null)
        {
            stickerIcon.sprite = sticker != null ? sticker.icon : null;
            stickerIcon.enabled = stickerIcon.sprite != null;
            stickerIcon.preserveAspect = true;
        }

        if (stickerNameText != null)
            stickerNameText.text = sticker != null ? sticker.DisplayName : "Sticker";
        if (valueText != null)
        {
            valueText.text = sticker != null
                ? $"Market value: {sticker.marketValue:N2} Gold\n" +
                  $"Applied contribution: " +
                  $"{sticker.marketValue * StickerApplicationService.AppliedValuePercent:N2} Gold"
                : "";
        }
        if (slotText != null)
            slotText.text = $"STICKER SLOT {slotIndex + 1}";

        float removalCost = sticker != null
            ? StickerApplicationService.GetRemovalCost(sticker.marketValue)
            : 0f;

        if (removeButtonText != null)
            removeButtonText.text = $"REMOVE\n{removalCost:N0} GOLD";

        for (int i = 0; i < targetSlotButtons.Length; i++)
        {
            if (targetSlotTexts != null && i < targetSlotTexts.Length &&
                targetSlotTexts[i] != null)
            {
                AppliedStickerSaveData target = service != null
                    ? service.GetAppliedSticker(skinItem, i)
                    : null;
                targetSlotTexts[i].text = i == slotIndex
                    ? $"SLOT {i + 1}\nCURRENT"
                    : target == null
                        ? $"MOVE TO SLOT {i + 1}"
                        : $"SWAP WITH SLOT {i + 1}";
            }

            if (targetSlotButtons[i] != null)
                targetSlotButtons[i].interactable = i != slotIndex;
        }
    }

    private void InspectSticker()
    {
        if (stickerInspectUI != null && sticker != null)
            stickerInspectUI.OpenApplied(sticker, applied, skinItem);
        else if (owner != null)
            owner.ShowStatus("Sticker Inspect UI is not assigned.", true);
    }

    private void ExplainReplace()
    {
        if (owner != null)
        {
            owner.ShowStatus(
                "Remove the current sticker first, then apply its replacement.",
                false);
        }
    }

    private void RequestRemove()
    {
        if (sticker == null || service == null)
            return;

        float cost = StickerApplicationService.GetRemovalCost(sticker.marketValue);
        string message =
            $"Remove {sticker.DisplayName} from slot {slotIndex + 1}?\n\n" +
            $"Removal cost: {cost:N0} Gold\n" +
            "The exact sticker item will return to inventory. " +
            "One free inventory slot is required.";

        if (SellConfirmationPopupUI.Instance != null)
        {
            SellConfirmationPopupUI.Instance.Show(
                "Remove Sticker",
                message,
                "Remove",
                "Cancel",
                ConfirmRemove);
            return;
        }

        ConfirmRemove();
    }

    private void ConfirmRemove()
    {
        StickerActionResult result = service != null
            ? service.RemoveSticker(skinItem, slotIndex)
            : StickerActionResult.Failed(
                StickerActionStatus.ServiceUnavailable,
                "Sticker service is unavailable.");

        if (owner != null)
        {
            owner.ShowStatus(result.message, !result.success);

            if (result.success)
                owner.RefreshNow();
        }

        if (result.success)
            Close();
    }

    private void ShowMoveTargets()
    {
        if (moveRoot != null)
            moveRoot.SetActive(true);

        RefreshPresentation();
    }

    private void MoveTo(int targetSlot)
    {
        StickerActionResult result = service != null
            ? service.MoveOrSwapSticker(skinItem, slotIndex, targetSlot)
            : StickerActionResult.Failed(
                StickerActionStatus.ServiceUnavailable,
                "Sticker service is unavailable.");

        if (owner != null)
        {
            owner.ShowStatus(result.message, !result.success);

            if (result.success)
                owner.RefreshNow();
        }

        if (result.success)
            Close();
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
}
