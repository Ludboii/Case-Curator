using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AutoAcquisitionIntakeItemCardUI : MonoBehaviour
{
    [SerializeField] private Image rarityBackground;
    [SerializeField] private Image itemImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text detailsText;
    [SerializeField] private TMP_Text sourceText;
    [SerializeField] private TMP_Text alertText;
    [SerializeField] private Button claimButton;
    [SerializeField] private TMP_Text claimButtonText;

    private AutoAcquisitionPendingItemSaveData pending;
    private AutomatedAcquisitionsPanelUI owner;
    private AutoAcquisitionService service;

    public void Setup(
        AutoAcquisitionPendingItemSaveData data,
        AutomatedAcquisitionsPanelUI panel,
        AutoAcquisitionService acquisitionService)
    {
        pending = data;
        owner = panel;
        service = acquisitionService;

        InventoryItem item = pending != null && service != null
            ? AutoAcquisitionItemSerializationUtility.ToRuntimeItem(
                pending.item,
                service.Database)
            : null;
        SkinData skin = item != null ? item.skin : null;

        if (rarityBackground != null)
        {
            rarityBackground.color = skin != null
                ? RarityColorUtility.GetColor(skin.rarity)
                : Color.gray;
        }

        if (itemImage != null)
        {
            itemImage.sprite = skin != null ? skin.icon : null;
            itemImage.enabled = itemImage.sprite != null;
            itemImage.preserveAspect = true;
        }

        if (titleText != null)
        {
            titleText.text = skin != null
                ? SkinDisplayUtility.GetDisplayName(skin)
                : "Unknown Intake Item";
        }

        if (detailsText != null)
        {
            detailsText.text = item != null
                ? BuildDetails(item)
                : "Saved item data is unavailable.";
        }

        if (sourceText != null)
        {
            sourceText.text = pending != null
                ? $"Line {pending.lineIndex + 1} • {pending.sourceContainerName}"
                : "";
        }

        if (alertText != null)
        {
            bool show = pending != null && pending.exceptional;
            alertText.gameObject.SetActive(show);
            alertText.text = show
                ? "CURATOR FLAG — " + pending.alertReason
                : "";
        }

        if (claimButtonText != null)
            claimButtonText.text = "CLAIM";

        if (claimButton != null)
        {
            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(HandleClaim);
            claimButton.interactable = pending != null && item != null;
        }
    }

    private void HandleClaim()
    {
        if (service == null || pending == null)
            return;

        AutoAcquisitionActionResult result = service.ClaimItem(pending.rewardId);

        if (owner != null)
            owner.HandleActionResult(result);
    }

    private static string BuildDetails(InventoryItem item)
    {
        string variant = item.souvenir
            ? "Souvenir"
            : item.statTrak ? "StatTrak" : "Normal";
        string floatText = item.isVanilla
            ? "Vanilla"
            : item.floatValue.ToString("0.000000");
        string pattern = item.patternTier != PatternTier.None
            ? $" • {item.patternTier}"
            : "";

        return $"{variant} • Float {floatText}{pattern}\n" +
               $"Value: {item.marketValue:N2} Gold";
    }
}
