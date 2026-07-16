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
}
