using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Awards 20-40 fragments of the player's current Museum band when a container
/// first reaches Gold completion. Rewards are one-time per stable CaseData apiId
/// and existing Gold completions are handled retroactively.
/// </summary>
public class MuseumGoldContainerCompletionRewardService : MonoBehaviour
{
    private const string RewardIdPrefix = "gold-container-completion:";

    public static MuseumGoldContainerCompletionRewardService Instance
    {
        get;
        private set;
    }

    [SerializeField] private bool persistBetweenScenes = true;
    [SerializeField, Min(0)] private int minimumFragments = 20;
    [SerializeField, Min(0)] private int maximumFragments = 40;
    [SerializeField] private bool verboseLogging;

    public event Action<MuseumGoldContainerCompletionReward>
        OnGoldCompletionRewardGranted;

    private bool subscribed;
    private bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GetOrCreate();
    }

    public static MuseumGoldContainerCompletionRewardService GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        MuseumGoldContainerCompletionRewardService existing =
            FindFirstObjectByType<MuseumGoldContainerCompletionRewardService>();

        if (existing != null)
            return existing;

        GameObject go =
            new GameObject("MuseumGoldContainerCompletionRewardService");
        return go.AddComponent<MuseumGoldContainerCompletionRewardService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        maximumFragments = Mathf.Max(minimumFragments, maximumFragments);

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

    public void ProcessGoldCompletions()
    {
        if (SaveManager.Instance == null ||
            SaveManager.Instance.database == null ||
            ContainerProgressManager.Instance == null)
        {
            return;
        }

        MuseumPresentService presentService =
            MuseumPresentService.GetOrCreate();
        MuseumPresentStateSaveData presentState =
            EnsurePresentState();

        if (presentService == null || presentState == null)
            return;

        ContainerProgressSaveData progressSnapshot =
            ContainerProgressManager.Instance.ExportSaveData();

        if (progressSnapshot == null ||
            progressSnapshot.progressEntries == null)
        {
            return;
        }

        HashSet<string> processed = BuildProcessedSet(
            presentState.processedMilestoneRewardIds);
        bool changed = false;

        // Only inspect containers that already have progress. Calling
        // IsGoldComplete for every database container would create empty
        // progress records for unopened cases.
        for (int i = 0; i < progressSnapshot.progressEntries.Count; i++)
        {
            ContainerProgressData progress =
                progressSnapshot.progressEntries[i];

            if (progress == null ||
                string.IsNullOrWhiteSpace(progress.containerId))
            {
                continue;
            }

            CaseData container =
                SaveManager.Instance.database.GetCaseByApiId(
                    progress.containerId);

            if (container == null || string.IsNullOrWhiteSpace(container.apiId))
                continue;

            string rewardId = RewardIdPrefix + container.apiId.Trim();

            if (processed.Contains(rewardId) ||
                !ContainerProgressManager.Instance.IsGoldComplete(container))
            {
                continue;
            }

            MuseumPresentTier tier = ResolveCurrentMuseumTier();
            int amount = UnityEngine.Random.Range(
                minimumFragments,
                maximumFragments + 1);

            // Notify immediately so the Present Desk and entrance badge refresh.
            presentService.AddFragments(tier, amount, true);
            presentState.processedMilestoneRewardIds.Add(rewardId);
            processed.Add(rewardId);
            changed = true;

            MuseumGoldContainerCompletionReward reward =
                new MuseumGoldContainerCompletionReward
                {
                    container = container,
                    tier = tier,
                    fragments = amount,
                    message =
                        $"Gold completion: {container.caseName}\n" +
                        $"+{amount:N0} " +
                        $"{MuseumPresentUtility.GetTierDisplayName(tier)} fragments"
                };

            OnGoldCompletionRewardGranted?.Invoke(reward);

            if (verboseLogging)
                Debug.Log(reward.message, this);
        }

        if (changed)
            SaveManager.Instance.MarkDirty();
    }

    private void TryInitialize()
    {
        if (initialized ||
            SaveManager.Instance == null ||
            ContainerProgressManager.Instance == null)
        {
            return;
        }

        ContainerProgressManager.Instance.OnContainerProgressChanged +=
            HandleContainerProgressChanged;
        subscribed = true;
        initialized = true;
        ProcessGoldCompletions();
    }

    private void HandleContainerProgressChanged()
    {
        ProcessGoldCompletions();
    }

    private void Unsubscribe()
    {
        if (ContainerProgressManager.Instance != null && subscribed)
        {
            ContainerProgressManager.Instance.OnContainerProgressChanged -=
                HandleContainerProgressChanged;
        }

        subscribed = false;
    }

    private static MuseumPresentTier ResolveCurrentMuseumTier()
    {
        MuseumMilestoneService milestoneService =
            MuseumMilestoneService.GetOrCreate();
        MuseumMilestoneState current =
            milestoneService != null
                ? milestoneService.GetCurrentReachedMilestone()
                : null;
        int step = current != null ? current.Step : 0;

        // Band-ending steps immediately move the player into the next Museum
        // band, even though that transition plaque visually belongs to the band
        // that was just completed.
        if (step >= 70)
            return MuseumPresentTier.GlobalElite;
        if (step >= 55)
            return MuseumPresentTier.Diamond;
        if (step >= 40)
            return MuseumPresentTier.Gold;
        if (step >= 25)
            return MuseumPresentTier.Silver;
        if (step >= 10)
            return MuseumPresentTier.Bronze;

        return MuseumPresentTier.Dusty;
    }

    private static MuseumPresentStateSaveData EnsurePresentState()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Museum == null)
            return null;

        if (SaveManager.Instance.Museum.presents == null)
        {
            SaveManager.Instance.Museum.presents =
                new MuseumPresentStateSaveData();
        }

        MuseumPresentStateSaveData state =
            SaveManager.Instance.Museum.presents;

        if (state.processedMilestoneRewardIds == null)
        {
            state.processedMilestoneRewardIds =
                new List<string>();
        }

        return state;
    }

    private static HashSet<string> BuildProcessedSet(List<string> source)
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
}
