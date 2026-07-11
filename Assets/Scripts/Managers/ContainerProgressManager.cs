using System;
using System.Collections.Generic;
using UnityEngine;

public class ContainerProgressManager : MonoBehaviour
{
    public static ContainerProgressManager Instance { get; private set; }

    private const string SaveKey = "ContainerProgress_Save_v1";

    [SerializeField] private ContainerProgressSaveData saveData = new ContainerProgressSaveData();

    private readonly Dictionary<string, ContainerProgressData> progressByContainer =
        new Dictionary<string, ContainerProgressData>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    public ContainerProgressData GetProgress(CaseData caseData)
    {
        string key = GetContainerKey(caseData);

        if (string.IsNullOrWhiteSpace(key))
            return null;

        if (!progressByContainer.TryGetValue(key, out ContainerProgressData progress))
        {
            progress = new ContainerProgressData
            {
                containerId = key
            };

            progressByContainer.Add(key, progress);
            saveData.progressEntries.Add(progress);
        }

        return progress;
    }

    public void RecordContainerOpened(
        CaseData caseData,
        SkinData pulledSkin,
        float costPaid,
        float pulledValue)
    {
        if (caseData == null || pulledSkin == null)
            return;

        ContainerProgressData progress = GetProgress(caseData);

        if (progress == null)
            return;

        progress.openedCount++;
        progress.totalSpent += Mathf.Max(0f, costPaid);
        progress.totalValuePulled += Mathf.Max(0f, pulledValue);

        if (pulledSkin.rarity == Rarity.RareSpecial)
        {
            progress.foundRareSpecial = true;
        }
        else
        {
            AddUnique(progress.foundSkinKeys, GetSkinKey(pulledSkin));
        }

        Save();
    }

    public int GetOpenedCount(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);
        return progress != null ? progress.openedCount : 0;
    }

    public float GetProfit(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);

        if (progress == null)
            return 0f;

        return progress.totalValuePulled - progress.totalSpent;
    }

    public int GetFoundCount(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);

        if (progress == null)
            return 0;

        int found = progress.foundSkinKeys.Count;

        if (progress.foundRareSpecial)
            found++;

        return found;
    }

    public bool HasFoundSkin(CaseData caseData, SkinData skin)
{
    if (caseData == null || skin == null)
        return false;

    ContainerProgressData progress = GetProgress(caseData);

    if (progress == null || progress.foundSkinKeys == null)
        return false;

    return progress.foundSkinKeys.Contains(GetSkinKey(skin));
}

public bool HasFoundRareSpecial(CaseData caseData)
{
    ContainerProgressData progress = GetProgress(caseData);

    if (progress == null)
        return false;

    return progress.foundRareSpecial;
}

    public int GetTargetCount(CaseData caseData)
    {
        if (caseData == null || caseData.dropPool == null)
            return 0;

        HashSet<string> normalSkinKeys = new HashSet<string>();
        bool hasRareSpecial = false;

        foreach (WeightedDrop drop in caseData.dropPool)
        {
            if (drop == null || drop.skin == null)
                continue;

            if (drop.skin.rarity == Rarity.RareSpecial)
            {
                hasRareSpecial = true;
                continue;
            }

            normalSkinKeys.Add(GetSkinKey(drop.skin));
        }

        int target = normalSkinKeys.Count;

        if (hasRareSpecial)
            target++;

        return target;
    }

    public string GetFoundDisplayText(CaseData caseData)
    {
        return $"Found {GetFoundCount(caseData)} / {GetTargetCount(caseData)}";
    }

    private static void AddUnique(List<string> list, string value)
    {
        if (list == null)
            return;

        if (string.IsNullOrWhiteSpace(value))
            return;

        if (!list.Contains(value))
            list.Add(value);
    }

    private static string GetContainerKey(CaseData caseData)
    {
        if (caseData == null)
            return "";

        if (!string.IsNullOrWhiteSpace(caseData.apiId))
            return caseData.apiId;

        return caseData.caseName;
    }

    private static string GetSkinKey(SkinData skin)
    {
        if (skin == null)
            return "";

        if (!string.IsNullOrWhiteSpace(skin.apiId))
            return skin.apiId;

        return $"{skin.weaponName}|{skin.skinName}|{skin.rarity}";
    }

    private void Load()
    {
        saveData = new ContainerProgressSaveData();
        progressByContainer.Clear();

        if (!PlayerPrefs.HasKey(SaveKey))
            return;

        string json = PlayerPrefs.GetString(SaveKey, "");

        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            saveData = JsonUtility.FromJson<ContainerProgressSaveData>(json);

            if (saveData == null)
                saveData = new ContainerProgressSaveData();

            if (saveData.progressEntries == null)
                saveData.progressEntries = new List<ContainerProgressData>();

            foreach (ContainerProgressData progress in saveData.progressEntries)
            {
                if (progress == null || string.IsNullOrWhiteSpace(progress.containerId))
                    continue;

                if (!progressByContainer.ContainsKey(progress.containerId))
                    progressByContainer.Add(progress.containerId, progress);
            }
        }
        catch
        {
            saveData = new ContainerProgressSaveData();
            progressByContainer.Clear();
        }
    }

    private void Save()
    {
        string json = JsonUtility.ToJson(saveData);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
    }
}

[Serializable]
public class ContainerProgressSaveData
{
    public List<ContainerProgressData> progressEntries = new List<ContainerProgressData>();
}

[Serializable]
public class ContainerProgressData
{
    public string containerId;

    public int openedCount;
    public float totalSpent;
    public float totalValuePulled;

    public bool foundRareSpecial;
    public List<string> foundSkinKeys = new List<string>();
}