using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MuseumDonationItemCardUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image skinImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text detailsText;
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private TMP_Text warningsText;
    [SerializeField] private GameObject selectedRoot;
    [SerializeField] private GameObject blockedRoot;
    [SerializeField] private TMP_Text blockedText;

    private MuseumDonationCandidate candidate;
    private MuseumDonationSelectionUI owner;

    public MuseumDonationCandidate Candidate => candidate;

    public void Setup(
        MuseumDonationCandidate value,
        MuseumDonationSelectionUI selectionOwner)
    {
        candidate = value;
        owner = selectionOwner;

        InventoryItem item = candidate != null ? candidate.item : null;
        SkinData skin = item != null ? item.skin : null;

        if (skinImage != null)
        {
            skinImage.sprite = skin != null ? skin.icon : null;
            skinImage.enabled = skin != null && skin.icon != null;
            skinImage.preserveAspect = true;
        }

        if (titleText != null)
            titleText.text = skin != null
                ? SkinDisplayUtility.GetDisplayName(skin)
                : "Unknown Item";

        if (detailsText != null)
            detailsText.text = BuildDetails(item, candidate != null ? candidate.preview : null);

        if (pointsText != null)
        {
            pointsText.text = candidate != null && candidate.preview != null
                ? $"{candidate.preview.MuseumPoints:0.##} MP"
                : "0 MP";
        }

        if (warningsText != null)
            warningsText.text = BuildWarnings(candidate);

        if (blockedRoot != null)
            blockedRoot.SetActive(candidate == null || !candidate.selectable);

        if (blockedText != null)
            blockedText.text = candidate != null
                ? candidate.blockedReason
                : "Unavailable";

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
            button.interactable = candidate != null && candidate.selectable;
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
        if (owner != null && candidate != null && candidate.selectable)
            owner.SelectCandidate(candidate);
    }

    private static string BuildDetails(
        InventoryItem item,
        MuseumDonationPreview preview)
    {
        if (item == null)
            return "No item data";

        string wear = preview != null && preview.isVanilla
            ? "Vanilla"
            : preview != null
                ? preview.wearTier.ToString()
                : "Unknown Wear";
        string variant = preview != null
            ? preview.variant.ToString()
            : MuseumDonationKeyUtility.GetVariant(item).ToString();

        return
            $"{wear} | {variant}\n" +
            $"Float: {item.floatValue:0.000000}\n" +
            $"Pattern: {item.patternId} ({item.patternTier})\n" +
            $"Value: {item.marketValue:0.##} Gold";
    }

    private static string BuildWarnings(MuseumDonationCandidate candidate)
    {
        if (candidate == null ||
            candidate.warnings == null ||
            candidate.warnings.Count == 0)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < candidate.warnings.Count; i++)
        {
            MuseumDonationWarning warning = candidate.warnings[i];

            if (warning == null)
                continue;

            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append("! ");
            builder.Append(warning.message);
        }

        return builder.ToString();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }
}
