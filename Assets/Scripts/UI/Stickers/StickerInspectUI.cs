using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StickerInspectUI : MonoBehaviour
{
    public static StickerInspectUI Instance { get; private set; }

    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image rarityBackground;
    [SerializeField] private Image stickerImage;

    [Header("Text")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private TMP_Text capsuleText;
    [SerializeField] private TMP_Text tournamentText;
    [SerializeField] private TMP_Text teamPlayerText;
    [SerializeField] private TMP_Text yearText;
    [SerializeField] private TMP_Text appliedStatusText;

    [Header("Buttons")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button favoriteButton;
    [SerializeField] private TMP_Text favoriteButtonText;
    [SerializeField] private Button sellButton;
    [SerializeField] private TMP_Text sellButtonText;

    private InventoryItem standaloneItem;
    private StickerData sticker;
    private bool appliedMode;
    private bool cataloguePreviewMode;

    private void Awake()
    {
        Instance = this;

        if (root == null)
            root = gameObject;

        SetupButton(closeButton, Close);
        SetupButton(favoriteButton, ToggleFavorite);
        SetupButton(sellButton, RequestSell);
        Close();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void OpenOwnedItem(InventoryItem item)
    {
        StickerData data = StickerItemUtility.GetSticker(item);

        if (data == null)
            return;

        standaloneItem = item;
        sticker = data;
        appliedMode = false;
        cataloguePreviewMode = false;
        OpenAndRefresh();
    }

    /// <summary>
    /// Opens a read-only sticker preview from a Sticker Capsule's possible-drop
    /// list. It does not imply ownership and exposes no sell/favourite actions.
    /// </summary>
    public void OpenCatalogueSticker(
        StickerData data,
        CaseData sourceCapsule = null)
    {
        if (data == null)
            return;

        standaloneItem = null;
        sticker = data;
        appliedMode = false;
        cataloguePreviewMode = true;
        OpenAndRefresh();

        if (appliedStatusText != null)
        {
            string source = sourceCapsule != null &&
                            !string.IsNullOrWhiteSpace(sourceCapsule.caseName)
                ? sourceCapsule.caseName
                : data.PrimaryCapsuleName;
            appliedStatusText.text = string.IsNullOrWhiteSpace(source)
                ? "POSSIBLE STICKER DROP"
                : $"POSSIBLE DROP FROM {source.ToUpperInvariant()}";
            appliedStatusText.gameObject.SetActive(true);
        }
    }

    public void OpenApplied(
        StickerData data,
        AppliedStickerSaveData applied,
        InventoryItem skinItem)
    {
        if (data == null)
            return;

        standaloneItem = null;
        sticker = data;
        appliedMode = true;
        cataloguePreviewMode = false;
        OpenAndRefresh();

        if (appliedStatusText != null)
        {
            string skinName = skinItem != null && skinItem.skin != null
                ? SkinDisplayUtility.GetDisplayName(skinItem.skin)
                : "weapon skin";
            appliedStatusText.text =
                $"Applied to {skinName} • Slot " +
                $"{(applied != null ? applied.slotIndex + 1 : 0)}";
            appliedStatusText.gameObject.SetActive(true);
        }
    }

    public void Close()
    {
        standaloneItem = null;
        sticker = null;
        appliedMode = false;
        cataloguePreviewMode = false;

        if (root != null)
            root.SetActive(false);
    }

    private void OpenAndRefresh()
    {
        if (root != null)
            root.SetActive(true);

        Refresh();
    }

    private void Refresh()
    {
        if (sticker == null)
            return;

        if (rarityBackground != null)
        {
            Color rarity = StickerRarityUtility.GetColor(sticker.stickerRarity);
            Color background = Color.Lerp(Color.black, rarity, 0.55f);
            background.a = 1f;
            rarityBackground.color = background;
        }

        if (stickerImage != null)
        {
            stickerImage.sprite = sticker.icon;
            stickerImage.enabled = sticker.icon != null;
            stickerImage.preserveAspect = true;
        }

        if (nameText != null)
            nameText.text = sticker.DisplayName;
        if (rarityText != null)
            rarityText.text = StickerRarityUtility.GetDisplayName(
                sticker.stickerRarity);
        if (valueText != null)
        {
            valueText.text =
                $"Market value: {sticker.marketValue:N2} Gold\n" +
                $"Applied value: " +
                $"{sticker.marketValue * StickerApplicationService.AppliedValuePercent:N2} Gold (20%)";
        }

        SetOptionalText(capsuleText, "Capsule", sticker.PrimaryCapsuleName);
        SetOptionalText(
            tournamentText,
            "Tournament / Event",
            sticker.tournamentEvent);

        string teamPlayer = "";

        if (!string.IsNullOrWhiteSpace(sticker.teamName))
            teamPlayer = sticker.teamName;
        if (!string.IsNullOrWhiteSpace(sticker.playerName))
        {
            teamPlayer += string.IsNullOrWhiteSpace(teamPlayer)
                ? sticker.playerName
                : " • " + sticker.playerName;
        }

        SetOptionalText(teamPlayerText, "Team / Player", teamPlayer);
        SetOptionalText(
            yearText,
            "Year",
            sticker.year > 0 ? sticker.year.ToString() : "");

        if (appliedStatusText != null &&
            !appliedMode &&
            !cataloguePreviewMode)
        {
            appliedStatusText.gameObject.SetActive(false);
        }

        bool ownsStandalone =
            !appliedMode &&
            !cataloguePreviewMode &&
            standaloneItem != null &&
            InventoryManager.Instance != null &&
            InventoryManager.Instance.GetItemByInstanceId(
                standaloneItem.instanceId) == standaloneItem;
        bool showOwnershipActions = !appliedMode && !cataloguePreviewMode;

        if (favoriteButton != null)
        {
            favoriteButton.gameObject.SetActive(showOwnershipActions);
            favoriteButton.interactable = ownsStandalone;
        }

        if (favoriteButtonText != null)
        {
            favoriteButtonText.text = standaloneItem != null &&
                                      standaloneItem.favorite
                ? "FAVORITED"
                : "FAVORITE";
        }

        if (sellButton != null)
        {
            sellButton.gameObject.SetActive(showOwnershipActions);
            sellButton.interactable = ownsStandalone &&
                                      standaloneItem != null &&
                                      !standaloneItem.favorite;
        }

        if (sellButtonText != null)
        {
            sellButtonText.text = standaloneItem != null &&
                                  standaloneItem.favorite
                ? "FAVORITED"
                : $"SELL\n{sticker.marketValue:N2} GOLD";
        }
    }

    private void ToggleFavorite()
    {
        if (standaloneItem == null || appliedMode || cataloguePreviewMode ||
            InventoryManager.Instance == null)
        {
            return;
        }

        InventoryManager.Instance.ToggleFavorite(standaloneItem);
        Refresh();
    }

    private void RequestSell()
    {
        if (standaloneItem == null || sticker == null ||
            appliedMode || cataloguePreviewMode)
        {
            return;
        }

        if (standaloneItem.favorite)
        {
            Debug.LogWarning(
                "Favorited stickers cannot be sold. Unfavorite it first.",
                this);
            return;
        }

        string message =
            $"Sell {sticker.DisplayName} for " +
            $"{sticker.marketValue:N2} Gold?";

        if (SellConfirmationPopupUI.Instance != null)
        {
            SellConfirmationPopupUI.Instance.Show(
                "Sell Sticker",
                message,
                "Sell",
                "Cancel",
                ConfirmSell);
            return;
        }

        ConfirmSell();
    }

    private void ConfirmSell()
    {
        if (standaloneItem == null || sticker == null ||
            standaloneItem.favorite ||
            InventoryManager.Instance == null ||
            SaveManager.Instance == null)
        {
            return;
        }

        float value = Mathf.Max(0f, sticker.marketValue);

        if (!InventoryManager.Instance.RemoveItem(standaloneItem))
            return;

        SaveManager.Instance.AddGold(value);
        Close();
    }

    private static void SetOptionalText(
        TMP_Text target,
        string label,
        string value)
    {
        if (target == null)
            return;

        bool show = !string.IsNullOrWhiteSpace(value);
        target.text = show ? $"{label}: {value}" : "";
        target.gameObject.SetActive(show);
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
