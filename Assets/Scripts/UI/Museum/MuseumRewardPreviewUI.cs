using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents the selected staircase step, its requirement, configured reward
/// payload and future-system unlocks. The component never grants rewards.
/// </summary>
public class MuseumRewardPreviewUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bandText;
    [SerializeField] private TMP_Text requirementText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text statusText;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        ResolveReferences();
    }

    public void Show(MuseumMilestoneState state)
    {
        ResolveReferences();

        if (root == null)
            root = gameObject;

        root.SetActive(state != null && state.data != null);

        if (state == null || state.data == null)
            return;

        MuseumMilestoneData data = state.data;

        if (iconImage != null)
        {
            iconImage.sprite = data.icon;
            iconImage.enabled = data.icon != null;
            iconImage.preserveAspect = true;
        }

        if (titleText != null)
            titleText.text =
                $"Step {data.stairNumber:00} — {data.DisplayName}";

        if (bandText != null)
            bandText.text = data.BandDisplayName;

        if (requirementText != null)
        {
            requirementText.text =
                $"Requires {data.requiredMuseumPoints:N0} MP\n" +
                $"Current {state.currentMuseumPoints:N0} MP";
        }

        if (rewardText != null)
            rewardText.text = BuildRewardText(data);

        if (descriptionText != null)
            descriptionText.text = data.description ?? "";

        if (statusText != null)
        {
            if (state.runtimePreviewOnly)
            {
                statusText.text =
                    "PREVIEW ONLY — GENERATE MILESTONE ASSETS";
            }
            else
            {
                switch (state.status)
                {
                    case MuseumMilestoneClaimStatus.Claimed:
                        statusText.text = "CLAIMED";
                        break;

                    case MuseumMilestoneClaimStatus.Claimable:
                        statusText.text = "READY TO CLAIM";
                        break;

                    default:
                        statusText.text = string.IsNullOrWhiteSpace(
                            state.lockedReason)
                            ? "LOCKED"
                            : state.lockedReason;
                        break;
                }
            }
        }
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    public static string BuildRewardText(MuseumMilestoneData data)
    {
        if (data == null)
            return "No reward configured.";

        StringBuilder builder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(data.rewardSummary))
            builder.AppendLine(data.rewardSummary.Trim());

        MuseumRewardData reward = data.reward;

        if (reward != null)
        {
            if (reward.gold > 0f)
                builder.AppendLine($"+{reward.gold:0.##} Gold");

            if (reward.diamonds > 0)
                builder.AppendLine($"+{reward.diamonds:N0} Diamonds");

            if (reward.xp > 0)
                builder.AppendLine($"+{reward.xp:N0} XP");

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

                    builder.AppendLine(
                        $"+{entry.amount:N0}x " +
                        $"{entry.container.caseName}");
                }
            }
        }

        if (data.unlocksPassiveMuseumGold)
            builder.AppendLine("Unlock: passive Museum Gold node");

        if (data.unlocksPassiveDiamonds)
        {
            builder.AppendLine(
                "Unlock: slow, capped passive diamond generation");
        }

        if (!string.IsNullOrWhiteSpace(data.announcedSystemId))
            builder.AppendLine($"System: {data.announcedSystemId}");

        string result = builder.ToString().TrimEnd();

        return string.IsNullOrWhiteSpace(result)
            ? "Milestone plaque and progression unlock."
            : result;
    }

    private void ResolveReferences()
    {
        TMP_Text[] texts =
            GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];

            if (text == null)
                continue;

            string objectName =
                text.gameObject.name.ToLowerInvariant();

            if (titleText == null && objectName.Contains("title"))
                titleText = text;
            else if (bandText == null && objectName.Contains("band"))
                bandText = text;
            else if (requirementText == null &&
                     objectName.Contains("requirement"))
                requirementText = text;
            else if (rewardText == null && objectName.Contains("reward"))
                rewardText = text;
            else if (descriptionText == null &&
                     (objectName.Contains("description") ||
                      objectName.Contains("note")))
                descriptionText = text;
            else if (statusText == null && objectName.Contains("status"))
                statusText = text;
        }

        if (iconImage == null)
        {
            Image[] images =
                GetComponentsInChildren<Image>(true);

            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];

                if (image != null &&
                    image.gameObject.name.ToLowerInvariant()
                        .Contains("icon"))
                {
                    iconImage = image;
                    break;
                }
            }
        }
    }
}
