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

    private bool legacyProgressReadyForDeletion;

    public bool HasAnyProgress =>
        saveData != null &&
        saveData.progressEntries != null &&
        saveData.progressEntries.Count > 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ReplaceProgress(new ContainerProgressSaveData(), false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public ContainerProgressData GetProgress(CaseData caseData)
    {
        string key = GetContainerKey(caseData);

        if (string.IsNullOrWhiteSpace(key))
            return null;

        if (!progressByContainer.TryGetValue(
                key,
                out ContainerProgressData progress))
        {
            progress = new ContainerProgressData
            {
                containerId = key
            };

            EnsureProgressLists(progress);
            progressByContainer.Add(key, progress);
            saveData.progressEntries.Add(progress);
        }

        return progress;
    }

    /// <summary>
    /// Records one generated inventory item for a container. Pass
    /// saveImmediately=false when opening several containers, then save once
    /// after the batch.
    /// </summary>
    public void RecordContainerOpened(
        CaseData caseData,
        InventoryItem pulledItem,
        float costPaid,
        bool saveImmediately = true)
    {
        if (caseData == null || pulledItem == null || pulledItem.skin == null)
            return;

        ContainerProgressData progress = GetProgress(caseData);

        if (progress == null)
            return;

        EnsureProgressLists(progress);

        progress.openedCount++;
        progress.totalSpent += Mathf.Max(0f, costPaid);
        progress.totalValuePulled += Mathf.Max(0f, pulledItem.marketValue);

        SkinData pulledSkin = pulledItem.skin;
        string skinKey = GetSkinKey(pulledSkin);

        if (pulledSkin.rarity == Rarity.RareSpecial)
        {
            // Bronze needs any one Rare Special item. The inspect list still
            // records every specific knife or glove that has been discovered.
            progress.foundRareSpecial = true;
            AddUnique(progress.foundRareSpecialSkinKeys, skinKey);
        }
        else
        {
            // Normal and Souvenir pulls both discover the underlying skin.
            AddUnique(progress.foundSkinKeys, skinKey);

            bool bestWear = IsBestPossibleWear(pulledItem);
            bool correctVariant = HasRequiredVariant(caseData, pulledItem);

            if (bestWear)
                AddUnique(progress.bestWearSkinKeys, skinKey);

            if (correctVariant)
                AddUnique(progress.variantSkinKeys, skinKey);

            if (bestWear && correctVariant)
                AddUnique(progress.bestWearVariantSkinKeys, skinKey);
        }

        if (saveImmediately)
            SaveProgress(true);
    }

    /// <summary>
    /// Compatibility overload for older callers. It tracks discovery, but the
    /// InventoryItem overload is required for wear and variant completion.
    /// </summary>
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

        SaveProgress(true);
    }

    /// <summary>
    /// Container progress is part of SaveData v2. This method refreshes UI and
    /// optionally writes the complete game save. Use saveGame=false when the
    /// caller already performs a single batched SaveGame call.
    /// </summary>
    public void SaveProgress(bool saveGame = false)
    {
        OnContainerProgressChanged?.Invoke();

        if (saveGame && SaveManager.Instance != null)
            SaveManager.Instance.SaveGame();
    }

    public void NotifyProgressChanged()
    {
        OnContainerProgressChanged?.Invoke();
    }

    public ContainerProgressSaveData CreateSaveDataSnapshot()
    {
        ContainerProgressSaveData snapshot = new ContainerProgressSaveData();

        if (saveData == null || saveData.progressEntries == null)
            return snapshot;

        foreach (ContainerProgressData progress in saveData.progressEntries)
        {
            if (progress == null || string.IsNullOrWhiteSpace(progress.containerId))
                continue;

            snapshot.progressEntries.Add(CloneProgress(progress));
        }

        return snapshot;
    }

    public void ReplaceProgress(
        ContainerProgressSaveData loadedData,
        bool notify = true)
    {
        saveData = loadedData ?? new ContainerProgressSaveData();

        if (saveData.progressEntries == null)
        {
            saveData.progressEntries =
                new List<ContainerProgressData>();
        }

        progressByContainer.Clear();

        for (int i = saveData.progressEntries.Count - 1; i >= 0; i--)
        {
            ContainerProgressData progress = saveData.progressEntries[i];

            if (progress == null || string.IsNullOrWhiteSpace(progress.containerId))
            {
                saveData.progressEntries.RemoveAt(i);
                continue;
            }

            EnsureProgressLists(progress);

            if (progressByContainer.ContainsKey(progress.containerId))
            {
                saveData.progressEntries.RemoveAt(i);
                continue;
            }

            progressByContainer.Add(progress.containerId, progress);
        }

        legacyProgressReadyForDeletion =
            HasAnyProgress && PlayerPrefs.HasKey(LegacySaveKey);

        if (notify)
            OnContainerProgressChanged?.Invoke();
    }

    public bool TryImportLegacyProgress(bool onlyIfCurrentEmpty = true)
    {
        if (onlyIfCurrentEmpty && HasAnyProgress)
            return false;

        if (!PlayerPrefs.HasKey(LegacySaveKey))
            return false;

        string json = PlayerPrefs.GetString(LegacySaveKey, "");

        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            ContainerProgressSaveData legacyData =
                JsonUtility.FromJson<ContainerProgressSaveData>(json);

            if (legacyData == null ||
                legacyData.progressEntries == null ||
                legacyData.progressEntries.Count == 0)
            {
                return false;
            }

            ReplaceProgress(legacyData, true);
            legacyProgressReadyForDeletion = true;

            Debug.Log(
                "Imported legacy PlayerPrefs container progress into SaveData v2.");

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"ContainerProgressManager: Failed to import legacy progress. " +
                $"{exception.Message}");
            return false;
        }
    }

    public void DeleteLegacyProgressAfterMigration(bool force = false)
    {
        if (!force && !legacyProgressReadyForDeletion)
            return;

        if (PlayerPrefs.HasKey(LegacySaveKey))
        {
            PlayerPrefs.DeleteKey(LegacySaveKey);
            PlayerPrefs.Save();
        }

        legacyProgressReadyForDeletion = false;
    }

    public int GetOpenedCount(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);
        return progress != null ? progress.openedCount : 0;
    }

    public float GetProfit(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);
        return progress != null
            ? progress.totalValuePulled - progress.totalSpent
            : 0f;
    }

    public int GetFoundCount(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);

        if (progress == null)
            return 0;

        int found = CountMatchingNormalTargets(
            caseData,
            progress.foundSkinKeys);

        if (HasRareSpecialTarget(caseData) && progress.foundRareSpecial)
            found++;

        return found;
    }

    public int GetTargetCount(CaseData caseData)
    {
        int target = GetNormalSkinTargetCount(caseData);

        if (HasRareSpecialTarget(caseData))
            target++;

        return target;
    }

    public int GetNormalSkinTargetCount(CaseData caseData)
    {
        return GetNormalSkinTargetKeys(caseData).Count;
    }

    public int GetBestWearCount(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);

        return progress == null
            ? 0
            : CountMatchingNormalTargets(
                caseData,
                progress.bestWearSkinKeys);
    }

    public int GetVariantCount(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);

        return progress == null
            ? 0
            : CountMatchingNormalTargets(
                caseData,
                progress.variantSkinKeys);
    }

    public int GetBestWearVariantCount(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);

        return progress == null
            ? 0
            : CountMatchingNormalTargets(
                caseData,
                progress.bestWearVariantSkinKeys);
    }

    public ContainerCompletionTier GetCompletionTier(CaseData caseData)
    {
        if (IsDiamondComplete(caseData))
            return ContainerCompletionTier.Diamond;

        if (IsGoldComplete(caseData))
            return ContainerCompletionTier.Gold;

        if (IsSilverComplete(caseData))
            return ContainerCompletionTier.Silver;

        if (IsBronzeComplete(caseData))
            return ContainerCompletionTier.Bronze;

        return ContainerCompletionTier.None;
    }

    public bool IsTierComplete(
        CaseData caseData,
        ContainerCompletionTier tier)
    {
        switch (tier)
        {
            case ContainerCompletionTier.Bronze:
                return IsBronzeComplete(caseData);
            case ContainerCompletionTier.Silver:
                return IsSilverComplete(caseData);
            case ContainerCompletionTier.Gold:
                return IsGoldComplete(caseData);
            case ContainerCompletionTier.Diamond:
                return IsDiamondComplete(caseData);
            default:
                return false;
        }
    }

    public bool IsBronzeComplete(CaseData caseData)
    {
        int normalTarget = GetNormalSkinTargetCount(caseData);

        if (normalTarget <= 0)
            return false;

        ContainerProgressData progress = GetProgress(caseData);

        if (progress == null)
            return false;

        bool allNormalFound =
            CountMatchingNormalTargets(
                caseData,
                progress.foundSkinKeys) >= normalTarget;

        bool rareSpecialComplete =
            !HasRareSpecialTarget(caseData) || progress.foundRareSpecial;

        return allNormalFound && rareSpecialComplete;
    }

    public bool IsSilverComplete(CaseData caseData)
    {
        int target = GetNormalSkinTargetCount(caseData);
        return target > 0 && GetBestWearCount(caseData) >= target;
    }

    public bool IsGoldComplete(CaseData caseData)
    {
        if (GetVariantRequirement(caseData) ==
            ContainerVariantRequirement.None)
        {
            return false;
        }

        int target = GetNormalSkinTargetCount(caseData);
        return target > 0 && GetVariantCount(caseData) >= target;
    }

    public bool IsDiamondComplete(CaseData caseData)
    {
        if (GetVariantRequirement(caseData) ==
            ContainerVariantRequirement.None)
        {
            return false;
        }

        int target = GetNormalSkinTargetCount(caseData);
        return target > 0 && GetBestWearVariantCount(caseData) >= target;
    }

    public string GetCompletionDisplayText(CaseData caseData)
    {
        switch (GetCompletionTier(caseData))
        {
            case ContainerCompletionTier.Diamond:
                return "Diamond Completion";
            case ContainerCompletionTier.Gold:
                return "Gold Completion";
            case ContainerCompletionTier.Silver:
                return "Silver Completion";
            case ContainerCompletionTier.Bronze:
                return "Bronze Completion";
            default:
                return $"Found {GetFoundCount(caseData)} / {GetTargetCount(caseData)}";
        }
    }

    public string GetFoundDisplayText(CaseData caseData)
    {
        return GetCompletionDisplayText(caseData);
    }

    public ContainerVariantRequirement GetVariantRequirement(CaseData caseData)
    {
        if (caseData == null)
            return ContainerVariantRequirement.None;

        if (caseData.forceSouvenirDrops ||
            caseData.containerType == CaseContainerType.SouvenirPackage)
        {
            return ContainerVariantRequirement.Souvenir;
        }

        // Normal collection packages cannot generate StatTrak items.
        if (caseData.containerType == CaseContainerType.CollectionPackage)
            return ContainerVariantRequirement.None;

        bool statTrakContainer =
            caseData.containerType == CaseContainerType.WeaponCase ||
            caseData.containerType == CaseContainerType.CustomCase;

        if (statTrakContainer && caseData.allowStatTrak)
            return ContainerVariantRequirement.StatTrak;

        return ContainerVariantRequirement.None;
    }

    public string GetVariantDisplayName(CaseData caseData)
    {
        switch (GetVariantRequirement(caseData))
        {
            case ContainerVariantRequirement.Souvenir:
                return "Souvenir";
            case ContainerVariantRequirement.StatTrak:
                return "StatTrak";
            default:
                return "Unavailable";
        }
    }

    public bool HasFoundSkin(CaseData caseData, SkinData skin)
    {
        if (caseData == null || skin == null)
            return false;

        ContainerProgressData progress = GetProgress(caseData);

        if (progress == null)
            return false;

        EnsureProgressLists(progress);
        string skinKey = GetSkinKey(skin);

        if (skin.rarity == Rarity.RareSpecial)
            return progress.foundRareSpecialSkinKeys.Contains(skinKey);

        return progress.foundSkinKeys.Contains(skinKey);
    }

    public bool HasFoundRareSpecial(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);
        return progress != null && progress.foundRareSpecial;
    }

    public bool IsRewardClaimed(
        CaseData caseData,
        ContainerCompletionTier tier)
    {
        ContainerProgressData progress = GetProgress(caseData);

        if (progress == null)
            return false;

        switch (tier)
        {
            case ContainerCompletionTier.Bronze:
                return progress.bronzeRewardClaimed;
            case ContainerCompletionTier.Silver:
                return progress.silverRewardClaimed;
            case ContainerCompletionTier.Gold:
                return progress.goldRewardClaimed;
            case ContainerCompletionTier.Diamond:
                return progress.diamondRewardClaimed;
            default:
                return false;
        }
    }

    public bool IsRewardImplemented(ContainerCompletionTier tier)
    {
        return tier == ContainerCompletionTier.Bronze ||
               tier == ContainerCompletionTier.Silver;
    }

    public bool CanClaimReward(
        CaseData caseData,
        ContainerCompletionTier tier)
    {
        return IsRewardImplemented(tier) &&
               IsTierComplete(caseData, tier) &&
               !IsRewardClaimed(caseData, tier);
    }

    public bool ClaimReward(
        CaseData caseData,
        ContainerCompletionTier tier)
    {
        if (!CanClaimReward(caseData, tier))
            return false;

        if (CaseInventoryManager.Instance == null)
        {
            Debug.LogWarning(
                "ContainerProgressManager: Cannot claim reward because " +
                "CaseInventoryManager is missing.");
            return false;
        }

        ContainerProgressData progress = GetProgress(caseData);

        if (progress == null)
            return false;

        int containerReward;

        switch (tier)
        {
            case ContainerCompletionTier.Bronze:
                containerReward = 10;
                progress.bronzeRewardClaimed = true;
                break;
            case ContainerCompletionTier.Silver:
                containerReward = 20;
                progress.silverRewardClaimed = true;
                break;
            default:
                return false;
        }

        CaseInventoryManager.Instance.AddCases(caseData, containerReward);
        SaveProgress(false);

        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveGame();

        Debug.Log(
            $"Claimed {tier} completion reward: " +
            $"{containerReward}x {caseData.caseName}.");

        return true;
    }

    public void ResetAllProgressForTesting()
    {
        ReplaceProgress(new ContainerProgressSaveData(), false);

        if (PlayerPrefs.HasKey(LegacySaveKey))
        {
            PlayerPrefs.DeleteKey(LegacySaveKey);
            PlayerPrefs.Save();
        }

        legacyProgressReadyForDeletion = false;
        SaveProgress(true);
        Debug.Log("ContainerProgressManager: Reset all container progress.");
    }

    private bool HasRequiredVariant(
        CaseData caseData,
        InventoryItem item)
    {
        if (item == null)
            return false;

        switch (GetVariantRequirement(caseData))
        {
            case ContainerVariantRequirement.Souvenir:
                return item.souvenir;
            case ContainerVariantRequirement.StatTrak:
                return item.statTrak && !item.souvenir;
            default:
                return false;
        }
    }

    private bool IsBestPossibleWear(InventoryItem item)
    {
        if (item == null || item.skin == null)
            return false;

        if (item.isVanilla || item.skin.isVanilla)
            return true;

        int bestPossibleWear = WearUtility.GetWearIndex(item.skin.minFloat);
        int openedWear = WearUtility.GetWearIndex((float)item.floatValue);
        return openedWear == bestPossibleWear;
    }

    private int CountMatchingNormalTargets(
        CaseData caseData,
        List<string> completedKeys)
    {
        if (completedKeys == null || completedKeys.Count == 0)
            return 0;

        HashSet<string> targetKeys = GetNormalSkinTargetKeys(caseData);
        int count = 0;

        foreach (string key in targetKeys)
        {
            if (completedKeys.Contains(key))
                count++;
        }

        return count;
    }

    private HashSet<string> GetNormalSkinTargetKeys(CaseData caseData)
    {
        HashSet<string> keys = new HashSet<string>();

        if (caseData == null || caseData.dropPool == null)
            return keys;

        foreach (WeightedDrop drop in caseData.dropPool)
        {
            if (drop == null ||
                drop.skin == null ||
                drop.skin.rarity == Rarity.RareSpecial)
            {
                continue;
            }

            keys.Add(GetSkinKey(drop.skin));
        }

        return keys;
    }

    private bool HasRareSpecialTarget(CaseData caseData)
    {
        if (caseData == null || caseData.dropPool == null)
            return false;

        foreach (WeightedDrop drop in caseData.dropPool)
        {
            if (drop != null &&
                drop.skin != null &&
                drop.skin.rarity == Rarity.RareSpecial)
            {
                return true;
            }
        }

        return false;
    }

    private static ContainerProgressData CloneProgress(
        ContainerProgressData source)
    {
        EnsureProgressLists(source);

        return new ContainerProgressData
        {
            containerId = source.containerId,
            openedCount = source.openedCount,
            totalSpent = source.totalSpent,
            totalValuePulled = source.totalValuePulled,
            foundRareSpecial = source.foundRareSpecial,
            foundSkinKeys = new List<string>(source.foundSkinKeys),
            foundRareSpecialSkinKeys =
                new List<string>(source.foundRareSpecialSkinKeys),
            bestWearSkinKeys = new List<string>(source.bestWearSkinKeys),
            variantSkinKeys = new List<string>(source.variantSkinKeys),
            bestWearVariantSkinKeys =
                new List<string>(source.bestWearVariantSkinKeys),
            bronzeRewardClaimed = source.bronzeRewardClaimed,
            silverRewardClaimed = source.silverRewardClaimed,
            goldRewardClaimed = source.goldRewardClaimed,
            diamondRewardClaimed = source.diamondRewardClaimed
        };
    }

    private static void AddUnique(List<string> list, string value)
    {
        if (list == null || string.IsNullOrWhiteSpace(value))
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

    private static void EnsureProgressLists(ContainerProgressData progress)
    {
        if (progress == null)
            return;

        if (progress.foundSkinKeys == null)
            progress.foundSkinKeys = new List<string>();

        if (progress.foundRareSpecialSkinKeys == null)
        {
            progress.foundRareSpecialSkinKeys =
                new List<string>();
        }

        if (progress.bestWearSkinKeys == null)
            progress.bestWearSkinKeys = new List<string>();

        if (progress.variantSkinKeys == null)
            progress.variantSkinKeys = new List<string>();

        if (progress.bestWearVariantSkinKeys == null)
        {
            progress.bestWearVariantSkinKeys =
                new List<string>();
        }
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
