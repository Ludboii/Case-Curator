using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AutoAcquisitionCategoryData
{
    [Tooltip("Stable save ID. Do not change after release.")]
    public string categoryId;

    public string displayName;

    [TextArea(2, 5)]
    public string description;

    public Sprite icon;
    public int sortOrder;

    [Min(0f)] public float licenseCost;
    [Min(0f)] public float minimumResearchCost;
    [Min(0f)] public float researchCostMultiplier = 1f;

    public List<CaseContainerType> supportedContainerTypes =
        new List<CaseContainerType>();

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(displayName)
            ? displayName.Trim()
            : categoryId;

    public bool Supports(CaseData container)
    {
        return container != null &&
               supportedContainerTypes != null &&
               supportedContainerTypes.Contains(container.containerType);
    }
}

[Serializable]
public class AutoAcquisitionContainerData
{
    [Tooltip("Stable identity copied from CaseData.apiId, with a name fallback.")]
    public string containerId;

    public CaseData container;
    public string categoryId;

    [Tooltip(
        "Permanent sequence number. Existing rows keep this number when the " +
        "setup command is rerun, even after container prices change.")]
    [Min(0)] public int automatedUnlockOrder;

    [Min(0f)] public float permanentResearchCost;
    [Min(0.01f)] public float processingDurationMultiplier = 1f;

    public string ContainerName =>
        container != null && !string.IsNullOrWhiteSpace(container.caseName)
            ? container.caseName.Trim()
            : containerId;
}

/// <summary>
/// Data-generated catalogue for the late-game Automated Acquisitions Wing.
/// Category and container order is stored in this asset so economy rebalancing
/// does not silently reorder already-shipped research progression.
/// </summary>
[CreateAssetMenu(
    fileName = "AutoAcquisitionCatalog",
    menuName = "Case Curator/Automated Acquisitions/Catalog")]
public class AutoAcquisitionCatalogData : ScriptableObject
{
    public const string WeaponCaseCategoryId =
        "auto-acq-category-weapon-cases";

    public const string CollectionPackageCategoryId =
        "auto-acq-category-collection-packages";

    [Header("Wing Presentation")]
    public string wingDisplayName = "Automated Acquisitions Wing";

    [TextArea(2, 6)]
    public string wingDescription =
        "Fund Museum acquisition lines, research Bronze-completed containers " +
        "and claim generated items from the Intake Vault.";

    [Header("Generated Catalogue")]
    public List<AutoAcquisitionCategoryData> categories =
        new List<AutoAcquisitionCategoryData>();

    public List<AutoAcquisitionContainerData> containers =
        new List<AutoAcquisitionContainerData>();

    [Header("Runtime Safety")]
    [Min(1)] public int maximumOfflineOpeningsPerLine = 10000;
    [Min(1)] public int maximumCalibrationAttempts = 32;
    [Min(0.1f)] public float runtimeTickSeconds = 1f;

    [Header("Curator Alert Thresholds")]
    [Min(0f)] public float exceptionalValueThreshold = 1000f;
    [Range(0f, 1f)] public float pristineFloatThreshold = 0.001f;
    [Range(0f, 1f)] public float extremeHighFloatThreshold = 0.999f;

    public AutoAcquisitionCategoryData GetCategory(string categoryId)
    {
        if (categories == null || string.IsNullOrWhiteSpace(categoryId))
            return null;

        for (int i = 0; i < categories.Count; i++)
        {
            AutoAcquisitionCategoryData entry = categories[i];

            if (entry != null &&
                string.Equals(
                    entry.categoryId,
                    categoryId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    public AutoAcquisitionContainerData GetContainer(string containerId)
    {
        if (containers == null || string.IsNullOrWhiteSpace(containerId))
            return null;

        for (int i = 0; i < containers.Count; i++)
        {
            AutoAcquisitionContainerData entry = containers[i];

            if (entry != null &&
                string.Equals(
                    entry.containerId,
                    containerId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    public List<AutoAcquisitionContainerData> GetContainersInCategory(
        string categoryId)
    {
        List<AutoAcquisitionContainerData> result =
            new List<AutoAcquisitionContainerData>();

        if (containers == null || string.IsNullOrWhiteSpace(categoryId))
            return result;

        for (int i = 0; i < containers.Count; i++)
        {
            AutoAcquisitionContainerData entry = containers[i];

            if (entry != null &&
                entry.container != null &&
                string.Equals(
                    entry.categoryId,
                    categoryId,
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Add(entry);
            }
        }

        result.Sort((a, b) =>
        {
            int order = a.automatedUnlockOrder.CompareTo(
                b.automatedUnlockOrder);

            return order != 0
                ? order
                : string.Compare(
                    a.ContainerName,
                    b.ContainerName,
                    StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    private void OnValidate()
    {
        maximumOfflineOpeningsPerLine =
            Mathf.Max(1, maximumOfflineOpeningsPerLine);
        maximumCalibrationAttempts = Mathf.Max(1, maximumCalibrationAttempts);
        runtimeTickSeconds = Mathf.Max(0.1f, runtimeTickSeconds);
        exceptionalValueThreshold = Mathf.Max(0f, exceptionalValueThreshold);
        pristineFloatThreshold = Mathf.Clamp01(pristineFloatThreshold);
        extremeHighFloatThreshold = Mathf.Clamp01(extremeHighFloatThreshold);

        if (categories == null)
            categories = new List<AutoAcquisitionCategoryData>();

        if (containers == null)
            containers = new List<AutoAcquisitionContainerData>();
    }
}
