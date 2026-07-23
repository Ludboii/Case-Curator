using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Temporary Phase M1 test surface. It previews or donates one inventory item
/// by list index and prints catalog/save totals. It is not intended as the
/// final Museum user interface.
/// </summary>
public class MuseumDebugUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MuseumService museumService;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button previewButton;
    [SerializeField] private Button donateButton;
    [SerializeField] private Button refreshButton;

    [Header("Selected Test Item")]
    [Min(0)]
    [SerializeField] private int inventoryItemIndex;

    private void Awake()
    {
        if (museumService == null)
            museumService = MuseumService.Instance;

        if (previewButton != null)
        {
            previewButton.onClick.RemoveListener(PreviewSelectedItem);
            previewButton.onClick.AddListener(PreviewSelectedItem);
        }

        if (donateButton != null)
        {
            donateButton.onClick.RemoveListener(DonateSelectedItem);
            donateButton.onClick.AddListener(DonateSelectedItem);
        }

        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(RefreshSummary);
            refreshButton.onClick.AddListener(RefreshSummary);
        }
    }

    private void Start()
    {
        if (museumService == null)
            museumService = MuseumService.Instance;

        RefreshSummary();
    }

    [ContextMenu("Preview Selected Inventory Item")]
    public void PreviewSelectedItem()
    {
        InventoryItem item = GetSelectedItem();

        if (museumService == null)
            museumService = MuseumService.Instance;

        if (museumService == null)
        {
            SetStatus("MuseumService is missing.");
            return;
        }

        MuseumDonationPreview preview =
            museumService.PreviewDonation(item);

        if (preview == null)
        {
            SetStatus("No donation preview was returned.");
            return;
        }

        if (!preview.canDonate)
        {
            SetStatus(
                $"Cannot donate item {inventoryItemIndex}:\n" +
                preview.message);
            return;
        }

        string displayName =
            SkinDisplayUtility.GetDisplayName(preview.skin);

        SetStatus(
            $"PREVIEW\n" +
            $"Item: {displayName}\n" +
            $"Slot: {preview.variant}, wear {preview.wearIndex}\n" +
            $"Museum Points: {preview.MuseumPoints:0.##}\n" +
            $"Key: {preview.donationKey}");
    }

    [ContextMenu("Donate Selected Inventory Item")]
    public void DonateSelectedItem()
    {
        InventoryItem item = GetSelectedItem();

        if (museumService == null)
            museumService = MuseumService.Instance;

        if (museumService == null)
        {
            SetStatus("MuseumService is missing.");
            return;
        }

        MuseumDonationResult result = museumService.Donate(item);

        if (result == null || !result.success)
        {
            SetStatus(
                result != null
                    ? $"DONATION FAILED\n{result.message}"
                    : "DONATION FAILED\nNo result returned.");
            return;
        }

        SetStatus(
            $"DONATED\n" +
            $"{SkinDisplayUtility.GetDisplayName(result.donatedItem.skin)}\n" +
            $"+{result.museumPointsAwarded:0.##} Museum Points\n" +
            $"Total: {result.totalMuseumPoints:0.##}");
    }

    [ContextMenu("Refresh Museum Summary")]
    public void RefreshSummary()
    {
        if (museumService == null)
            museumService = MuseumService.Instance;

        if (museumService == null)
        {
            SetStatus("MuseumService is missing.");
            return;
        }

        MuseumCatalogSnapshot catalog =
            museumService.GetCatalogSnapshot(true);

        int inventoryCount = InventoryManager.Instance != null
            ? InventoryManager.Instance.Count
            : 0;

        SetStatus(
            $"MUSEUM PHASE M1\n" +
            $"Museum Points: {museumService.MuseumPoints:0.##}\n" +
            $"Donated slots: {museumService.DonatedSlotCount}\n" +
            $"Catalog slots: {catalog.donatedSlots}/{catalog.totalSlots}\n" +
            $"Catalog skins: {catalog.totalSkins}\n" +
            $"Inventory items: {inventoryCount}\n" +
            $"Selected inventory index: {inventoryItemIndex}");
    }

    private InventoryItem GetSelectedItem()
    {
        if (InventoryManager.Instance == null ||
            InventoryManager.Instance.Items == null)
        {
            return null;
        }

        if (inventoryItemIndex < 0 ||
            inventoryItemIndex >= InventoryManager.Instance.Items.Count)
        {
            return null;
        }

        return InventoryManager.Instance.Items[inventoryItemIndex];
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message ?? "";

        Debug.Log(message ?? "", this);
    }
}
