using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Entrance card/button for M4.5. Shows whether fragments or presents are
/// waiting and opens the Museum Present Desk overlay.
/// </summary>
public class MuseumPresentDeskLauncherUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private MuseumPresentDeskUI presentDesk;
    [SerializeField] private GameObject notificationBadge;
    [SerializeField] private TMP_Text notificationText;
    [SerializeField] private TMP_Text summaryText;

    private MuseumPresentService service;
    private bool subscribed;

    private void Awake()
    {
        ResolveViewReferences();

        if (button != null)
        {
            button.onClick.RemoveListener(OpenDesk);
            button.onClick.AddListener(OpenDesk);
        }
    }

    private void Start()
    {
        ResolveRuntimeService();
        Subscribe();
        Refresh();
    }

    private void OnEnable()
    {
        ResolveViewReferences();

        if (SaveManager.Instance != null)
        {
            ResolveRuntimeService();
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
            button.onClick.RemoveListener(OpenDesk);
    }

    public void OpenDesk()
    {
        ResolveViewReferences();
        ResolveRuntimeService();

        if (presentDesk == null)
        {
            Debug.LogWarning(
                "MuseumPresentDeskLauncherUI: MuseumPresentDeskUI is not assigned.",
                this);
            return;
        }

        presentDesk.Open();
    }

    public void Refresh()
    {
        if (service == null)
            return;

        int totalPresents = 0;
        int assemblable = 0;
        int totalFragments = 0;

        for (int i = 0; i < MuseumPresentUtility.AllTiers.Length; i++)
        {
            MuseumPresentTier tier = MuseumPresentUtility.AllTiers[i];
            int fragments = service.GetFragments(tier);

            totalFragments += fragments;
            totalPresents += service.GetPresents(tier);

            if (fragments >= service.GetFragmentsPerPresent(tier))
                assemblable++;
        }

        int notifications = totalPresents + assemblable;

        if (notificationBadge != null)
            notificationBadge.SetActive(notifications > 0);

        if (notificationText != null)
        {
            notificationText.text = notifications > 99
                ? "99+"
                : notifications.ToString();
        }

        if (summaryText != null)
        {
            summaryText.text = totalPresents > 0
                ? $"{totalPresents:N0} presents ready to open"
                : totalFragments > 0
                    ? $"{totalFragments:N0} fragments collected"
                    : "Collect fragments from Staircase rewards";
        }
    }

    private void HandleChanged()
    {
        Refresh();
    }

    private void ResolveViewReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (presentDesk == null)
        {
            presentDesk = FindFirstObjectByType<MuseumPresentDeskUI>(
                FindObjectsInactive.Include);
        }
    }

    private void ResolveRuntimeService()
    {
        if (service == null)
            service = MuseumPresentService.GetOrCreate();
    }

    private void Subscribe()
    {
        if (service == null || subscribed)
            return;

        service.OnPresentStateChanged += HandleChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (service != null && subscribed)
            service.OnPresentStateChanged -= HandleChanged;

        subscribed = false;
    }
}
