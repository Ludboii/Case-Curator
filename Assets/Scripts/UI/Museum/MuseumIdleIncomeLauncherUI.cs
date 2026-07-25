using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Entrance card for M5. Shows claimable visitor income and opens the income
/// popup without performing any currency mutation itself.
/// </summary>
public sealed class MuseumIdleIncomeLauncherUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private MuseumIdleIncomePopupUI popup;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private GameObject notificationBadge;
    [SerializeField] private TMP_Text notificationText;

    private MuseumIdleIncomeService service;
    private bool subscribed;

    private void Awake()
    {
        ResolveViewReferences();

        if (button != null)
        {
            button.onClick.RemoveListener(OpenPopup);
            button.onClick.AddListener(OpenPopup);
        }
    }

    private void Start()
    {
        ResolveService();
        Subscribe();
        Refresh();
    }

    private void OnEnable()
    {
        ResolveViewReferences();

        if (SaveManager.Instance != null)
        {
            ResolveService();
            Subscribe();
            Refresh();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (button != null)
            button.onClick.RemoveListener(OpenPopup);
    }

    public void OpenPopup()
    {
        ResolveViewReferences();
        ResolveService();

        if (popup == null)
        {
            Debug.LogWarning(
                "MuseumIdleIncomeLauncherUI: Popup is not assigned.",
                this);
            return;
        }

        popup.Open();
    }

    public void Refresh()
    {
        ResolveService();

        if (service == null)
            return;

        MuseumIdleIncomeSnapshot snapshot = service.GetSnapshot(true);
        int wholeDiamonds = snapshot.ClaimableWholeDiamonds;
        bool hasNotification =
            snapshot.unclaimedGold >= 0.01d || wholeDiamonds > 0;

        if (notificationBadge != null)
            notificationBadge.SetActive(hasNotification);

        if (notificationText != null)
        {
            int count = (snapshot.unclaimedGold >= 0.01d ? 1 : 0) +
                        (wholeDiamonds > 0 ? 1 : 0);
            notificationText.text = count > 0 ? count.ToString() : "";
        }

        if (summaryText == null)
            return;

        if (!snapshot.goldUnlocked)
        {
            summaryText.text =
                "Unlock visitor income from the Museum Staircase";
        }
        else if (snapshot.unclaimedGold >= 0.01d && wholeDiamonds > 0)
        {
            summaryText.text =
                $"{snapshot.unclaimedGold:N2} Gold + " +
                $"{wholeDiamonds:N0} Diamonds ready";
        }
        else if (snapshot.unclaimedGold >= 0.01d)
        {
            summaryText.text =
                $"{snapshot.unclaimedGold:N2} Gold ready to claim";
        }
        else if (wholeDiamonds > 0)
        {
            summaryText.text =
                $"{wholeDiamonds:N0} Diamonds ready to claim";
        }
        else
        {
            summaryText.text =
                $"Generating {snapshot.goldPerHour:N2} Gold per hour";
        }
    }

    private void ResolveViewReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (popup == null)
        {
            popup = FindFirstObjectByType<MuseumIdleIncomePopupUI>(
                FindObjectsInactive.Include);
        }
    }

    private void ResolveService()
    {
        if (service == null && SaveManager.Instance != null)
            service = MuseumIdleIncomeService.GetOrCreate();
    }

    private void Subscribe()
    {
        if (service == null || subscribed)
            return;

        service.OnIdleIncomeChanged += Refresh;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (service != null && subscribed)
            service.OnIdleIncomeChanged -= Refresh;

        subscribed = false;
    }
}
