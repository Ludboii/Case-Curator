using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AutoAcquisitionContainerCardUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text orderText;
    [SerializeField] private TMP_Text bronzeText;
    [SerializeField] private TMP_Text researchCostText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private Button researchButton;
    [SerializeField] private TMP_Text researchButtonText;

    private AutoAcquisitionContainerData entry;
    private AutomatedAcquisitionsPanelUI owner;
    private AutoAcquisitionService service;

    public void Setup(
        AutoAcquisitionContainerData data,
        AutomatedAcquisitionsPanelUI panel,
        AutoAcquisitionService acquisitionService)
    {
        entry = data;
        owner = panel;
        service = acquisitionService;

        CaseData container = entry != null ? entry.container : null;
        bool researched = service != null &&
                          entry != null &&
                          service.IsContainerResearched(entry.containerId);
        bool bronze = container != null &&
                      ContainerProgressManager.Instance != null &&
                      ContainerProgressManager.Instance.IsBronzeComplete(container);
        bool categoryOwned = service != null &&
                             entry != null &&
                             service.OwnsCategory(entry.categoryId);

        if (titleText != null)
            titleText.text = entry != null ? entry.ContainerName : "Container";

        if (orderText != null)
            orderText.text = entry != null
                ? $"RESEARCH ORDER {entry.automatedUnlockOrder + 1:N0}"
                : "";

        if (bronzeText != null)
            bronzeText.text = bronze ? "BRONZE COMPLETE" : "BRONZE REQUIRED";

        if (researchCostText != null)
        {
            researchCostText.text = entry != null
                ? $"Research: {entry.permanentResearchCost:N0} Gold"
                : "";
        }

        if (stateText != null)
        {
            stateText.text = researched
                ? "RESEARCHED"
                : !categoryOwned
                    ? "ARCHIVE LICENCE REQUIRED"
                    : bronze
                        ? "AVAILABLE FOR RESEARCH"
                        : "LOCKED BY CONTAINER COMPLETION";
        }

        if (iconImage != null)
        {
            iconImage.sprite = container != null ? container.icon : null;
            iconImage.enabled = iconImage.sprite != null;
            iconImage.preserveAspect = true;
        }

        if (researchButtonText != null)
            researchButtonText.text = researched ? "RESEARCHED" : "RESEARCH";

        if (researchButton != null)
        {
            researchButton.onClick.RemoveAllListeners();
            researchButton.onClick.AddListener(HandleResearch);
            researchButton.interactable = !researched && categoryOwned;
        }
    }

    private void HandleResearch()
    {
        if (service == null || entry == null)
            return;

        AutoAcquisitionActionResult result =
            service.ResearchContainer(entry.containerId);

        if (owner != null)
            owner.HandleActionResult(result);
    }
}
