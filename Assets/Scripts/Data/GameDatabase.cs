using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "GameDatabase",
    menuName = "Case Catcher/Game Database")]
public class GameDatabase : ScriptableObject
{
    [Header("Core Content")]
    public List<SkinData> allSkins = new List<SkinData>();
    public List<CaseData> allCases = new List<CaseData>();
    public List<CollectionData> allCollections = new List<CollectionData>();

    [Header("Progression")]
    public UpgradeCatalog upgradeCatalog;

    [Header("Museum")]
    public MuseumBalanceData museumBalance;
    public MuseumCatalogConfig museumCatalog;

    public List<MuseumMilestoneData> museumMilestones =
        new List<MuseumMilestoneData>();

    public SkinData GetSkinByApiId(string apiId)
    {
        foreach (SkinData skin in allSkins)
        {
            if (skin != null && skin.apiId == apiId)
                return skin;
        }

        return null;
    }

    public CaseData GetCaseByApiId(string apiId)
    {
        foreach (CaseData caseData in allCases)
        {
            if (caseData != null && caseData.apiId == apiId)
                return caseData;
        }

        return null;
    }

    public CollectionData GetCollectionByName(string collectionName)
    {
        foreach (CollectionData collection in allCollections)
        {
            if (collection != null && collection.collectionName == collectionName)
                return collection;
        }

        return null;
    }

    public UpgradeData GetUpgradeById(string upgradeId)
    {
        return upgradeCatalog != null
            ? upgradeCatalog.GetUpgradeById(upgradeId)
            : null;
    }

    public MuseumMilestoneData GetMuseumMilestoneById(string milestoneId)
    {
        if (string.IsNullOrWhiteSpace(milestoneId) ||
            museumMilestones == null)
        {
            return null;
        }

        for (int i = 0; i < museumMilestones.Count; i++)
        {
            MuseumMilestoneData milestone = museumMilestones[i];

            if (milestone != null &&
                string.Equals(
                    milestone.milestoneId,
                    milestoneId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return milestone;
            }
        }

        return null;
    }

    private void OnValidate()
    {
        if (allSkins == null)
            allSkins = new List<SkinData>();

        if (allCases == null)
            allCases = new List<CaseData>();

        if (allCollections == null)
            allCollections = new List<CollectionData>();

        if (museumMilestones == null)
            museumMilestones = new List<MuseumMilestoneData>();
    }
}
