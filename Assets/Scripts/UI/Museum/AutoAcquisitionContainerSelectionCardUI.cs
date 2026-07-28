using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AutoAcquisitionContainerSelectionCardUI : MonoBehaviour
{
    [SerializeField] private Button selectButton;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private TMP_Text skinPreviewText;
    [SerializeField] private TMP_Text selectButtonText;

    private AutoAcquisitionContainerData entry;
    private AutoAcquisitionContainerSelectionPopupUI owner;

    public void Setup(
        AutoAcquisitionContainerData data,
        AutoAcquisitionContainerSelectionPopupUI popup)
    {
        entry = data;
        owner = popup;

        CaseData container = entry != null ? entry.container : null;

        if (titleText != null)
            titleText.text = entry != null ? entry.ContainerName : "Container";

        if (iconImage != null)
        {
            iconImage.sprite = container != null ? container.caseImage : null;
            iconImage.enabled = iconImage.sprite != null;
            iconImage.preserveAspect = true;
        }

        if (summaryText != null)
        {
            float seconds = entry != null
                ? AutoAcquisitionUpgradeUtility.GetBaseProcessingSeconds() *
                  Mathf.Max(0.01f, entry.processingDurationMultiplier)
                : 0f;

            summaryText.text = container != null
                ? $"Cost: {container.priceInGold:N0} Gold\n" +
                  $"Processing: {seconds:N0}s\n" +
                  $"Rarity calibration: " +
                  $"{AutoAcquisitionUpgradeUtility.GetCalibrationMultiplier():P0}\n" +
                  $"Float calibration: " +
                  $"{AutoAcquisitionUpgradeUtility.GetFloatCalibrationExponent():0.00}"
                : "Container data unavailable.";
        }

        if (skinPreviewText != null)
            skinPreviewText.text = BuildSkinPreview(container);

        if (selectButtonText != null)
            selectButtonText.text = "SELECT CONTAINER";

        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(HandleSelect);
            selectButton.onClick.AddListener(HandleSelect);
            selectButton.interactable = entry != null && container != null;
        }
    }

    private static string BuildSkinPreview(CaseData container)
    {
        if (container == null)
            return "";

        var rows = AutoAcquisitionPreviewUtility.Build(container);
        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < rows.Count; i++)
        {
            AutoAcquisitionSkinPreview row = rows[i];

            if (row == null || row.skin == null)
                continue;

            if (builder.Length > 0)
                builder.Append('\n');

            builder.Append(SkinDisplayUtility.GetDisplayName(row.skin));
            builder.Append(" — ");
            builder.Append(row.automatedChancePercent.ToString("0.####"));
            builder.Append('%');

            if (row.expectedAutomatedFloat >= 0d)
            {
                builder.Append(" | Avg float ");
                builder.Append(row.expectedAutomatedFloat.ToString("0.0000"));
            }

            if (row.souvenir)
                builder.Append(" | Souvenir");
            else if (row.canBeStatTrak)
                builder.Append(" | StatTrak eligible");
        }

        return builder.ToString();
    }

    private void HandleSelect()
    {
        if (owner != null && entry != null)
            owner.Select(entry);
    }

    private void OnDestroy()
    {
        if (selectButton != null)
            selectButton.onClick.RemoveListener(HandleSelect);
    }
}
