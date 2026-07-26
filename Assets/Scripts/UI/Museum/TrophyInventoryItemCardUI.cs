using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TrophyInventoryItemCardUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text detailsText;
    [SerializeField] private TMP_Text powerText;
    [SerializeField] private Button button;
    [SerializeField] private GameObject selectedIndicator;

    private const string NormalVariantColor = "#FFFFFF";
    private const string StatTrakVariantColor = "#FF8C24";
    private const string SouvenirVariantColor = "#FFD24A";

    private InventoryItem item;
    private TrophySelectionPopupUI owner;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
        }

        if (detailsText != null)
            detailsText.richText = true;
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }

    public void Bind(
        InventoryItem inventoryItem,
        TrophyPowerBreakdown power,
        TrophySelectionPopupUI popup,
        bool selected)
    {
        item = inventoryItem;
        owner = popup;

        if (icon != null)
        {
            icon.sprite = item != null && item.skin != null
                ? item.skin.icon
                : null;
            icon.enabled = icon.sprite != null;
        }

        if (nameText != null)
        {
            nameText.text = item != null && item.skin != null
                ? SkinDisplayUtility.GetDisplayName(item.skin)
                : "Unknown item";
        }

        if (detailsText != null)
        {
            if (item == null || item.skin == null)
            {
                detailsText.text = "";
            }
            else
            {
                string variant = item.statTrak
                    ? "StatTrak"
                    : item.souvenir ? "Souvenir" : "Normal";

                string variantColor = item.statTrak
                    ? StatTrakVariantColor
                    : item.souvenir
                        ? SouvenirVariantColor
                        : NormalVariantColor;

                string rarityColor = GetRarityColor(item.skin.rarity);
                string floatText = item.isVanilla || item.floatValue < 0d
                    ? "Vanilla"
                    : $"Float {item.floatValue:0.000000}";

                detailsText.text =
                    $"<color={rarityColor}>{item.skin.rarity}</color> • " +
                    $"<color={variantColor}>{variant}</color>\n" +
                    $"{floatText} • {item.marketValue:N2} Gold";
            }
        }

        if (powerText != null)
        {
            powerText.text = power != null
                ? $"{power.finalContribution:N0} POWER"
                : "0 POWER";
        }

        if (selectedIndicator != null)
            selectedIndicator.SetActive(selected);

        if (button != null)
            button.interactable = item != null && item.skin != null;
    }

    private static string GetRarityColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Consumer:
                return "#B0C3D9";
            case Rarity.Industrial:
                return "#5E98D9";
            case Rarity.MilSpec:
                return "#4B69FF";
            case Rarity.Restricted:
                return "#8847FF";
            case Rarity.Classified:
                return "#D32CE6";
            case Rarity.Covert:
                return "#EB4B4B";
            case Rarity.RareSpecial:
                return "#E4AE39";
            default:
                return "#FFFFFF";
        }
    }

    private void HandleClicked()
    {
        if (owner != null && item != null)
            owner.SelectItem(item);
    }
}
