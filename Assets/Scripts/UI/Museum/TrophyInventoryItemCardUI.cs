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

    private InventoryItem item;
    private TrophySelectionPopupUI owner;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
        }
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
                string floatText = item.isVanilla || item.floatValue < 0d
                    ? "Vanilla"
                    : $"Float {item.floatValue:0.000000}";

                detailsText.text =
                    $"{item.skin.rarity} • {variant}\n" +
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

    private void HandleClicked()
    {
        if (owner != null && item != null)
            owner.SelectItem(item);
    }
}
