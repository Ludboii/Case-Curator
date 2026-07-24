using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One generated plaque/step on the Museum Milestone Staircase.
/// Selecting a locked step is allowed so the player can inspect its requirement.
/// </summary>
public class MuseumMilestoneStepUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text stepText;
    [SerializeField] private TMP_Text bandText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject claimableBadge;
    [SerializeField] private GameObject majorRoot;
    [SerializeField] private GameObject selectedRoot;

    [Header("State Colors")]
    [SerializeField] private Color lockedColor =
        new Color(0.2f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color reachedColor =
        new Color(0.78f, 0.58f, 0.08f, 1f);
    [SerializeField] private Color claimedColor =
        new Color(0.22f, 0.58f, 0.28f, 1f);

    private MuseumMilestoneState state;
    private MuseumStaircaseUI owner;

    public MuseumMilestoneState State => state;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    public void Setup(
        MuseumMilestoneState milestoneState,
        MuseumStaircaseUI staircase)
    {
        ResolveReferences();

        state = milestoneState;
        owner = staircase;
        MuseumMilestoneData data =
            state != null ? state.data : null;

        if (stepText != null)
            stepText.text = data != null
                ? $"STEP {data.stairNumber:00}"
                : "STEP --";

        if (bandText != null)
            bandText.text = data != null
                ? data.BandDisplayName
                : "Museum";

        if (titleText != null)
            titleText.text = data != null
                ? data.DisplayName
                : "Missing Milestone";

        if (pointsText != null)
            pointsText.text = data != null
                ? $"{data.requiredMuseumPoints:N0} MP"
                : "0 MP";

        if (rewardText != null)
            rewardText.text = data != null
                ? data.rewardSummary
                : "";

        MuseumMilestoneClaimStatus status = state != null
            ? state.status
            : MuseumMilestoneClaimStatus.Locked;

        if (statusText != null)
        {
            switch (status)
            {
                case MuseumMilestoneClaimStatus.Claimed:
                    statusText.text = "CLAIMED";
                    break;

                case MuseumMilestoneClaimStatus.Claimable:
                    statusText.text = "CLAIM";
                    break;

                default:
                    statusText.text = "LOCKED";
                    break;
            }
        }

        if (background != null)
        {
            background.color = status ==
                MuseumMilestoneClaimStatus.Claimed
                    ? claimedColor
                    : status ==
                      MuseumMilestoneClaimStatus.Claimable
                        ? reachedColor
                        : lockedColor;
        }

        if (iconImage != null)
        {
            iconImage.sprite = data != null ? data.icon : null;
            iconImage.enabled =
                data != null && data.icon != null;
            iconImage.preserveAspect = true;
        }

        if (claimableBadge != null)
        {
            claimableBadge.SetActive(
                status == MuseumMilestoneClaimStatus.Claimable);
        }

        if (majorRoot != null)
        {
            majorRoot.SetActive(
                data != null && data.majorMilestone);
        }

        SetSelected(false);

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
            button.interactable = state != null && data != null;
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectedRoot != null)
            selectedRoot.SetActive(selected);
    }

    private void HandleClicked()
    {
        if (owner != null && state != null)
            owner.SelectMilestone(state);
    }

    private void ResolveReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (background == null)
            background = GetComponent<Image>();

        TMP_Text[] texts =
            GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];

            if (text == null)
                continue;

            string objectName =
                text.gameObject.name.ToLowerInvariant();

            if (stepText == null && objectName.Contains("step"))
                stepText = text;
            else if (bandText == null && objectName.Contains("band"))
                bandText = text;
            else if (titleText == null && objectName.Contains("title"))
                titleText = text;
            else if (pointsText == null &&
                     (objectName.Contains("point") ||
                      objectName.Contains("requirement")))
                pointsText = text;
            else if (rewardText == null && objectName.Contains("reward"))
                rewardText = text;
            else if (statusText == null && objectName.Contains("status"))
                statusText = text;
        }
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }
}
