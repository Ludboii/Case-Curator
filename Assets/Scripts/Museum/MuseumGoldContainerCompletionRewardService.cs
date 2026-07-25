using System;
using UnityEngine;

/// <summary>
/// Grants the manually claimed Gold container-completion reward: 20-40
/// fragments from the player's current Museum band. Completion alone never
/// grants or marks this reward as claimed.
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

    private bool processing;

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
        minimumFragments = Mathf.Max(0, minimumFragments);
        maximumFragments = Mathf.Max(minimumFragments, maximumFragments);

        if (persistBetweenScenes)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool CanClaim(CaseData container)
    {
        if (container == null ||
            ContainerProgressManager.Instance == null)
        {
            return false;
        }

        ContainerProgressData progress =
            ContainerProgressManager.Instance.GetProgress(container);

        return progress != null &&
               !progress.goldRewardClaimed &&
               ContainerProgressManager.Instance.IsGoldComplete(container);
    }

    public bool TryClaim(
        CaseData container,
        out MuseumGoldContainerCompletionReward reward)
    {
        reward = null;

        if (processing ||
            container == null ||
            SaveManager.Instance == null ||
            ContainerProgressManager.Instance == null)
        {
            return false;
        }

        ContainerProgressData progress =
            ContainerProgressManager.Instance.GetProgress(container);

        if (progress == null ||
            progress.goldRewardClaimed ||
            !ContainerProgressManager.Instance.IsGoldComplete(container))
        {
            return false;
        }

        MuseumPresentService presentService =
            MuseumPresentService.GetOrCreate();

        if (presentService == null)
            return false;

        processing = true;

        try
        {
            MuseumPresentTier tier = ResolveCurrentMuseumTier();
            int amount = UnityEngine.Random.Range(
                minimumFragments,
                maximumFragments + 1);

            // Mark the reward before raising save/UI events so a re-entrant
            // button press cannot grant it twice.
            progress.goldRewardClaimed = true;
            presentService.AddFragments(tier, amount, true);

            reward = new MuseumGoldContainerCompletionReward
            {
                container = container,
                tier = tier,
                fragments = amount,
                message =
                    $"Gold completion reward: {container.caseName}\n" +
                    $"+{amount:N0} " +
                    $"{MuseumPresentUtility.GetTierDisplayName(tier)} fragments"
            };

            ContainerProgressManager.Instance.SaveProgress();
            SaveManager.Instance.MarkDirty();
            SaveManager.Instance.SaveGame();

            OnGoldCompletionRewardGranted?.Invoke(reward);

            if (verboseLogging)
                Debug.Log(reward.message, this);

            return true;
        }
        finally
        {
            processing = false;
        }
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
