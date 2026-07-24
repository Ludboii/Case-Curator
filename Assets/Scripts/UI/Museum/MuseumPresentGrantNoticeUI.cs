using TMPro;
using UnityEngine;

/// <summary>
/// Optional M4.5 notice shown on the Staircase screen when a claimed milestone
/// grants fragments or full presents. Use a separate TMP label from the normal
/// milestone claim-result text so neither system overwrites the other.
/// </summary>
public class MuseumPresentGrantNoticeUI : MonoBehaviour
{
    [SerializeField] private TMP_Text noticeText;
    [SerializeField] private GameObject noticeRoot;

    private MuseumPresentService service;
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
        if (summary == null || !summary.HasRewards || noticeText == null)
            return;

        noticeText.text = string.Join("\n", summary.rewardLines);

        if (noticeRoot != null)
            noticeRoot.SetActive(true);
    }

    private void Subscribe()
    {
        if (service == null || subscribed)
            return;

        service.OnMilestonePresentRewardGranted += HandleGranted;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (service != null && subscribed)
            service.OnMilestonePresentRewardGranted -= HandleGranted;

        subscribed = false;
    }
}
