using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AutoAcquisitionCategoryCardUI : MonoBehaviour
{
    [SerializeField] private Button openButton;
    [SerializeField] private Button licenseButton;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text licenseButtonText;

    private AutoAcquisitionCategoryData category;
    private AutomatedAcquisitionsPanelUI owner;
    private AutoAcquisitionService service;

    public void Setup(
        AutoAcquisitionCategoryData data,
        AutomatedAcquisitionsPanelUI panel,
        AutoAcquisitionService acquisitionService)
    {
        category = data;
        owner = panel;
        service = acquisitionService;

        bool owned = service != null &&
                     category != null &&
                     service.OwnsCategory(category.categoryId);

        if (titleText != null)
            titleText.text = category != null ? category.DisplayName : "Archive";

        if (descriptionText != null)
            descriptionText.text = category != null ? category.description : "";

        if (iconImage != null)
        {
            iconImage.sprite = category != null ? category.icon : null;
            iconImage.enabled = iconImage.sprite != null;
            iconImage.preserveAspect = true;
        }

        int total = 0;
        int researched = 0;

        if (service != null && category != null)
        {
            var entries = service.GetContainers(category.categoryId);
            total = entries.Count;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null &&
                    service.IsContainerResearched(entries[i].containerId))
                {
                    researched++;
                }
            }
        }

        if (progressText != null)
        {
            progressText.text = owned
                ? $"Research: {researched:N0} / {total:N0}"
                : "ARCHIVE LOCKED";
        }

        if (licenseButtonText != null)
        {
            licenseButtonText.text = owned
                ? "LICENSED"
                : $"LICENSE — {category?.licenseCost ?? 0f:N0} GOLD";
        }

        SetupButton(openButton, HandleOpen);
        SetupButton(licenseButton, HandleLicense);

        if (openButton != null)
            openButton.interactable = owned;

        if (licenseButton != null)
            licenseButton.interactable = !owned && category != null;
    }

    private void HandleOpen()
    {
        if (owner != null && category != null)
            owner.SelectCategory(category.categoryId);
    }

    private void HandleLicense()
    {
        if (service == null || category == null)
            return;

        AutoAcquisitionActionResult result =
            service.BuyCategoryLicense(category.categoryId);

        if (owner != null)
            owner.HandleActionResult(result);
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
