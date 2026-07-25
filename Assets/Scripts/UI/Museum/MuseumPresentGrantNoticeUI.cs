using TMPro;
using UnityEngine;

/// <summary>
/// Optional M4.5 notice shown when a milestone or Gold container completion
/// grants Museum Present fragments or full presents.
/// </summary>
public class MuseumPresentGrantNoticeUI : MonoBehaviour
{
    [SerializeField] private TMP_Text noticeText;
    [SerializeField] private GameObject noticeRoot;

    private MuseumPresentService service;
    private MuseumGoldContainerCompletionRewardService completionService;
    private bool subscribed;

    private void Awake()
    {
        if (noticeRoot == null && noticeText != null)
            noticeRoot = noticeText.gameObject;

        Hide();
    }

    private void OnEnable()
    {
        service = MuseumPresentService.GetOrCreate();
        completionService =
            MuseumGoldContainerCompletionRewardService.GetOrCreate();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    public void Hide()
    {
        if (noticeText != null)
            noticeText.text = "";

        if (noticeRoot != null)
            noticeRoot.SetActive(false);
    }

    private void HandleGranted(MuseumPresentGrantSummary summary)
    {
        if (summary == null || !summary.HasRewards)
            return;

        Show(string.Join("\n", summary.rewardLines));
    }

    private void HandleGoldCompletionGranted(
        MuseumGoldContainerCompletionReward reward)
    {
        if (reward == null)
            return;

        Show(reward.message);
    }

    private void Show(string message)
    {
        if (noticeText == null)
            return;

        noticeText.text = message ?? "";

        if (noticeRoot != null)
            noticeRoot.SetActive(true);
    }

    private void Subscribe()
    {
        if (subscribed)
            return;

        if (service != null)
            service.OnMilestonePresentRewardGranted += HandleGranted;

        if (completionService != null)
        {
            completionService.OnGoldCompletionRewardGranted +=
                HandleGoldCompletionGranted;
        }

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (service != null)
            service.OnMilestonePresentRewardGranted -= HandleGranted;

        if (completionService != null)
        {
            completionService.OnGoldCompletionRewardGranted -=
                HandleGoldCompletionGranted;
        }

        subscribed = false;
    }
}
