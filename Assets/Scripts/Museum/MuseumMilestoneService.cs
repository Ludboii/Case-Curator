using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoritative phase M4 service for the Museum Milestone Staircase.
/// UI reads state and submits claims; only this service changes claimed IDs,
/// plaque IDs or milestone rewards.
/// </summary>
public class MuseumMilestoneService : MonoBehaviour
{
    public static MuseumMilestoneService Instance { get; private set; }

    [SerializeField] private GameDatabase database;
    [SerializeField] private bool persistBetweenScenes = true;
    [SerializeField] private bool allowRuntimePreviewFallback = true;
    [SerializeField] private bool verboseLogging;

    public event Action OnMilestonesChanged;

    private readonly List<MuseumMilestoneData> milestones =
        new List<MuseumMilestoneData>();

    private List<MuseumMilestoneData> runtimePreview;
    private bool usingRuntimePreview;
    private bool cacheDirty = true;

    public bool IsUsingRuntimePreview
    {
        get
        {
            EnsureMilestones();
            return usingRuntimePreview;
        }
    }

    public bool IsReady =>
        ResolveDatabase(false) &&
        SaveManager.Instance != null &&
        SaveManager.Instance.Museum != null;

    public static MuseumMilestoneService GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        MuseumMilestoneService existing =
            FindObjectOfType<MuseumMilestoneService>();

        if (existing != null)
            return existing;

        GameObject go = new GameObject("MuseumMilestoneService");
        return go.AddComponent<MuseumMilestoneService>();
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

        ResolveDatabase(true);
    }

    private void OnDestroy()
    {
        if (runtimePreview != null)
        {
            for (int i = 0; i < runtimePreview.Count; i++)
            {
                if (runtimePreview[i] != null)
                    Destroy(runtimePreview[i]);
            }
        }

        if (Instance == this)
            Instance = null;
    }

    public void InvalidateMilestones()
    {
        cacheDirty = true;
    }

    public IReadOnlyList<MuseumMilestoneData> GetMilestones()
    {
        EnsureMilestones();
        return milestones;
    }

    public IReadOnlyList<MuseumMilestoneState> GetMilestoneStates()
    {
        EnsureMilestones();

        List<MuseumMilestoneState> result =
            new List<MuseumMilestoneState>(milestones.Count);

        HashSet<string> claimed = GetClaimedIds();
        double points = GetMuseumPoints();
        double previous = 0d;

        for (int i = 0; i < milestones.Count; i++)
        {
            MuseumMilestoneData data = milestones[i];

            if (data == null)
                continue;

            bool isClaimed = claimed.Contains(data.milestoneId ?? "");
            bool pointsReached =
                points + 0.0001d >= data.requiredMuseumPoints;
            bool extraMet = true;
            string lockedReason = "";

            if (data.additionalRequirement != null)
            {
                UnlockEvaluationResult unlock =
                    UnlockEvaluator.Evaluate(data.additionalRequirement);

                extraMet = unlock != null && unlock.isUnlocked;
                lockedReason = unlock != null
                    ? unlock.FirstFailureReason
                    : "An additional requirement is not met.";
            }

            if (!pointsReached)
            {
                lockedReason =
                    $"Requires {data.requiredMuseumPoints:N0} Museum Points. " +
                    $"Current: {points:N0}.";
            }

            MuseumMilestoneClaimStatus status = isClaimed
                ? MuseumMilestoneClaimStatus.Claimed
                : pointsReached && extraMet
                    ? MuseumMilestoneClaimStatus.Claimable
                    : MuseumMilestoneClaimStatus.Locked;

            double segment =
                Math.Max(0d, data.requiredMuseumPoints - previous);

            float progress = segment <= 0d
                ? pointsReached ? 1f : 0f
                : Mathf.Clamp01(
                    (float)((points - previous) / segment));

            result.Add(new MuseumMilestoneState
            {
                data = data,
                status = status,
                currentMuseumPoints = points,
                previousRequiredMuseumPoints = previous,
                requiredMuseumPoints = data.requiredMuseumPoints,
                segmentProgress01 = progress,
                additionalRequirementMet = extraMet,
                lockedReason = lockedReason,
                runtimePreviewOnly = usingRuntimePreview
            });

            previous = Math.Max(previous, data.requiredMuseumPoints);
        }

        return result;
    }

    public MuseumMilestoneState GetMilestoneState(string milestoneId)
    {
        if (string.IsNullOrWhiteSpace(milestoneId))
            return null;

        IReadOnlyList<MuseumMilestoneState> states =
            GetMilestoneStates();

        for (int i = 0; i < states.Count; i++)
        {
            MuseumMilestoneState state = states[i];

            if (state != null &&
                string.Equals(
                    state.MilestoneId,
                    milestoneId.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return state;
            }
        }

        return null;
    }

    public MuseumMilestoneState GetNextUnclaimedMilestone()
    {
        IReadOnlyList<MuseumMilestoneState> states =
            GetMilestoneStates();

        for (int i = 0; i < states.Count; i++)
        {
            if (states[i] != null && !states[i].IsClaimed)
                return states[i];
        }

        return states.Count > 0 ? states[states.Count - 1] : null;
    }

    public MuseumMilestoneState GetCurrentReachedMilestone()
    {
        IReadOnlyList<MuseumMilestoneState> states =
            GetMilestoneStates();

        MuseumMilestoneState current = null;

        for (int i = 0; i < states.Count; i++)
        {
            MuseumMilestoneState state = states[i];

            if (state == null ||
                state.currentMuseumPoints + 0.0001d <
                state.requiredMuseumPoints)
            {
                break;
            }

            current = state;
        }

        return current;
    }

    public int GetClaimableCount()
    {
        IReadOnlyList<MuseumMilestoneState> states =
            GetMilestoneStates();

        int count = 0;

        for (int i = 0; i < states.Count; i++)
        {
            if (states[i] != null && states[i].IsClaimable)
                count++;
        }

        return count;
    }

    public int GetClaimedCount()
    {
        return GetClaimedIds().Count;
    }

    public bool HasClaimed(string milestoneId)
    {
        return !string.IsNullOrWhiteSpace(milestoneId) &&
               GetClaimedIds().Contains(milestoneId.Trim());
    }

    public bool HasUnlockedPassiveMuseumGold()
    {
        return HasClaimedFlag(data => data.unlocksPassiveMuseumGold);
    }

    public bool HasUnlockedPassiveDiamonds()
    {
        return HasClaimedFlag(data => data.unlocksPassiveDiamonds);
    }

    public MuseumMilestoneClaimResult Claim(string milestoneId)
    {
        MuseumMilestoneState state = GetMilestoneState(milestoneId);

        if (state == null || state.data == null)
        {
            return MuseumMilestoneClaimResult.Failed(
                null,
                "The requested Museum milestone does not exist.");
        }

        MuseumMilestoneData data = state.data;

        if (state.runtimePreviewOnly)
        {
            return MuseumMilestoneClaimResult.Failed(
                data,
                "Run Tools > Case Curator > Museum > Generate or Update " +
                "80-Step Staircase before claiming milestones.");
        }

        if (!IsReady)
        {
            return MuseumMilestoneClaimResult.Failed(
                data,
                "Save or Museum progression data is unavailable.");
        }

        if (state.IsClaimed)
        {
            return MuseumMilestoneClaimResult.Failed(
                data,
                "This Museum milestone has already been claimed.");
        }

        if (!state.IsClaimable)
        {
            return MuseumMilestoneClaimResult.Failed(
                data,
                string.IsNullOrWhiteSpace(state.lockedReason)
                    ? "This Museum milestone is locked."
                    : state.lockedReason);
        }

        string rewardError = ValidateReward(data.reward);

        if (!string.IsNullOrWhiteSpace(rewardError))
            return MuseumMilestoneClaimResult.Failed(data, rewardError);

        MuseumStateSaveData museum = SaveManager.Instance.Museum;

        if (museum.claimedMilestoneIds == null)
            museum.claimedMilestoneIds = new List<string>();

        if (museum.unlockedPlaqueIds == null)
            museum.unlockedPlaqueIds = new List<string>();

        AddUnique(museum.claimedMilestoneIds, data.milestoneId);
        AddUnique(museum.unlockedPlaqueIds, data.unlockedPlaqueId);

        MuseumMilestoneClaimResult result =
            new MuseumMilestoneClaimResult
            {
                success = true,
                milestone = data,
                reward = data.reward
            };

        ApplyReward(data, result);
        SaveManager.Instance.MarkDirty();

        result.totalClaimedMilestones = GetClaimedCount();
        result.remainingClaimableMilestones = GetClaimableCount();
        result.message =
            $"Claimed Museum Step {data.stairNumber}: {data.DisplayName}.";

        OnMilestonesChanged?.Invoke();

        if (verboseLogging)
        {
            Debug.Log(
                $"Claimed {data.milestoneId} at " +
                $"{data.requiredMuseumPoints:N0} MP.",
                this);
        }

        return result;
    }

    private bool HasClaimedFlag(Predicate<MuseumMilestoneData> predicate)
    {
        EnsureMilestones();
        HashSet<string> claimed = GetClaimedIds();

        for (int i = 0; i < milestones.Count; i++)
        {
            MuseumMilestoneData data = milestones[i];

            if (data != null &&
                predicate(data) &&
                claimed.Contains(data.milestoneId ?? ""))
            {
                return true;
            }
        }

        return false;
    }

    private static string ValidateReward(MuseumRewardData reward)
    {
        if (SaveManager.Instance == null)
            return "SaveManager is unavailable.";

        if (reward == null || reward.containerRewards == null)
            return "";

        for (int i = 0; i < reward.containerRewards.Count; i++)
        {
            MuseumContainerReward entry = reward.containerRewards[i];

            if (entry != null &&
                entry.container != null &&
                entry.amount > 0 &&
                CaseInventoryManager.Instance == null)
            {
                return
                    "CaseInventoryManager is required for this " +
                    "milestone's container reward.";
            }
        }

        return "";
    }

    private static void ApplyReward(
        MuseumMilestoneData data,
        MuseumMilestoneClaimResult result)
    {
        MuseumRewardData reward = data.reward;

        if (reward != null)
        {
            if (reward.gold > 0f)
            {
                SaveManager.Instance.AddGold(reward.gold);
                result.grantedRewardLines.Add(
                    $"+{reward.gold:0.##} Gold");
            }

            if (reward.diamonds > 0)
            {
                SaveManager.Instance.AddDiamonds(reward.diamonds);
                result.grantedRewardLines.Add(
                    $"+{reward.diamonds:N0} Diamonds");
            }

            if (reward.xp > 0)
            {
                SaveManager.Instance.AddXP(reward.xp);
                result.grantedRewardLines.Add(
                    $"+{reward.xp:N0} XP");
            }

            if (reward.containerRewards != null)
            {
                for (int i = 0; i < reward.containerRewards.Count; i++)
                {
                    MuseumContainerReward entry =
                        reward.containerRewards[i];

                    if (entry == null ||
                        entry.container == null ||
                        entry.amount <= 0)
                    {
                        continue;
                    }

                    CaseInventoryManager.Instance.AddCases(
                        entry.container,
                        entry.amount);

                    result.grantedRewardLines.Add(
                        $"+{entry.amount:N0}x {entry.container.caseName}");
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(data.unlockedPlaqueId))
        {
            result.grantedRewardLines.Add(
                $"Plaque unlocked: {data.unlockedPlaqueId}");
        }

        if (data.unlocksPassiveMuseumGold)
        {
            result.grantedRewardLines.Add(
                "Passive Museum Gold node unlocked.");
        }

        if (data.unlocksPassiveDiamonds)
        {
            result.grantedRewardLines.Add(
                "Passive diamond generation unlocked for M5.");
        }

        if (!string.IsNullOrWhiteSpace(data.announcedSystemId))
        {
            result.grantedRewardLines.Add(
                $"System milestone: {data.announcedSystemId}");
        }
    }

    private void EnsureMilestones()
    {
        if (!cacheDirty)
            return;

        cacheDirty = false;
        milestones.Clear();
        usingRuntimePreview = false;
        ResolveDatabase(true);

        if (database != null &&
            database.museumMilestones != null &&
            database.museumMilestones.Count > 0)
        {
            for (int i = 0; i < database.museumMilestones.Count; i++)
            {
                MuseumMilestoneData data =
                    database.museumMilestones[i];

                if (data != null)
                    milestones.Add(data);
            }
        }
        else if (allowRuntimePreviewFallback)
        {
            if (runtimePreview == null)
            {
                runtimePreview =
                    MuseumMilestone80Defaults
                        .CreateRuntimePreviewMilestones();
            }

            milestones.AddRange(runtimePreview);
            usingRuntimePreview = true;
        }

        milestones.Sort(CompareMilestones);
    }

    private bool ResolveDatabase(bool log)
    {
        if (database == null && SaveManager.Instance != null)
            database = SaveManager.Instance.database;

        if (database != null)
            return true;

        if (log)
        {
            Debug.LogWarning(
                "MuseumMilestoneService could not resolve GameDatabase.",
                this);
        }

        return false;
    }

    private static double GetMuseumPoints()
    {
        return SaveManager.Instance != null &&
               SaveManager.Instance.Museum != null
            ? Math.Max(
                0d,
                SaveManager.Instance.Museum.museumPoints)
            : 0d;
    }

    private static HashSet<string> GetClaimedIds()
    {
        HashSet<string> ids =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (SaveManager.Instance == null ||
            SaveManager.Instance.Museum == null ||
            SaveManager.Instance.Museum.claimedMilestoneIds == null)
        {
            return ids;
        }

        List<string> source =
            SaveManager.Instance.Museum.claimedMilestoneIds;

        for (int i = 0; i < source.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(source[i]))
                ids.Add(source[i].Trim());
        }

        return ids;
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

    private static int CompareMilestones(
        MuseumMilestoneData a,
        MuseumMilestoneData b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        int step = a.stairNumber.CompareTo(b.stairNumber);

        return step != 0
            ? step
            : a.requiredMuseumPoints.CompareTo(
                b.requiredMuseumPoints);
    }
}
