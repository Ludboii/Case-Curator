using System;
using System.Collections.Generic;
using UnityEngine;

public enum ContainerCompletionTier
{
    None = 0,
    Bronze = 1,
    Silver = 2,
    Gold = 3,
    Diamond = 4
}

public enum ContainerVariantRequirement
{
    None,
    StatTrak,
    Souvenir
}

public class ContainerProgressManager : MonoBehaviour
{
    public static ContainerProgressManager Instance { get; private set; }

    private const string LegacySaveKey = "ContainerProgress_Save_v1";

    public event Action OnContainerProgressChanged;

    [SerializeField] private ContainerProgressSaveData saveData =
        new ContainerProgressSaveData();

    private readonly Dictionary<string, ContainerProgressData> progressByContainer =
        new Dictionary<string, ContainerProgressData>();

    private bool loadedLegacyProgress;
    public bool HasLegacyProgress => loadedLegacyProgress;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadLegacyProgressForMigration();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public ContainerProgressData GetProgress(CaseData caseData)
    {
        string key = GetContainerKey(caseData);
        if (string.IsNullOrWhiteSpace(key)) return null;

        if (!progressByContainer.TryGetValue(key, out ContainerProgressData progress))
        {
            progress = new ContainerProgressData { containerId = key };
            EnsureProgressLists(progress);
            progressByContainer.Add(key, progress);
            saveData.progressEntries.Add(progress);
        }

        return progress;
    }

    public void RecordContainerOpened(
        CaseData caseData,
        InventoryItem pulledItem,
        float costPaid,
        bool saveImmediately = true)
    {
        if (caseData == null || pulledItem == null || pulledItem.skin == null)
            return;

        ContainerProgressData progress = GetProgress(caseData);
        if (progress == null) return;

        EnsureProgressLists(progress);
        progress.openedCount++;
        progress.totalSpent += Mathf.Max(0f, costPaid);
        progress.totalValuePulled += Mathf.Max(0f, pulledItem.marketValue);

        SkinData skin = pulledItem.skin;
        string skinKey = GetSkinKey(skin);

        if (skin.rarity == Rarity.RareSpecial)
        {
            progress.foundRareSpecial = true;
            AddUnique(progress.foundRareSpecialSkinKeys, skinKey);
        }
        else
        {
            // Souvenir is a variant of a normal skin, never a StatTrak item.
            AddUnique(progress.foundSkinKeys, skinKey);

            bool bestWear = IsBestPossibleWear(pulledItem);
            bool correctVariant = HasRequiredVariant(caseData, pulledItem);

            if (bestWear) AddUnique(progress.bestWearSkinKeys, skinKey);
            if (correctVariant) AddUnique(progress.variantSkinKeys, skinKey);
            if (bestWear && correctVariant)
                AddUnique(progress.bestWearVariantSkinKeys, skinKey);
        }

        if (saveImmediately)
        {
            SaveProgress();
            if (SaveManager.Instance != null)
                SaveManager.Instance.SaveGame();
        }
    }

    // Compatibility overload for older callers. Full wear/variant tracking
    // requires the InventoryItem overload above.
    public void RecordContainerOpened(
        CaseData caseData,
        SkinData pulledSkin,
        float costPaid,
        float pulledValue)
    {
        if (caseData == null || pulledSkin == null) return;

        ContainerProgressData progress = GetProgress(caseData);
        if (progress == null) return;

        EnsureProgressLists(progress);
        progress.openedCount++;
        progress.totalSpent += Mathf.Max(0f, costPaid);
        progress.totalValuePulled += Mathf.Max(0f, pulledValue);

        string skinKey = GetSkinKey(pulledSkin);
        if (pulledSkin.rarity == Rarity.RareSpecial)
        {
            progress.foundRareSpecial = true;
            AddUnique(progress.foundRareSpecialSkinKeys, skinKey);
        }
        else
        {
            AddUnique(progress.foundSkinKeys, skinKey);
        }

        SaveProgress();
        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveGame();
    }

    // SaveData 2.0 owns disk persistence. This method keeps the existing API
    // and sends the UI refresh event; the mutating caller saves via SaveManager.
    public void SaveProgress()
    {
        OnContainerProgressChanged?.Invoke();
    }

    public ContainerProgressSaveData ExportSaveData()
    {
        EnsureSaveData();
        ContainerProgressSaveData copy =
            JsonUtility.FromJson<ContainerProgressSaveData>(JsonUtility.ToJson(saveData));

        if (copy == null) copy = new ContainerProgressSaveData();
        if (copy.progressEntries == null)
            copy.progressEntries = new List<ContainerProgressData>();
        return copy;
    }

    public void ReplaceProgress(ContainerProgressSaveData loadedProgress)
    {
        if (loadedProgress == null)
        {
            saveData = new ContainerProgressSaveData();
        }
        else
        {
            saveData = JsonUtility.FromJson<ContainerProgressSaveData>(
                JsonUtility.ToJson(loadedProgress));

            if (saveData == null)
                saveData = new ContainerProgressSaveData();
        }

        loadedLegacyProgress = false;
        RebuildLookup();
        OnContainerProgressChanged?.Invoke();
    }

    public void DeleteLegacySaveAfterMigration()
    {
        DeleteLegacySaveFile();
        loadedLegacyProgress = false;
    }

    public static ContainerProgressSaveData ReadLegacySaveForMigration()
    {
        if (!PlayerPrefs.HasKey(LegacySaveKey))
            return new ContainerProgressSaveData();

        string json = PlayerPrefs.GetString(LegacySaveKey, "");
        if (string.IsNullOrWhiteSpace(json))
            return new ContainerProgressSaveData();

        try
        {
            ContainerProgressSaveData data =
                JsonUtility.FromJson<ContainerProgressSaveData>(json);
            return data ?? new ContainerProgressSaveData();
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"ContainerProgressManager: Failed to read legacy progress. {exception.Message}");
            return new ContainerProgressSaveData();
        }
    }

    public static void DeleteLegacySaveFile()
    {
        if (!PlayerPrefs.HasKey(LegacySaveKey)) return;
        PlayerPrefs.DeleteKey(LegacySaveKey);
        PlayerPrefs.Save();
    }

    public int GetOpenedCount(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);
        return progress != null ? progress.openedCount : 0;
    }

    public float GetProfit(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);
        return progress != null ? progress.totalValuePulled - progress.totalSpent : 0f;
    }

    public int GetFoundCount(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);
        if (progress == null) return 0;

        int found = CountMatchingNormalTargets(caseData, progress.foundSkinKeys);
        if (HasRareSpecialTarget(caseData) && progress.foundRareSpecial)
            found++;
        return found;
    }

    public int GetTargetCount(CaseData caseData)
    {
        int target = GetNormalSkinTargetCount(caseData);
        if (HasRareSpecialTarget(caseData)) target++;
        return target;
    }

    public int GetNormalSkinTargetCount(CaseData caseData)
    {
        return GetNormalSkinTargetKeys(caseData).Count;
    }

    public int GetBestWearCount(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);
        return progress == null ? 0 :
            CountMatchingNormalTargets(caseData, progress.bestWearSkinKeys);
    }

    public int GetVariantCount(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);
        return progress == null ? 0 :
            CountMatchingNormalTargets(caseData, progress.variantSkinKeys);
    }

    public int GetBestWearVariantCount(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);
        return progress == null ? 0 :
            CountMatchingNormalTargets(caseData, progress.bestWearVariantSkinKeys);
    }

    public ContainerCompletionTier GetCompletionTier(CaseData caseData)
    {
        if (IsDiamondComplete(caseData)) return ContainerCompletionTier.Diamond;
        if (IsGoldComplete(caseData)) return ContainerCompletionTier.Gold;
        if (IsSilverComplete(caseData)) return ContainerCompletionTier.Silver;
        if (IsBronzeComplete(caseData)) return ContainerCompletionTier.Bronze;
        return ContainerCompletionTier.None;
    }

    public bool IsTierComplete(CaseData caseData, ContainerCompletionTier tier)
    {
        switch (tier)
        {
            case ContainerCompletionTier.Bronze: return IsBronzeComplete(caseData);
            case ContainerCompletionTier.Silver: return IsSilverComplete(caseData);
            case ContainerCompletionTier.Gold: return IsGoldComplete(caseData);
            case ContainerCompletionTier.Diamond: return IsDiamondComplete(caseData);
            default: return false;
        }
    }

    public bool IsBronzeComplete(CaseData caseData)
    {
        int target = GetNormalSkinTargetCount(caseData);
        if (target <= 0) return false;

        ContainerProgressData progress = GetProgress(caseData);
        if (progress == null) return false;

        bool normalComplete =
            CountMatchingNormalTargets(caseData, progress.foundSkinKeys) >= target;
        bool rareComplete =
            !HasRareSpecialTarget(caseData) || progress.foundRareSpecial;
        return normalComplete && rareComplete;
    }

    public bool IsSilverComplete(CaseData caseData)
    {
        int target = GetNormalSkinTargetCount(caseData);
        return target > 0 && GetBestWearCount(caseData) >= target;
    }

    public bool IsGoldComplete(CaseData caseData)
    {
        if (GetVariantRequirement(caseData) == ContainerVariantRequirement.None)
            return false;

        int target = GetNormalSkinTargetCount(caseData);
        return target > 0 && GetVariantCount(caseData) >= target;
    }

    public bool IsDiamondComplete(CaseData caseData)
    {
        if (GetVariantRequirement(caseData) == ContainerVariantRequirement.None)
            return false;

        int target = GetNormalSkinTargetCount(caseData);
        return target > 0 && GetBestWearVariantCount(caseData) >= target;
    }

    public string GetCompletionDisplayText(CaseData caseData)
    {
        switch (GetCompletionTier(caseData))
        {
            case ContainerCompletionTier.Diamond: return "Diamond Completion";
            case ContainerCompletionTier.Gold: return "Gold Completion";
            case ContainerCompletionTier.Silver: return "Silver Completion";
            case ContainerCompletionTier.Bronze: return "Bronze Completion";
            default: return $"Found {GetFoundCount(caseData)} / {GetTargetCount(caseData)}";
        }
    }

    public string GetFoundDisplayText(CaseData caseData)
    {
        return GetCompletionDisplayText(caseData);
    }

    public ContainerVariantRequirement GetVariantRequirement(CaseData caseData)
    {
        if (caseData == null) return ContainerVariantRequirement.None;

        if (caseData.containerType == CaseContainerType.SouvenirPackage ||
            caseData.forceSouvenirDrops)
        {
            return ContainerVariantRequirement.Souvenir;
        }

        if (caseData.containerType == CaseContainerType.CollectionPackage)
            return ContainerVariantRequirement.None;

        return caseData.allowStatTrak
            ? ContainerVariantRequirement.StatTrak
            : ContainerVariantRequirement.None;
    }

    public string GetVariantDisplayName(CaseData caseData)
    {
        switch (GetVariantRequirement(caseData))
        {
            case ContainerVariantRequirement.Souvenir: return "Souvenir";
            case ContainerVariantRequirement.StatTrak: return "StatTrak";
            default: return "Unavailable";
        }
    }

    public bool HasFoundSkin(CaseData caseData, SkinData skin)
    {
        if (caseData == null || skin == null) return false;

        ContainerProgressData progress = GetProgress(caseData);
        if (progress == null) return false;

        EnsureProgressLists(progress);
        string key = GetSkinKey(skin);

        return skin.rarity == Rarity.RareSpecial
            ? progress.foundRareSpecialSkinKeys.Contains(key)
            : progress.foundSkinKeys.Contains(key);
    }

    public bool HasFoundRareSpecial(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);
        return progress != null && progress.foundRareSpecial;
    }

    public bool IsRewardClaimed(CaseData caseData, ContainerCompletionTier tier)
    {
        ContainerProgressData progress = GetProgress(caseData);
        if (progress == null) return false;

        switch (tier)
        {
            case ContainerCompletionTier.Bronze: return progress.bronzeRewardClaimed;
            case ContainerCompletionTier.Silver: return progress.silverRewardClaimed;
            case ContainerCompletionTier.Gold: return progress.goldRewardClaimed;
            case ContainerCompletionTier.Diamond: return progress.diamondRewardClaimed;
            default: return false;
        }
    }

    public bool IsRewardImplemented(ContainerCompletionTier tier)
    {
        return tier == ContainerCompletionTier.Bronze ||
               tier == ContainerCompletionTier.Silver;
    }

    public bool CanClaimReward(CaseData caseData, ContainerCompletionTier tier)
    {
        return IsRewardImplemented(tier) &&
               IsTierComplete(caseData, tier) &&
               !IsRewardClaimed(caseData, tier);
    }

    public bool ClaimReward(CaseData caseData, ContainerCompletionTier tier)
    {
        if (!CanClaimReward(caseData, tier)) return false;

        if (CaseInventoryManager.Instance == null)
        {
            Debug.LogWarning(
                "ContainerProgressManager: Cannot claim reward because CaseInventoryManager is missing.");
            return false;
        }

        ContainerProgressData progress = GetProgress(caseData);
        if (progress == null) return false;

        int reward;
        switch (tier)
        {
            case ContainerCompletionTier.Bronze:
                reward = 20;
                progress.bronzeRewardClaimed = true;
                break;
            case ContainerCompletionTier.Silver:
                reward = 40;
                progress.silverRewardClaimed = true;
                break;
            default:
                return false;
        }

        CaseInventoryManager.Instance.AddCases(caseData, reward);
        SaveProgress();
        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveGame();

        Debug.Log($"Claimed {tier} completion reward: {reward}x {caseData.caseName}.");
        return true;
    }

    public void ResetAllProgressForTesting()
    {
        saveData = new ContainerProgressSaveData();
        progressByContainer.Clear();
        DeleteLegacySaveAfterMigration();
        OnContainerProgressChanged?.Invoke();

        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveGame();

        Debug.Log("ContainerProgressManager: Reset all container progress.");
    }

    private bool HasRequiredVariant(CaseData caseData, InventoryItem item)
    {
        if (item == null) return false;

        switch (GetVariantRequirement(caseData))
        {
            case ContainerVariantRequirement.Souvenir:
                return item.souvenir && !item.statTrak;
            case ContainerVariantRequirement.StatTrak:
                return item.statTrak && !item.souvenir;
            default:
                return false;
        }
    }

    private bool IsBestPossibleWear(InventoryItem item)
    {
        if (item == null || item.skin == null) return false;
        if (item.isVanilla || item.skin.isVanilla) return true;

        int bestWear = WearUtility.GetWearIndex(item.skin.minFloat);
        int openedWear = WearUtility.GetWearIndex((float)item.floatValue);
        return openedWear == bestWear;
    }

    private int CountMatchingNormalTargets(CaseData caseData, List<string> completedKeys)
    {
        if (completedKeys == null || completedKeys.Count == 0) return 0;

        int count = 0;
        foreach (string key in GetNormalSkinTargetKeys(caseData))
            if (completedKeys.Contains(key)) count++;
        return count;
    }

    private HashSet<string> GetNormalSkinTargetKeys(CaseData caseData)
    {
        HashSet<string> keys = new HashSet<string>();
        if (caseData == null || caseData.dropPool == null) return keys;

        foreach (WeightedDrop drop in caseData.dropPool)
        {
            if (drop == null || drop.skin == null || drop.skin.rarity == Rarity.RareSpecial)
                continue;
            keys.Add(GetSkinKey(drop.skin));
        }

        return keys;
    }

    private bool HasRareSpecialTarget(CaseData caseData)
    {
        if (caseData == null || caseData.dropPool == null) return false;

        foreach (WeightedDrop drop in caseData.dropPool)
        {
            if (drop != null && drop.skin != null &&
                drop.skin.rarity == Rarity.RareSpecial)
                return true;
        }

        return false;
    }

    private void LoadLegacyProgressForMigration()
    {
        saveData = ReadLegacySaveForMigration();
        progressByContainer.Clear();
        RebuildLookup();

        loadedLegacyProgress = saveData.progressEntries.Count > 0;
        if (loadedLegacyProgress)
        {
            Debug.Log(
                "ContainerProgressManager: Loaded legacy progress for SaveData 2.0 migration.");
        }
    }

    private void RebuildLookup()
    {
        progressByContainer.Clear();
        EnsureSaveData();

        List<ContainerProgressData> valid = new List<ContainerProgressData>();

        foreach (ContainerProgressData progress in saveData.progressEntries)
        {
            if (progress == null || string.IsNullOrWhiteSpace(progress.containerId))
                continue;

            EnsureProgressLists(progress);
            if (progressByContainer.ContainsKey(progress.containerId))
                continue;

            progressByContainer.Add(progress.containerId, progress);
            valid.Add(progress);
        }

        saveData.progressEntries = valid;
    }

    private void EnsureSaveData()
    {
        if (saveData == null) saveData = new ContainerProgressSaveData();
        if (saveData.progressEntries == null)
            saveData.progressEntries = new List<ContainerProgressData>();
    }

    private static void AddUnique(List<string> list, string value)
    {
        if (list == null || string.IsNullOrWhiteSpace(value)) return;
        if (!list.Contains(value)) list.Add(value);
    }

    private static string GetContainerKey(CaseData caseData)
    {
        if (caseData == null) return "";
        return !string.IsNullOrWhiteSpace(caseData.apiId)
            ? caseData.apiId
            : caseData.caseName;
    }

    private static string GetSkinKey(SkinData skin)
    {
        if (skin == null) return "";
        return !string.IsNullOrWhiteSpace(skin.apiId)
            ? skin.apiId
            : $"{skin.weaponName}|{skin.skinName}|{skin.rarity}";
    }

    private static void EnsureProgressLists(ContainerProgressData progress)
    {
        if (progress == null) return;
        if (progress.foundSkinKeys == null) progress.foundSkinKeys = new List<string>();
        if (progress.foundRareSpecialSkinKeys == null)
            progress.foundRareSpecialSkinKeys = new List<string>();
        if (progress.bestWearSkinKeys == null) progress.bestWearSkinKeys = new List<string>();
        if (progress.variantSkinKeys == null) progress.variantSkinKeys = new List<string>();
        if (progress.bestWearVariantSkinKeys == null)
            progress.bestWearVariantSkinKeys = new List<string>();
    }
}

[Serializable]
public class ContainerProgressSaveData
{
    public List<ContainerProgressData> progressEntries =
        new List<ContainerProgressData>();
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
    public List<string> foundRareSpecialSkinKeys = new List<string>();
    public List<string> bestWearSkinKeys = new List<string>();
    public List<string> variantSkinKeys = new List<string>();
    public List<string> bestWearVariantSkinKeys = new List<string>();

    public bool bronzeRewardClaimed;
    public bool silverRewardClaimed;
    public bool goldRewardClaimed;
    public bool diamondRewardClaimed;
}
