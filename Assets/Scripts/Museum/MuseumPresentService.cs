using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase M4.5 authority for Museum Present fragments, assembled presents and
/// opening rewards. It also grants fragment/present payloads from already
/// claimed milestones exactly once, making the feature retroactive-safe.
/// </summary>
public class MuseumPresentService : MonoBehaviour
{
    public static MuseumPresentService Instance { get; private set; }

    [SerializeField] private MuseumPresentConfig config;
    [SerializeField] private bool persistBetweenScenes = true;
    [SerializeField] private bool verboseLogging;

    public event Action OnPresentStateChanged;
    public event Action<MuseumPresentGrantSummary> OnMilestonePresentRewardGranted;

    private MuseumMilestoneService milestoneService;
    private bool subscribed;
    private bool initialized;

    public static MuseumPresentService GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        MuseumPresentService existing =
            FindFirstObjectByType<MuseumPresentService>();

        if (existing != null)
            return existing;

        GameObject go = new GameObject("MuseumPresentService");
        return go.AddComponent<MuseumPresentService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistBetweenScenes)
            DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        TryInitialize();
    }

    private void Update()
    {
        if (!initialized)
            TryInitialize();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (Instance == this)
            Instance = null;
    }

    public MuseumPresentTierConfig GetTierConfig(MuseumPresentTier tier)
    {
        ResolveConfig();

        return config != null
            ? config.GetTier(tier)
            : MuseumPresentConfig.CreateFallbackTier(tier);
    }

    public int GetFragments(MuseumPresentTier tier)
    {
        MuseumPresentTierBalanceSaveData balance =
            GetBalance(tier, false);

        return balance != null ? Mathf.Max(0, balance.fragments) : 0;
    }

    public int GetPresents(MuseumPresentTier tier)
    {
        MuseumPresentTierBalanceSaveData balance =
            GetBalance(tier, false);

        return balance != null ? Mathf.Max(0, balance.presents) : 0;
    }

    public int GetPresentsOpened(MuseumPresentTier tier)
    {
        MuseumPresentTierBalanceSaveData balance =
            GetBalance(tier, false);

        return balance != null ? Mathf.Max(0, balance.presentsOpened) : 0;
    }

    public int GetFragmentsPerPresent(MuseumPresentTier tier)
    {
        return Mathf.Max(1, GetTierConfig(tier).fragmentsPerPresent);
    }

    public bool CanAssemble(MuseumPresentTier tier)
    {
        return GetFragments(tier) >= GetFragmentsPerPresent(tier);
    }

    public void AddFragments(
        MuseumPresentTier tier,
        int amount,
        bool notify = true)
    {
        if (amount <= 0 || !EnsureState())
            return;

        MuseumPresentTierBalanceSaveData balance =
            GetBalance(tier, true);

        balance.fragments += amount;
        SaveManager.Instance.MarkDirty();

        if (notify)
            OnPresentStateChanged?.Invoke();
    }

    public void AddPresents(
        MuseumPresentTier tier,
        int amount,
        bool notify = true)
    {
        if (amount <= 0 || !EnsureState())
            return;

        MuseumPresentTierBalanceSaveData balance =
            GetBalance(tier, true);

        balance.presents += amount;
        SaveManager.Instance.MarkDirty();

        if (notify)
            OnPresentStateChanged?.Invoke();
    }

    public bool AssembleOne(
        MuseumPresentTier tier,
        out string message)
    {
        message = "";

        if (!EnsureState())
        {
            message = "Museum Present save data is unavailable.";
            return false;
        }

        MuseumPresentTierBalanceSaveData balance =
            GetBalance(tier, true);
        int cost = GetFragmentsPerPresent(tier);

        if (balance.fragments < cost)
        {
            message =
                $"Requires {cost:N0} {MuseumPresentUtility.GetTierDisplayName(tier)} fragments.";
            return false;
        }

        balance.fragments -= cost;
        balance.presents++;
        SaveManager.Instance.MarkDirty();
        OnPresentStateChanged?.Invoke();

        message =
            $"Assembled 1 {MuseumPresentUtility.GetTierDisplayName(tier)} Present.";
        return true;
    }

    public int AssembleMaximum(MuseumPresentTier tier)
    {
        if (!EnsureState())
            return 0;

        MuseumPresentTierBalanceSaveData balance =
            GetBalance(tier, true);
        int cost = GetFragmentsPerPresent(tier);
        int amount = balance.fragments / cost;

        if (amount <= 0)
            return 0;

        balance.fragments -= amount * cost;
        balance.presents += amount;
        SaveManager.Instance.MarkDirty();
        OnPresentStateChanged?.Invoke();
        return amount;
    }

    public MuseumPresentOpenResult OpenPresent(MuseumPresentTier tier)
    {
        if (!EnsureState())
        {
            return MuseumPresentOpenResult.Failed(
                tier,
                "Museum Present save data is unavailable.");
        }

        MuseumPresentTierBalanceSaveData balance =
            GetBalance(tier, true);

        if (balance.presents <= 0)
        {
            return MuseumPresentOpenResult.Failed(
                tier,
                $"You do not own a {MuseumPresentUtility.GetTierDisplayName(tier)} Present.");
        }

        MuseumPresentTierConfig tierConfig = GetTierConfig(tier);
        float gold = RandomRange(
            tierConfig.minimumGold,
            tierConfig.maximumGold);
        int xp = RandomRange(
            tierConfig.minimumXP,
            tierConfig.maximumXP);
        int diamonds = RandomRange(
            tierConfig.minimumDiamonds,
            tierConfig.maximumDiamonds);

        balance.presents--;
        balance.presentsOpened++;
        SaveManager.Instance.Museum.presents.totalPresentsOpened++;

        if (gold > 0f)
            SaveManager.Instance.AddGold(gold);

        if (xp > 0)
            SaveManager.Instance.AddXP(xp);

        if (diamonds > 0)
            SaveManager.Instance.AddDiamonds(diamonds);

        SaveManager.Instance.MarkDirty();
        OnPresentStateChanged?.Invoke();

        MuseumPresentOpenResult result = new MuseumPresentOpenResult
        {
            success = true,
            tier = tier,
            gold = gold,
            xp = xp,
            diamonds = diamonds,
            remainingPresents = balance.presents,
            message = BuildOpenMessage(tier, gold, xp, diamonds)
        };

        if (verboseLogging)
            Debug.Log(result.message, this);

        return result;
    }

    public void ProcessClaimedMilestoneRewards()
    {
        if (!EnsureState())
            return;

        GameDatabase database = SaveManager.Instance.database;

        if (database == null || database.museumMilestones == null)
            return;

        MuseumPresentStateSaveData state =
            SaveManager.Instance.Museum.presents;
        HashSet<string> claimed = BuildIdSet(
            SaveManager.Instance.Museum.claimedMilestoneIds);
        HashSet<string> processed = BuildIdSet(
            state.processedMilestoneRewardIds);

        bool changed = false;

        for (int i = 0; i < database.museumMilestones.Count; i++)
        {
            MuseumMilestoneData milestone = database.museumMilestones[i];

            if (milestone == null ||
                string.IsNullOrWhiteSpace(milestone.milestoneId) ||
                !claimed.Contains(milestone.milestoneId) ||
                processed.Contains(milestone.milestoneId))
            {
                continue;
            }

            MuseumRewardData reward = milestone.reward;

            if (reward == null || reward.presentRewards == null)
                continue;

            MuseumPresentGrantSummary summary =
                new MuseumPresentGrantSummary
                {
                    milestoneId = milestone.milestoneId
                };

            for (int rewardIndex = 0;
                 rewardIndex < reward.presentRewards.Count;
                 rewardIndex++)
            {
                MuseumPresentRewardEntry entry =
                    reward.presentRewards[rewardIndex];

                if (entry == null || !entry.HasReward)
                    continue;

                MuseumPresentTierBalanceSaveData balance =
                    GetBalance(entry.tier, true);

                if (entry.fragments > 0)
                {
                    balance.fragments += entry.fragments;
                    summary.rewardLines.Add(
                        $"+{entry.fragments:N0} " +
                        $"{MuseumPresentUtility.GetTierDisplayName(entry.tier)} fragments");
                }

                if (entry.presents > 0)
                {
                    balance.presents += entry.presents;
                    summary.rewardLines.Add(
                        $"+{entry.presents:N0} " +
                        $"{MuseumPresentUtility.GetTierDisplayName(entry.tier)} Present" +
                        (entry.presents == 1 ? "" : "s"));
                }
            }

            if (!summary.HasRewards)
                continue;

            AddUnique(
                state.processedMilestoneRewardIds,
                milestone.milestoneId);
            processed.Add(milestone.milestoneId);
            changed = true;
            OnMilestonePresentRewardGranted?.Invoke(summary);

            if (verboseLogging)
            {
                Debug.Log(
                    $"Granted Museum Present rewards for {milestone.milestoneId}: " +
                    string.Join(", ", summary.rewardLines),
                    this);
            }
        }

        if (!changed)
            return;

        SaveManager.Instance.MarkDirty();
        OnPresentStateChanged?.Invoke();
    }

    private void TryInitialize()
    {
        if (initialized || SaveManager.Instance == null)
            return;

        ResolveConfig();
        EnsureState();

        milestoneService = MuseumMilestoneService.GetOrCreate();
        Subscribe();
        initialized = true;
        ProcessClaimedMilestoneRewards();
    }

    private void ResolveConfig()
    {
        if (config == null &&
            SaveManager.Instance != null &&
            SaveManager.Instance.database != null)
        {
            config = SaveManager.Instance.database.museumPresentConfig;
        }
    }

    private bool EnsureState()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Museum == null)
            return false;

        MuseumStateSaveData museum = SaveManager.Instance.Museum;

        if (museum.presents == null)
            museum.presents = new MuseumPresentStateSaveData();

        if (museum.presents.tierBalances == null)
        {
            museum.presents.tierBalances =
                new List<MuseumPresentTierBalanceSaveData>();
        }

        if (museum.presents.processedMilestoneRewardIds == null)
        {
            museum.presents.processedMilestoneRewardIds =
                new List<string>();
        }

        museum.presents.totalPresentsOpened =
            Mathf.Max(0, museum.presents.totalPresentsOpened);

        for (int i = 0; i < museum.presents.tierBalances.Count; i++)
        {
            MuseumPresentTierBalanceSaveData balance =
                museum.presents.tierBalances[i];

            if (balance == null)
                continue;

            balance.fragments = Mathf.Max(0, balance.fragments);
            balance.presents = Mathf.Max(0, balance.presents);
            balance.presentsOpened = Mathf.Max(0, balance.presentsOpened);
        }

        return true;
    }

    private MuseumPresentTierBalanceSaveData GetBalance(
        MuseumPresentTier tier,
        bool create)
    {
        if (!EnsureState())
            return null;

        List<MuseumPresentTierBalanceSaveData> balances =
            SaveManager.Instance.Museum.presents.tierBalances;

        for (int i = 0; i < balances.Count; i++)
        {
            MuseumPresentTierBalanceSaveData balance = balances[i];

            if (balance != null && balance.tier == tier)
                return balance;
        }

        if (!create)
            return null;

        MuseumPresentTierBalanceSaveData created =
            new MuseumPresentTierBalanceSaveData
            {
                tier = tier
            };

        balances.Add(created);
        return created;
    }

    private void HandleMilestonesChanged()
    {
        ProcessClaimedMilestoneRewards();
    }

    private void Subscribe()
    {
        if (milestoneService == null || subscribed)
            return;

        milestoneService.OnMilestonesChanged += HandleMilestonesChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (milestoneService != null && subscribed)
            milestoneService.OnMilestonesChanged -= HandleMilestonesChanged;

        subscribed = false;
    }

    private static HashSet<string> BuildIdSet(List<string> source)
    {
        HashSet<string> result =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (source == null)
            return result;

        for (int i = 0; i < source.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(source[i]))
                result.Add(source[i].Trim());
        }

        return result;
    }

    private static void AddUnique(List<string> list, string value)
    {
        if (list == null || string.IsNullOrWhiteSpace(value))
            return;

        string normalized = value.Trim();

        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(
                    list[i],
                    normalized,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        list.Add(normalized);
    }

    private static float RandomRange(float minimum, float maximum)
    {
        if (maximum <= minimum)
            return Mathf.Max(0f, minimum);

        return Mathf.Round(
            UnityEngine.Random.Range(minimum, maximum) * 100f) / 100f;
    }

    private static int RandomRange(int minimum, int maximum)
    {
        int min = Mathf.Max(0, minimum);
        int max = Mathf.Max(min, maximum);

        return max <= min
            ? min
            : UnityEngine.Random.Range(min, max + 1);
    }

    private static string BuildOpenMessage(
        MuseumPresentTier tier,
        float gold,
        int xp,
        int diamonds)
    {
        List<string> rewards = new List<string>();

        if (gold > 0f)
            rewards.Add($"{gold:0.##} Gold");
        if (xp > 0)
            rewards.Add($"{xp:N0} XP");
        if (diamonds > 0)
            rewards.Add($"{diamonds:N0} Diamonds");

        string rewardText = rewards.Count > 0
            ? string.Join(", ", rewards)
            : "no reward";

        return
            $"Opened a {MuseumPresentUtility.GetTierDisplayName(tier)} Present: " +
            rewardText + ".";
    }
}
