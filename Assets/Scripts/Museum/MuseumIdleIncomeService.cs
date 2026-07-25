using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoritative M5 service for passive Museum Gold and the late-game Diamond
/// Endowment. Income is calculated from UTC time, respects offline and storage
/// caps, persists fractional progress and only grants currency through an
/// explicit player claim.
/// </summary>
public sealed class MuseumIdleIncomeService : MonoBehaviour
{
    private struct RateContext
    {
        public bool goldUnlocked;
        public bool diamondsUnlocked;
        public double museumPoints;
        public int goldNodeCount;
        public double goldNodeWeight;
        public double goldPerHour;
        public double diamondsPerHour;
        public double goldCapacity;
        public double diamondCapacity;
        public double maximumOfflineHours;
    }

    public static MuseumIdleIncomeService Instance { get; private set; }

    [SerializeField] private GameDatabase database;
    [SerializeField] private bool persistBetweenScenes = true;
    [SerializeField] private bool verboseLogging;

    public event Action OnIdleIncomeChanged;
    public event Action<MuseumIdleIncomeClaimResult> OnIdleIncomeClaimed;

    private MuseumMilestoneService milestoneService;
    private MuseumService museumService;
    private MuseumStateSaveData observedState;
    private RateContext rateContext;

    private bool initialized;
    private bool subscribedToSaveManager;
    private bool subscribedToMilestones;
    private bool subscribedToMuseum;
    private bool processing;
    private float nextCalculationTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GetOrCreate();
    }

    public static MuseumIdleIncomeService GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        MuseumIdleIncomeService existing =
            FindFirstObjectByType<MuseumIdleIncomeService>();

        if (existing != null)
            return existing;

        GameObject go = new GameObject("MuseumIdleIncomeService");
        return go.AddComponent<MuseumIdleIncomeService>();
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
        {
            TryInitialize();
            return;
        }

        ResolveEventSources();

        if (Time.unscaledTime >= nextCalculationTime)
            ProcessElapsedTimeNow(false);
    }

    private void OnApplicationPause(bool paused)
    {
        if (!initialized)
            return;

        ProcessElapsedTimeNow(true);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!initialized)
            return;

        ProcessElapsedTimeNow(true);
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (Instance == this)
            Instance = null;
    }

    public MuseumIdleIncomeSnapshot GetSnapshot(bool processElapsedTime = true)
    {
        if (!initialized)
            TryInitialize();

        if (processElapsedTime)
            ProcessElapsedTimeNow(false);

        MuseumStateSaveData state = GetState();

        if (state == null)
            return new MuseumIdleIncomeSnapshot();

        NormalizeState(state);
        RateContext current = CaptureCurrentContext();

        return new MuseumIdleIncomeSnapshot
        {
            goldUnlocked = current.goldUnlocked,
            diamondsUnlocked = current.diamondsUnlocked,
            museumPoints = current.museumPoints,
            claimedGoldNodeCount = current.goldNodeCount,
            claimedGoldNodeWeight = current.goldNodeWeight,
            goldPerHour = current.goldPerHour,
            diamondsPerHour = current.diamondsPerHour,
            unclaimedGold = state.unclaimedIdleGold,
            unclaimedDiamonds = state.unclaimedIdleDiamonds,
            goldCapacity = current.goldCapacity,
            diamondCapacity = current.diamondCapacity,
            maximumOfflineHours = current.maximumOfflineHours,
            goldAtCapacity =
                current.goldCapacity > 0d &&
                state.unclaimedIdleGold + 0.0001d >= current.goldCapacity,
            diamondsAtCapacity =
                current.diamondCapacity > 0d &&
                state.unclaimedIdleDiamonds + 0.0001d >= current.diamondCapacity
        };
    }

    public bool ProcessElapsedTimeNow(bool force)
    {
        if (processing)
            return false;

        if (!initialized)
        {
            TryInitialize();
            return initialized;
        }

        MuseumStateSaveData state = GetState();

        if (state == null)
            return false;

        if (!ReferenceEquals(observedState, state))
        {
            BindLoadedState(state);
            return true;
        }

        processing = true;

        try
        {
            return AccumulateTo(DateTime.UtcNow.Ticks, force);
        }
        finally
        {
            processing = false;
        }
    }

    public MuseumIdleIncomeClaimResult ClaimGold()
    {
        return Claim(true, false);
    }

    public MuseumIdleIncomeClaimResult ClaimDiamonds()
    {
        return Claim(false, true);
    }

    public MuseumIdleIncomeClaimResult ClaimAll()
    {
        return Claim(true, true);
    }

    public void SimulateElapsedHoursForTesting(double hours)
    {
        if (!initialized)
            TryInitialize();

        MuseumStateSaveData state = GetState();

        if (state == null || hours <= 0d)
            return;

        long ticks = (long)Math.Min(
            TimeSpan.MaxValue.Ticks,
            TimeSpan.FromHours(hours).Ticks);

        long current = state.lastIdleGoldCalculationUtcTicks;

        if (current <= 0)
            current = DateTime.UtcNow.Ticks;

        state.lastIdleGoldCalculationUtcTicks =
            Math.Max(DateTime.MinValue.Ticks, current - ticks);

        ProcessElapsedTimeNow(true);
    }

    [ContextMenu("Debug/Simulate 1 Hour")]
    private void SimulateOneHour()
    {
        SimulateElapsedHoursForTesting(1d);
    }

    [ContextMenu("Debug/Simulate 8 Hours")]
    private void SimulateEightHours()
    {
        SimulateElapsedHoursForTesting(8d);
    }

    private void TryInitialize()
    {
        if (initialized || SaveManager.Instance == null)
            return;

        if (database == null)
            database = SaveManager.Instance.database;

        if (database == null ||
            database.museumBalance == null ||
            database.museumBalance.idleIncome == null ||
            SaveManager.Instance.Museum == null)
        {
            return;
        }

        milestoneService = MuseumMilestoneService.GetOrCreate();
        observedState = SaveManager.Instance.Museum;
        NormalizeState(observedState);
        rateContext = CaptureCurrentContext();
        initialized = true;

        Subscribe();
        ResolveEventSources();

        if (observedState.lastIdleGoldCalculationUtcTicks <= 0)
        {
            observedState.lastIdleGoldCalculationUtcTicks = DateTime.UtcNow.Ticks;
            SaveManager.Instance.MarkDirty();
        }
        else
        {
            ProcessElapsedTimeNow(true);
        }

        ScheduleNextCalculation();
        OnIdleIncomeChanged?.Invoke();
    }

    private void BindLoadedState(MuseumStateSaveData state)
    {
        observedState = state;
        NormalizeState(observedState);
        rateContext = CaptureCurrentContext();

        if (observedState.lastIdleGoldCalculationUtcTicks <= 0)
        {
            observedState.lastIdleGoldCalculationUtcTicks = DateTime.UtcNow.Ticks;
            SaveManager.Instance.MarkDirty();
        }
        else
        {
            processing = true;

            try
            {
                AccumulateTo(DateTime.UtcNow.Ticks, true);
            }
            finally
            {
                processing = false;
            }
        }

        ScheduleNextCalculation();
        OnIdleIncomeChanged?.Invoke();
    }

    private bool AccumulateTo(long nowTicks, bool force)
    {
        MuseumStateSaveData state = observedState;
        MuseumIdleIncomeSettings settings = GetSettings();

        if (state == null || settings == null)
            return false;

        NormalizeState(state);

        if (state.lastIdleGoldCalculationUtcTicks <= 0)
        {
            state.lastIdleGoldCalculationUtcTicks = nowTicks;
            rateContext = CaptureCurrentContext();
            SaveManager.Instance.MarkDirty();
            ScheduleNextCalculation();
            return true;
        }

        long elapsedTicks = nowTicks - state.lastIdleGoldCalculationUtcTicks;

        if (elapsedTicks < 0)
        {
            state.lastIdleGoldCalculationUtcTicks = nowTicks;
            rateContext = CaptureCurrentContext();
            SaveManager.Instance.MarkDirty();
            ScheduleNextCalculation();
            return true;
        }

        double elapsedSeconds = elapsedTicks / (double)TimeSpan.TicksPerSecond;
        double minimumSeconds =
            Math.Max(0d, settings.minimumCalculationIntervalSeconds);

        if (!force && elapsedSeconds + 0.0001d < minimumSeconds)
        {
            ScheduleNextCalculation();
            return false;
        }

        double elapsedHours = elapsedSeconds / 3600d;
        double eligibleHours = rateContext.maximumOfflineHours > 0d
            ? Math.Min(elapsedHours, rateContext.maximumOfflineHours)
            : elapsedHours;

        eligibleHours = Math.Max(0d, eligibleHours);

        double goldBefore = state.unclaimedIdleGold;
        double diamondsBefore = state.unclaimedIdleDiamonds;

        if (rateContext.goldUnlocked && rateContext.goldPerHour > 0d)
        {
            double generated = rateContext.goldPerHour * eligibleHours;
            state.unclaimedIdleGold = AddWithCapacity(
                state.unclaimedIdleGold,
                generated,
                rateContext.goldCapacity);
        }

        if (rateContext.diamondsUnlocked && rateContext.diamondsPerHour > 0d)
        {
            double generated = rateContext.diamondsPerHour * eligibleHours;
            state.unclaimedIdleDiamonds = AddWithCapacity(
                state.unclaimedIdleDiamonds,
                generated,
                rateContext.diamondCapacity);
        }

        state.lastIdleGoldCalculationUtcTicks = nowTicks;
        rateContext = CaptureCurrentContext();
        SaveManager.Instance.MarkDirty();
        ScheduleNextCalculation();

        bool changed =
            Math.Abs(state.unclaimedIdleGold - goldBefore) > 0.0000001d ||
            Math.Abs(state.unclaimedIdleDiamonds - diamondsBefore) > 0.0000001d;

        if (changed)
        {
            OnIdleIncomeChanged?.Invoke();

            if (verboseLogging)
            {
                Debug.Log(
                    $"Museum idle income: +" +
                    $"{state.unclaimedIdleGold - goldBefore:0.####} Gold, +" +
                    $"{state.unclaimedIdleDiamonds - diamondsBefore:0.####} Diamonds " +
                    $"over {eligibleHours:0.###} eligible hours.",
                    this);
            }
        }

        return true;
    }

    private MuseumIdleIncomeClaimResult Claim(
        bool claimGold,
        bool claimDiamonds)
    {
        if (!initialized)
            TryInitialize();

        ProcessElapsedTimeNow(true);

        MuseumStateSaveData state = GetState();

        if (state == null || SaveManager.Instance == null)
        {
            return MuseumIdleIncomeClaimResult.Empty(
                "Museum income state is unavailable.");
        }

        NormalizeState(state);

        double gold = claimGold
            ? Math.Max(0d, state.unclaimedIdleGold)
            : 0d;
        int diamonds = claimDiamonds
            ? Math.Max(
                0,
                (int)Math.Floor(
                    state.unclaimedIdleDiamonds + 0.0000001d))
            : 0;

        if (gold <= 0.0001d && diamonds <= 0)
        {
            return MuseumIdleIncomeClaimResult.Empty(
                "No Museum income is ready to claim.");
        }

        if (gold > 0.0001d)
        {
            state.unclaimedIdleGold = 0d;
            state.lifetimeIdleGoldClaimed += gold;
            SaveManager.Instance.AddGold(
                (float)Math.Min(gold, float.MaxValue));
        }

        if (diamonds > 0)
        {
            state.unclaimedIdleDiamonds =
                Math.Max(0d, state.unclaimedIdleDiamonds - diamonds);
            state.lifetimeIdleDiamondsClaimed += diamonds;
            SaveManager.Instance.AddDiamonds(diamonds);
        }

        SaveManager.Instance.MarkDirty();

        MuseumIdleIncomeClaimResult result =
            new MuseumIdleIncomeClaimResult
            {
                success = true,
                goldClaimed = gold,
                diamondsClaimed = diamonds,
                message = BuildClaimMessage(gold, diamonds)
            };

        OnIdleIncomeChanged?.Invoke();
        OnIdleIncomeClaimed?.Invoke(result);
        return result;
    }

    private RateContext CaptureCurrentContext()
    {
        MuseumIdleIncomeSettings settings = GetSettings();
        MuseumStateSaveData state = GetState();
        RateContext context = new RateContext();

        if (settings == null || state == null)
            return context;

        context.museumPoints = Math.Max(0d, state.museumPoints);

        HashSet<string> claimed = new HashSet<string>(
            state.claimedMilestoneIds ?? new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        double capacityMultiplierBonus = 0d;
        double offlineHoursBonus = 0d;

        if (milestoneService == null)
            milestoneService = MuseumMilestoneService.GetOrCreate();

        IReadOnlyList<MuseumMilestoneData> milestones =
            milestoneService != null
                ? milestoneService.GetMilestones()
                : null;

        if (milestones != null)
        {
            for (int i = 0; i < milestones.Count; i++)
            {
                MuseumMilestoneData milestone = milestones[i];

                if (milestone == null ||
                    string.IsNullOrWhiteSpace(milestone.milestoneId) ||
                    !claimed.Contains(milestone.milestoneId))
                {
                    continue;
                }

                MuseumIdleMilestoneModifier modifier =
                    settings.GetModifier(milestone.milestoneId);

                if (modifier != null)
                {
                    capacityMultiplierBonus +=
                        Math.Max(0d, modifier.goldCapacityMultiplierBonus);
                    offlineHoursBonus +=
                        Math.Max(0d, modifier.offlineHoursBonus);
                }

                if (milestone.unlocksPassiveMuseumGold)
                {
                    context.goldNodeCount++;
                    context.goldNodeWeight +=
                        modifier != null && modifier.goldNodeWeight > 0f
                            ? modifier.goldNodeWeight
                            : 1d;
                }

                if (milestone.unlocksPassiveDiamonds)
                    context.diamondsUnlocked = true;
            }
        }

        context.goldUnlocked = context.goldNodeWeight > 0d;
        context.goldPerHour = context.goldUnlocked
            ? context.museumPoints *
              Math.Max(0d, settings.goldPerMuseumPointPerHour) *
              context.goldNodeWeight
            : 0d;
        context.diamondsPerHour = context.diamondsUnlocked
            ? Math.Max(0d, settings.diamondsPerHour)
            : 0d;

        double baseGoldCapacity =
            Math.Max(0d, settings.unclaimedGoldCapacity);
        context.goldCapacity = baseGoldCapacity > 0d
            ? baseGoldCapacity * Math.Max(1d, 1d + capacityMultiplierBonus)
            : 0d;
        context.diamondCapacity =
            Math.Max(0d, settings.unclaimedDiamondCapacity);
        context.maximumOfflineHours =
            Math.Max(0d, settings.maximumOfflineHours + offlineHoursBonus);

        return context;
    }

    private MuseumIdleIncomeSettings GetSettings()
    {
        if (database == null && SaveManager.Instance != null)
            database = SaveManager.Instance.database;

        return database != null && database.museumBalance != null
            ? database.museumBalance.idleIncome
            : null;
    }

    private MuseumStateSaveData GetState()
    {
        return SaveManager.Instance != null
            ? SaveManager.Instance.Museum
            : null;
    }

    private void ResolveEventSources()
    {
        if (milestoneService == null)
            milestoneService = MuseumMilestoneService.GetOrCreate();

        if (!subscribedToMilestones && milestoneService != null)
        {
            milestoneService.OnMilestonesChanged += HandleProgressionChanged;
            subscribedToMilestones = true;
        }

        if (museumService == null)
        {
            museumService = MuseumService.Instance != null
                ? MuseumService.Instance
                : FindFirstObjectByType<MuseumService>();
        }

        if (!subscribedToMuseum && museumService != null)
        {
            museumService.OnMuseumChanged += HandleProgressionChanged;
            subscribedToMuseum = true;
        }
    }

    private void Subscribe()
    {
        if (!subscribedToSaveManager && SaveManager.Instance != null)
        {
            SaveManager.Instance.OnProgressChanged += HandleSaveProgressChanged;
            subscribedToSaveManager = true;
        }

        ResolveEventSources();
    }

    private void Unsubscribe()
    {
        if (subscribedToSaveManager && SaveManager.Instance != null)
        {
            SaveManager.Instance.OnProgressChanged -= HandleSaveProgressChanged;
        }

        if (subscribedToMilestones && milestoneService != null)
        {
            milestoneService.OnMilestonesChanged -= HandleProgressionChanged;
        }

        if (subscribedToMuseum && museumService != null)
        {
            museumService.OnMuseumChanged -= HandleProgressionChanged;
        }

        subscribedToSaveManager = false;
        subscribedToMilestones = false;
        subscribedToMuseum = false;
    }

    private void HandleSaveProgressChanged()
    {
        MuseumStateSaveData current = GetState();

        if (!ReferenceEquals(observedState, current))
        {
            if (current != null)
                BindLoadedState(current);

            return;
        }

        ProcessElapsedTimeNow(true);
    }

    private void HandleProgressionChanged()
    {
        // The stored RateContext represents the rates before the event. This
        // accounts for elapsed time using the old MP/node state, then captures
        // the new state for future generation.
        ProcessElapsedTimeNow(true);
        OnIdleIncomeChanged?.Invoke();
    }

    private void ScheduleNextCalculation()
    {
        MuseumIdleIncomeSettings settings = GetSettings();
        float interval = settings != null
            ? Mathf.Max(1f, settings.minimumCalculationIntervalSeconds)
            : 30f;

        nextCalculationTime = Time.unscaledTime + interval;
    }

    private static void NormalizeState(MuseumStateSaveData state)
    {
        if (state == null)
            return;

        state.museumPoints = Math.Max(0d, state.museumPoints);
        state.unclaimedIdleGold = Math.Max(0d, state.unclaimedIdleGold);
        state.unclaimedIdleDiamonds = Math.Max(0d, state.unclaimedIdleDiamonds);
        state.lifetimeIdleGoldClaimed =
            Math.Max(0d, state.lifetimeIdleGoldClaimed);
        state.lifetimeIdleDiamondsClaimed =
            Math.Max(0, state.lifetimeIdleDiamondsClaimed);

        if (state.claimedMilestoneIds == null)
            state.claimedMilestoneIds = new List<string>();
    }

    private static double AddWithCapacity(
        double current,
        double addition,
        double capacity)
    {
        double value = Math.Max(0d, current) + Math.Max(0d, addition);
        return capacity > 0d
            ? Math.Min(value, capacity)
            : value;
    }

    private static string BuildClaimMessage(
        double gold,
        int diamonds)
    {
        if (gold > 0.0001d && diamonds > 0)
        {
            return $"Claimed {gold:N2} Museum Gold and " +
                   $"{diamonds:N0} Diamonds.";
        }

        if (gold > 0.0001d)
            return $"Claimed {gold:N2} Museum Gold.";

        return $"Claimed {diamonds:N0} Diamonds.";
    }
}
