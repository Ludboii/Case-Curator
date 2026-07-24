using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Connects an entrance button/card to the M4 staircase and displays a badge
/// when one or more reached milestones are waiting to be claimed.
/// </summary>
public class MuseumStaircaseLauncherUI : MonoBehaviour
{
    [SerializeField] private Button staircaseButton;
    [SerializeField] private MuseumStaircaseUI staircaseUI;
    [SerializeField] private MuseumMilestoneService milestoneService;
    [SerializeField] private GameObject claimableBadge;
    [SerializeField] private TMP_Text claimableCountText;
    [SerializeField] private TMP_Text summaryText;

    private MuseumService museumService;
    private MuseumPresentService presentService;
    private bool subscribedMilestones;
    private bool subscribedMuseum;

    private void Awake()
    {
        ResolveViewReferences();

        if (staircaseButton != null)
        {
            staircaseButton.onClick.RemoveListener(OpenStaircase);
            staircaseButton.onClick.AddListener(OpenStaircase);
        }
    }

    private void Start()
    {
        ResolveRuntimeServices();
        Subscribe();
        Refresh();
    }

    private void OnEnable()
    {
        ResolveViewReferences();

        // Runtime services are intentionally not created from Awake when the
        // SaveManager might not exist yet. Start and OpenStaircase run after
        // scene managers have completed Awake.
        if (SaveManager.Instance != null)
        {
            ResolveRuntimeServices();
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

        if (staircaseButton != null)
            staircaseButton.onClick.RemoveListener(OpenStaircase);
    }

    public void OpenStaircase()
    {
        ResolveViewReferences();
        ResolveRuntimeServices();
        Subscribe();

        if (staircaseUI == null)
        {
            Debug.LogWarning(
                "MuseumStaircaseLauncherUI: MuseumStaircaseUI is not assigned.",
                this);
            return;
        }

        staircaseUI.Open(milestoneService);
    }

    public void Refresh()
    {
        ResolveViewReferences();

        int claimable = milestoneService != null
            ? milestoneService.GetClaimableCount()
            : 0;

        if (claimableBadge != null)
            claimableBadge.SetActive(claimable > 0);

        if (claimableCountText != null)
            claimableCountText.text = claimable > 99
                ? "99+"
                : claimable.ToString();

        if (summaryText != null)
        {
            MuseumMilestoneState next =
                milestoneService != null
                    ? milestoneService.GetNextUnclaimedMilestone()
                    : null;

            if (next == null || next.data == null)
            {
                summaryText.text = milestoneService == null
                    ? "Milestone Staircase"
                    : "Museum Staircase complete";
            }
            else if (next.IsClaimable)
            {
                summaryText.text =
                    $"Step {next.Step:00} ready to claim";
            }
            else
            {
                summaryText.text =
                    $"Next step: {next.requiredMuseumPoints:N0} MP";
            }
        }
    }

    private void HandleChanged()
    {
        Refresh();

        if (staircaseUI != null && staircaseUI.IsOpen)
            staircaseUI.Refresh(false);
    }

    private void ResolveViewReferences()
    {
        if (staircaseButton == null)
            staircaseButton = GetComponent<Button>();

        if (staircaseUI == null)
        {
            staircaseUI = FindFirstObjectByType<MuseumStaircaseUI>(
                FindObjectsInactive.Include);
        }
    }

    private void ResolveRuntimeServices()
    {
        if (milestoneService == null)
            milestoneService = MuseumMilestoneService.GetOrCreate();

        if (presentService == null)
            presentService = MuseumPresentService.GetOrCreate();

        if (museumService == null)
            museumService = MuseumService.Instance;
    }

    private void Subscribe()
    {
        if (milestoneService != null && !subscribedMilestones)
        {
            milestoneService.OnMilestonesChanged += HandleChanged;
            subscribedMilestones = true;
        }

        if (museumService != null && !subscribedMuseum)
        {
            museumService.OnMuseumChanged += HandleChanged;
            subscribedMuseum = true;
        }
    }

    private void Unsubscribe()
    {
        if (milestoneService != null && subscribedMilestones)
            milestoneService.OnMilestonesChanged -= HandleChanged;

        if (museumService != null && subscribedMuseum)
            museumService.OnMuseumChanged -= HandleChanged;

        subscribedMilestones = false;
        subscribedMuseum = false;
    }
}
