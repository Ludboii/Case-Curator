using System;
using UnityEngine;

/// <summary>
/// Awards 20-40 fragments of the player's current Museum band when a container
/// first reaches Gold completion. The existing goldRewardClaimed field is used
/// as the one-time authority, so the reward integrates with container progress.
/// </summary>
public class MuseumGoldContainerCompletionRewardService : MonoBehaviour
{
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
    private bool processing;

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
        if (processing ||
            SaveManager.Instance == null ||
            SaveManager.Instance.database == null ||
            ContainerProgressManager.Instance == null)
        {
            return;
        }

        processing = true;

        try
        {
            MuseumPresentService presentService =
                MuseumPresentService.GetOrCreate();
            ContainerProgressSaveData progressSnapshot =
                ContainerProgressManager.Instance.ExportSaveData();

            if (presentService == null ||
                progressSnapshot == null ||
                progressSnapshot.progressEntries == null)
            {
                return;
            }

            bool changed = false;

            // Only inspect containers that already have progress. Calling
            // IsGoldComplete for every database container would create empty
            // progress records for unopened cases.
            for (int i = 0; i < progressSnapshot.progressEntries.Count; i++)
            {
                ContainerProgressData savedProgress =
                    progressSnapshot.progressEntries[i];

                if (savedProgress == null ||
                    savedProgress.goldRewardClaimed ||
                    string.IsNullOrWhiteSpace(savedProgress.containerId))
                {
                    continue;
                }

                CaseData container =
                    SaveManager.Instance.database.GetCaseByApiId(
                        savedProgress.containerId);

                if (container == null ||
                    !ContainerProgressManager.Instance.IsGoldComplete(container))
                {
                    continue;
                }

                ContainerProgressData liveProgress =
                    ContainerProgressManager.Instance.GetProgress(container);

                if (liveProgress == null || liveProgress.goldRewardClaimed)
                    continue;

                MuseumPresentTier tier = ResolveCurrentMuseumTier();
                int amount = UnityEngine.Random.Range(
                    minimumFragments,
                    maximumFragments + 1);

                // Mark before raising the progress event to make re-entry safe.
                liveProgress.goldRewardClaimed = true;
                presentService.AddFragments(tier, amount, true);
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

            if (!changed)
                return;

            ContainerProgressManager.Instance.SaveProgress();
            SaveManager.Instance.MarkDirty();
        }
        finally
        {
            processing = false;
        }
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
}
