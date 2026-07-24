using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Final irreversible M3 confirmation. The item is resolved and previewed again
/// by instance ID immediately before MuseumService.Donate is called.
/// </summary>
public class MuseumDonationConfirmationUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Image skinImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text detailsText;
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private TMP_Text warningsText;
    [SerializeField] private TMP_Text irreversibleWarningText;
    [SerializeField] private TMP_Text errorText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private MuseumSlotEntry slot;
    private MuseumDonationCandidate candidate;
    private MuseumPanelUI owner;
    private MuseumService service;
    private bool processing;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(Confirm);
            confirmButton.onClick.AddListener(Confirm);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(Close);
            cancelButton.onClick.AddListener(Close);
        }

        if (irreversibleWarningText != null)
        {
            irreversibleWarningText.text =
                "This donation permanently removes the selected item from your " +
                "inventory. It cannot be recovered.";
        }
    }

    public void Open(
        MuseumSlotEntry targetSlot,
        MuseumDonationCandidate selectedCandidate,
        MuseumPanelUI panel,
        MuseumService museumService)
    {
        slot = targetSlot;
        candidate = selectedCandidate;
        owner = panel;
        service = museumService;
        processing = false;

        if (root == null)
            root = gameObject;

        root.SetActive(true);

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
                : "Museum Donation";

        if (detailsText != null)
            detailsText.text = BuildDetails(item, candidate != null ? candidate.preview : null);

        if (pointsText != null)
            pointsText.text = BuildPointBreakdown(candidate != null ? candidate.preview : null);

        if (warningsText != null)
            warningsText.text = BuildWarnings(candidate);

        if (errorText != null)
            errorText.text = "";

        if (confirmButton != null)
            confirmButton.interactable = candidate != null && candidate.selectable;
    }

    public void Close()
    {
        processing = false;
        slot = null;
        candidate = null;

        if (root != null)
            root.SetActive(false);
    }

    private void Confirm()
    {
        if (processing || candidate == null || service == null)
            return;

        processing = true;

        if (confirmButton != null)
            confirmButton.interactable = false;

        MuseumDonationPreview freshPreview =
            service.PreviewDonation(candidate.instanceId);

        if (freshPreview == null || !freshPreview.canDonate)
        {
            ShowError(freshPreview != null
                ? freshPreview.message
                : "The selected item is no longer available.");
            processing = false;
            return;
        }

        if (slot == null ||
            !string.Equals(
                freshPreview.donationKey,
                slot.donationKey,
                System.StringComparison.Ordinal))
        {
            ShowError("The selected item no longer matches this Museum slot.");
            processing = false;
            return;
        }

        string donatedKey = slot.donationKey;
        MuseumDonationResult result = service.Donate(candidate.instanceId);

        if (result == null || !result.success)
        {
            ShowError(result != null
                ? result.message
                : "Museum donation failed.");
            processing = false;
            return;
        }

        Close();

        if (owner != null)
            owner.HandleDonationCompleted(result, donatedKey);
    }

    private void ShowError(string message)
    {
        if (errorText != null)
            errorText.text = message;

        if (confirmButton != null)
            confirmButton.interactable = true;
    }

    private static string BuildDetails(
        InventoryItem item,
        MuseumDonationPreview preview)
    {
        if (item == null)
            return "No item selected.";

        string wear = preview != null && preview.isVanilla
            ? "Vanilla"
            : preview != null
                ? preview.wearTier.ToString()
                : "Unknown";
        string variant = preview != null
            ? preview.variant.ToString()
            : MuseumDonationKeyUtility.GetVariant(item).ToString();

        return
            $"Slot: {wear} | {variant}\n" +
            $"Float: {item.floatValue:0.000000}\n" +
            $"Pattern: {item.patternId} ({item.patternTier})\n" +
            $"Market value: {item.marketValue:0.##} Gold";
    }

    private static string BuildPointBreakdown(MuseumDonationPreview preview)
    {
        if (preview == null || preview.points == null)
            return "Reward: 0 MP";

        MuseumPointBreakdown points = preview.points;
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Base slot reward: {points.rarityWearPoints:0.##} MP");

        if (System.Math.Abs(points.variantMultiplier - 1d) > 0.0001d)
            builder.AppendLine($"Variant multiplier: x{points.variantMultiplier:0.##}");

        if (points.marketValueBonus > 0d)
        {
            builder.AppendLine(
                $"Market-value bonus: +{points.marketValueBonus:0.##} MP " +
                $"({points.marketValueBonusRate * 100d:0.##}%)");
        }

        builder.Append($"Total reward: {points.totalPoints:0.##} MP");
        return builder.ToString();
    }

    private static string BuildWarnings(MuseumDonationCandidate value)
    {
        if (value == null ||
            value.warnings == null ||
            value.warnings.Count == 0)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < value.warnings.Count; i++)
        {
            MuseumDonationWarning warning = value.warnings[i];

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
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(Confirm);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(Close);
    }
}
