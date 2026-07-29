using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StickerPickerItemCardUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image rarityBar;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text sourceText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private TMP_Text addedValueText;
    [SerializeField] private GameObject favoriteRoot;
    [SerializeField] private TMP_Text favoriteText;
    [SerializeField] private GameObject selectedRoot;

    private StickerPickerPopupUI owner;
    private InventoryItem item;

    public InventoryItem Item => item;

    public void Setup(
        InventoryItem stickerItem,
        StickerPickerPopupUI popup,
        float estimatedAddedValue,
        int ownedQuantity)
    {
        item = stickerItem;
        owner = popup;
        StickerData sticker = StickerItemUtility.GetSticker(item);

        if (sticker == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (iconImage != null)
        {
            iconImage.sprite = sticker.icon;
            iconImage.enabled = sticker.icon != null;
            iconImage.preserveAspect = true;
        }

        if (rarityBar != null)
            rarityBar.color = StickerRarityUtility.GetColor(sticker.stickerRarity);

        if (nameText != null)
            nameText.text = sticker.DisplayName;
        if (rarityText != null)
            rarityText.text = StickerRarityUtility.GetDisplayName(
                sticker.stickerRarity);

        if (sourceText != null)
        {
            string source = sticker.PrimaryCapsuleName;

            if (sticker.year > 0)
                source += string.IsNullOrWhiteSpace(source)
                    ? sticker.year.ToString()
                    : $" • {sticker.year}";

            if (ownedQuantity > 1)
                source += string.IsNullOrWhiteSpace(source)
                    ? $"Owned: {ownedQuantity}"
                    : $" • Owned: {ownedQuantity}";

            sourceText.text = source;
            sourceText.gameObject.SetActive(!string.IsNullOrWhiteSpace(source));
        }

        if (valueText != null)
            valueText.text = $"{sticker.marketValue:N2} Gold";

        if (addedValueText != null)
        {
            addedValueText.text =
                $"Adds {estimatedAddedValue:N2} Gold (20%)";
        }

        bool favorite = item.favorite;

        if (favoriteRoot != null)
            favoriteRoot.SetActive(favorite);
        if (favoriteText != null)
        {
            favoriteText.text = favorite ? "FAVORITED — UNAVAILABLE" : "";
            favoriteText.gameObject.SetActive(favorite);
        }

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
            button.interactable = !favorite;
        }

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (selectedRoot != null)
            selectedRoot.SetActive(selected);
    }

    private void HandleClicked()
    {
        if (owner != null && item != null && !item.favorite)
            owner.Select(item, this);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }
}
