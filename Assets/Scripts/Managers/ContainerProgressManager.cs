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

public enum ContainerItemVariant
{
    Normal,
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
        if (string.IsNullOrWhiteSpace(key))
            return null;

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
        if (progress == null)
            return;

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
            AddUnique(progress.foundSkinKeys, skinKey);

            int wearIndex = GetOpenedWearIndex(pulledItem);
            RecordBestFoundWear(progress, skinKey, wearIndex);
            RecordPriceDiscovery(progress, skinKey, wearIndex, GetItemVariant(pulledItem));

            bool bestWear = IsBestPossibleWear(pulledItem);
            bool correctVariant = HasRequiredVariant(caseData, pulledItem);
            bool topQuarterFloat = IsTopQuarterFloat(pulledItem);
            bool topQuarterStatTrak =
                topQuarterFloat && pulledItem.statTrak && !pulledItem.souvenir;

            if (bestWear)
                AddUnique(progress.bestWearSkinKeys, skinKey);

            if (correctVariant)
                AddUnique(progress.variantSkinKeys, skinKey);

            if (bestWear && correctVariant)
                AddUnique(progress.bestWearVariantSkinKeys, skinKey);

            if (topQuarterFloat)
                AddUnique(progress.topQuarterFloatSkinKeys, skinKey);

            if (topQuarterStatTrak)
                AddUnique(progress.topQuarterFloatStatTrakSkinKeys, skinKey);
        }

        if (saveImmediately)
        {
            SaveProgress();

            if (SaveManager.Instance != null)
                SaveManager.Instance.SaveGame();
        }
    }

    // Compatibility overload for older callers. Exact wear, float-band and
    // variant discovery requires the InventoryItem overload above.
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

        if (copy == null)
            copy = new ContainerProgressSaveData();

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
        if (!PlayerPrefs.HasKey(LegacySaveKey))
            return;

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
        return progress != null
            ? progress.totalValuePulled - progress.totalSpent
            : 0f;
    }

    public int GetFoundCount(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);
        if (progress == null)
            return 0;

        int found = CountMatchingNormalTargets(caseData, progress.foundSkinKeys);

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
            : CountMatchingNormalTargets(caseData, progress.bestWearSkinKeys);
    }

    // Kept for compatibility with older UI and data. Gold no longer uses this.
    public int GetVariantCount(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);

        return progress == null
            ? 0
            : CountMatchingNormalTargets(caseData, progress.variantSkinKeys);
    }

    // Kept for compatibility with older UI and data. Diamond no longer uses this.
    public int GetBestWearVariantCount(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);

        return progress == null
            ? 0
            : CountMatchingNormalTargets(caseData, progress.bestWearVariantSkinKeys);
    }

    public int GetTopQuarterFloatCount(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);

        return progress == null
            ? 0
            : CountMatchingNormalTargets(caseData, progress.topQuarterFloatSkinKeys);
    }

    public int GetTopQuarterFloatStatTrakCount(CaseData caseData)
    {
        ContainerProgressData progress = GetProgress(caseData);

        return progress == null
            ? 0
            : CountMatchingNormalTargets(
                caseData,
                progress.topQuarterFloatStatTrakSkinKeys);
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

    public bool IsTierComplete(CaseData caseData, ContainerCompletionTier tier)
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
        int target = GetNormalSkinTargetCount(caseData);
        if (target <= 0)
            return false;

        ContainerProgressData progress = GetProgress(caseData);
        if (progress == null)
            return false;

        bool normalComplete =
            CountMatchingNormalTargets(caseData, progress.foundSkinKeys) >= target;

        bool rareComplete =
            !HasRareSpecialTarget(caseData) || progress.foundRareSpecial;

        return normalComplete && rareComplete;
    }

    public bool IsSilverComplete(CaseData caseData)
    {
        int target = GetNormalSkinTargetCount(caseData);

        return target > 0 &&
               IsBronzeComplete(caseData) &&
               GetBestWearCount(caseData) >= target;
    }

    public bool IsGoldComplete(CaseData caseData)
    {
        int target = GetNormalSkinTargetCount(caseData);

        return target > 0 &&
               IsSilverComplete(caseData) &&
               GetTopQuarterFloatCount(caseData) >= target;
    }

    public bool IsDiamondComplete(CaseData caseData)
    {
        if (!CanCompleteDiamond(caseData))
            return false;

        int target = GetNormalSkinTargetCount(caseData);

        return target > 0 &&
               IsGoldComplete(caseData) &&
               GetTopQuarterFloatStatTrakCount(caseData) >= target;
    }

    public bool CanCompleteDiamond(CaseData caseData)
    {
        if (caseData == null)
            return false;

        if (caseData.containerType == CaseContainerType.CollectionPackage ||
            caseData.containerType == CaseContainerType.SouvenirPackage ||
            caseData.forceSouvenirDrops)
        {
            return false;
        }

        return caseData.allowStatTrak;
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

    public bool HasFoundBestWear(CaseData caseData, SkinData skin)
    {
        if (caseData == null || skin == null)
            return false;

        ContainerProgressData progress = GetProgress(caseData);
        if (progress == null)
            return false;

        EnsureProgressLists(progress);
        return progress.bestWearSkinKeys.Contains(GetSkinKey(skin));
    }

    public bool HasFoundTopQuarterFloat(CaseData caseData, SkinData skin)
    {
        if (caseData == null || skin == null)
            return false;

        ContainerProgressData progress = GetProgress(caseData);
        if (progress == null)
            return false;

        EnsureProgressLists(progress);
        return progress.topQuarterFloatSkinKeys.Contains(GetSkinKey(skin));
    }

    public bool HasFoundTopQuarterFloatStatTrak(CaseData caseData, SkinData skin)
    {
        if (caseData == null || skin == null)
            return false;

        ContainerProgressData progress = GetProgress(caseData);
        if (progress == null)
            return false;

        EnsureProgressLists(progress);
        return progress.topQuarterFloatStatTrakSkinKeys.Contains(GetSkinKey(skin));
    }

    public int GetBestFoundWearIndex(CaseData caseData, SkinData skin)
    {
        if (caseData == null || skin == null)
            return -1;

        ContainerProgressData progress = GetProgress(caseData);
        if (progress == null)
            return -1;

        EnsureProgressLists(progress);
        string skinKey = GetSkinKey(skin);

        for (int i = 0; i < progress.bestFoundWearBySkin.Count; i++)
        {
            ContainerSkinWearProgress entry = progress.bestFoundWearBySkin[i];

            if (entry != null &&
                string.Equals(entry.skinKey, skinKey, StringComparison.Ordinal))
            {
                return Mathf.Clamp(entry.bestWearIndex, 0, 4);
            }
        }

        // Additive migration fallback: old saves know when the best possible
        // wear was found, even though they did not store the exact wear index.
        if (progress.bestWearSkinKeys.Contains(skinKey))
            return GetBestPossibleWearIndex(skin);

        return -1;
    }

    public string GetBestFoundWearDisplayName(CaseData caseData, SkinData skin)
    {
        int wearIndex = GetBestFoundWearIndex(caseData, skin);
        return wearIndex >= 0 ? GetWearDisplayName(wearIndex) : "Unknown";
    }

    public bool HasDiscoveredPrice(
        CaseData caseData,
        SkinData skin,
        int wearIndex,
        ContainerItemVariant variant)
    {
        if (caseData == null || skin == null)
            return false;

        ContainerProgressData progress = GetProgress(caseData);
        if (progress == null)
            return false;

        EnsureProgressLists(progress);
        string skinKey = GetSkinKey(skin);
        int clampedWear = Mathf.Clamp(wearIndex, 0, 4);
        string discoveryKey = BuildPriceDiscoveryKey(
            skinKey,
            clampedWear,
            variant);

        if (progress.discoveredPriceKeys.Contains(discoveryKey))
            return true;

        // Limited migration fallback for old saves: a best-wear discovery can
        // be reconstructed, but arbitrary historic wear rows cannot.
        if (clampedWear != GetBestPossibleWearIndex(skin))
            return false;

        if (variant == ContainerItemVariant.Normal)
            return progress.bestWearSkinKeys.Contains(skinKey);

        return progress.bestWearVariantSkinKeys.Contains(skinKey);
    }

    public static float GetTopQuarterFloatThreshold(SkinData skin)
    {
        if (skin == null)
            return 0f;

        float min = Mathf.Min(skin.minFloat, skin.maxFloat);
        float max = Mathf.Max(skin.minFloat, skin.maxFloat);
        return min + (max - min) * 0.25f;
    }

    public static int GetBestPossibleWearIndex(SkinData skin)
    {
        if (skin == null || skin.isVanilla)
            return 0;

        return WearUtility.GetWearIndex(skin.minFloat);
    }

    public static string GetWearDisplayName(int wearIndex)
    {
        switch (Mathf.Clamp(wearIndex, 0, 4))
        {
            case 0: return "Factory New";
            case 1: return "Minimal Wear";
            case 2: return "Field-Tested";
            case 3: return "Well-Worn";
            default: return "Battle-Scarred";
        }
    }

    public bool IsRewardClaimed(CaseData caseData, ContainerCompletionTier tier)
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

    public bool CanClaimReward(CaseData caseData, ContainerCompletionTier tier)
    {
        return IsRewardImplemented(tier) &&
               IsTierComplete(caseData, tier) &&
               !IsRewardClaimed(caseData, tier);
    }

    public bool ClaimReward(CaseData caseData, ContainerCompletionTier tier)
    {
        if (!CanClaimReward(caseData, tier))
            return false;

        if (CaseInventoryManager.Instance == null)
        {
            Debug.LogWarning(
                "ContainerProgressManager: Cannot claim reward because CaseInventoryManager is missing.");
            return false;
        }

        ContainerProgressData progress = GetProgress(caseData);
        if (progress == null)
            return false;

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

        Debug.Log(
            $"Claimed {tier} completion reward: {reward}x {caseData.caseName}.");

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

    private static ContainerItemVariant GetItemVariant(InventoryItem item)
    {
        if (item != null && item.souvenir)
            return ContainerItemVariant.Souvenir;

        if (item != null && item.statTrak)
            return ContainerItemVariant.StatTrak;

        return ContainerItemVariant.Normal;
    }

    private bool HasRequiredVariant(CaseData caseData, InventoryItem item)
    {
        if (item == null)
            return false;

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

    private static bool IsBestPossibleWear(InventoryItem item)
    {
        if (item == null || item.skin == null)
            return false;

        if (item.isVanilla || item.skin.isVanilla)
            return true;

        int bestWear = GetBestPossibleWearIndex(item.skin);
        int openedWear = WearUtility.GetWearIndex((float)item.floatValue);
        return openedWear == bestWear;
    }

    private static bool IsTopQuarterFloat(InventoryItem item)
    {
        if (item == null || item.skin == null)
            return false;

        if (item.isVanilla || item.skin.isVanilla)
            return true;

        double threshold = GetTopQuarterFloatThreshold(item.skin);
        return item.floatValue <= threshold + 0.0000001d;
    }

    private static int GetOpenedWearIndex(InventoryItem item)
    {
        if (item == null || item.skin == null)
            return 4;

        if (item.isVanilla || item.skin.isVanilla)
            return 0;

        return Mathf.Clamp(
            WearUtility.GetWearIndex((float)item.floatValue),
            0,
            4);
    }

    private static void RecordBestFoundWear(
        ContainerProgressData progress,
        string skinKey,
        int wearIndex)
    {
        if (progress == null || string.IsNullOrWhiteSpace(skinKey))
            return;

        EnsureProgressLists(progress);
        int clampedWear = Mathf.Clamp(wearIndex, 0, 4);

        for (int i = 0; i < progress.bestFoundWearBySkin.Count; i++)
        {
            ContainerSkinWearProgress entry = progress.bestFoundWearBySkin[i];

            if (entry == null ||
                !string.Equals(entry.skinKey, skinKey, StringComparison.Ordinal))
            {
                continue;
            }

            entry.bestWearIndex = Mathf.Min(entry.bestWearIndex, clampedWear);
            return;
        }

        progress.bestFoundWearBySkin.Add(
            new ContainerSkinWearProgress
            {
                skinKey = skinKey,
                bestWearIndex = clampedWear
            });
    }

    private static void RecordPriceDiscovery(
        ContainerProgressData progress,
        string skinKey,
        int wearIndex,
        ContainerItemVariant variant)
    {
        if (progress == null || string.IsNullOrWhiteSpace(skinKey))
            return;

        EnsureProgressLists(progress);
        AddUnique(
            progress.discoveredPriceKeys,
            BuildPriceDiscoveryKey(skinKey, wearIndex, variant));
    }

    private static string BuildPriceDiscoveryKey(
        string skinKey,
        int wearIndex,
        ContainerItemVariant variant)
    {
        return $"{skinKey}::wear={Mathf.Clamp(wearIndex, 0, 4)}::variant={(int)variant}";
    }

    private int CountMatchingNormalTargets(
        CaseData caseData,
        List<string> completedKeys)
    {
        if (completedKeys == null || completedKeys.Count == 0)
            return 0;

        int count = 0;

        foreach (string key in GetNormalSkinTargetKeys(caseData))
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
        if (saveData == null)
            saveData = new ContainerProgressSaveData();

        if (saveData.progressEntries == null)
            saveData.progressEntries = new List<ContainerProgressData>();
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

        return !string.IsNullOrWhiteSpace(caseData.apiId)
            ? caseData.apiId
            : caseData.caseName;
    }

    private static string GetSkinKey(SkinData skin)
    {
        if (skin == null)
            return "";

        return !string.IsNullOrWhiteSpace(skin.apiId)
            ? skin.apiId
            : $"{skin.weaponName}|{skin.skinName}|{skin.rarity}";
    }

    private static void EnsureProgressLists(ContainerProgressData progress)
    {
        if (progress == null)
            return;

        if (progress.foundSkinKeys == null)
            progress.foundSkinKeys = new List<string>();

        if (progress.foundRareSpecialSkinKeys == null)
            progress.foundRareSpecialSkinKeys = new List<string>();

        if (progress.bestWearSkinKeys == null)
            progress.bestWearSkinKeys = new List<string>();

        if (progress.variantSkinKeys == null)
            progress.variantSkinKeys = new List<string>();

        if (progress.bestWearVariantSkinKeys == null)
            progress.bestWearVariantSkinKeys = new List<string>();

        if (progress.topQuarterFloatSkinKeys == null)
            progress.topQuarterFloatSkinKeys = new List<string>();

        if (progress.topQuarterFloatStatTrakSkinKeys == null)
            progress.topQuarterFloatStatTrakSkinKeys = new List<string>();

        if (progress.discoveredPriceKeys == null)
            progress.discoveredPriceKeys = new List<string>();

        if (progress.bestFoundWearBySkin == null)
            progress.bestFoundWearBySkin = new List<ContainerSkinWearProgress>();
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

    // Legacy variant completion fields are retained for additive save support.
    public List<string> variantSkinKeys = new List<string>();
    public List<string> bestWearVariantSkinKeys = new List<string>();

    // New completion and item-info discovery state.
    public List<string> topQuarterFloatSkinKeys = new List<string>();
    public List<string> topQuarterFloatStatTrakSkinKeys = new List<string>();
    public List<string> discoveredPriceKeys = new List<string>();
    public List<ContainerSkinWearProgress> bestFoundWearBySkin =
        new List<ContainerSkinWearProgress>();

    public bool bronzeRewardClaimed;
    public bool silverRewardClaimed;
    public bool goldRewardClaimed;
    public bool diamondRewardClaimed;
}

[Serializable]
public class ContainerSkinWearProgress
{
    public string skinKey;
    [Range(0, 4)] public int bestWearIndex = 4;
}
