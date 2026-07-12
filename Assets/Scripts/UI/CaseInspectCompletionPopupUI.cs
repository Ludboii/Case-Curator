using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CaseInspectCompletionPopupUI : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;

    [Header("Tier Explanation Text")]
    public TMP_Text bronzeExplanationText;
    public TMP_Text silverExplanationText;
    public TMP_Text goldExplanationText;
    public TMP_Text diamondExplanationText;

    [Header("Claim Buttons")]
    public Button bronzeClaimButton;
    public Button silverClaimButton;
    public Button goldClaimButton;
    public Button diamondClaimButton;

    [Header("Claim Button Text")]
    public TMP_Text bronzeClaimButtonText;
    public TMP_Text silverClaimButtonText;
    public TMP_Text goldClaimButtonText;
    public TMP_Text diamondClaimButtonText;

    [Header("Close")]
    public Button closeButton;

    [Header("Button Colors")]
    public Color claimableColor = new Color(0.20f, 0.85f, 0.35f, 1f);
    public Color lockedColor = new Color(0.30f, 0.30f, 0.30f, 1f);
    public Color claimedColor = new Color(0.15f, 0.55f, 0.25f, 1f);
    public Color comingLaterColor = new Color(0.35f, 0.35f, 0.45f, 1f);

    private CaseData currentCase;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        SetupButton(bronzeClaimButton, ClaimBronzeReward);
        SetupButton(silverClaimButton, ClaimSilverReward);
        SetupButton(goldClaimButton, ClaimGoldReward);
        SetupButton(diamondClaimButton, ClaimDiamondReward);
        SetupButton(closeButton, Close);

        Close();
    }

    public void Open(CaseData caseData)
    {
        currentCase = caseData;

        if (currentCase == null)
        {
            Close();
            return;
        }

        if (root != null)
            root.SetActive(true);

        Refresh();
    }

    public void Close()
    {
        currentCase = null;

        if (root != null)
            root.SetActive(false);
    }

    public void Refresh()
    {
        if (currentCase == null)
            return;

        ContainerProgressManager progressManager = ContainerProgressManager.Instance;
        string variantName = progressManager != null
            ? progressManager.GetVariantDisplayName(currentCase)
            : "StatTrak / Souvenir";

        if (bronzeExplanationText != null)
        {
            bronzeExplanationText.text =
                "BRONZE COMPLETION\n" +
                "Open every normal skin and one Rare Special item if the container has knives or gloves.\n" +
                "Reward: 10x this container.";
        }

        if (silverExplanationText != null)
        {
            silverExplanationText.text =
                "SILVER COMPLETION\n" +
                "Open every normal skin in its best possible wear. Rare Special items are not required.\n" +
                "Reward: 20x this container.";
        }

        if (goldExplanationText != null)
        {
            string requirement = variantName == "Unavailable"
                ? "This container has no StatTrak or Souvenir completion tier."
                : $"Open every normal skin as {variantName}. Any wear is accepted. Rare Special items are not required.";

            goldExplanationText.text =
                "GOLD COMPLETION\n" +
                requirement + "\n" +
                "Reward: present shards and a container discount. Added later.";
        }

        if (diamondExplanationText != null)
        {
            string requirement = variantName == "Unavailable"
                ? "This container has no StatTrak or Souvenir completion tier."
                : $"Open every normal skin as {variantName} in its best possible wear. Rare Special items are not required.";

            diamondExplanationText.text =
                "DIAMOND COMPLETION\n" +
                requirement + "\n" +
                "Reward: additive Museum Points and idle Gold bonuses. Added later.";
        }

        RefreshClaimButton(
            bronzeClaimButton,
            bronzeClaimButtonText,
            ContainerCompletionTier.Bronze);

        RefreshClaimButton(
            silverClaimButton,
            silverClaimButtonText,
            ContainerCompletionTier.Silver);

        RefreshClaimButton(
            goldClaimButton,
            goldClaimButtonText,
            ContainerCompletionTier.Gold);

        RefreshClaimButton(
            diamondClaimButton,
            diamondClaimButtonText,
            ContainerCompletionTier.Diamond);
    }

    private void RefreshClaimButton(
        Button button,
        TMP_Text buttonText,
        ContainerCompletionTier tier)
    {
        if (button == null)
            return;

        ContainerProgressManager progressManager = ContainerProgressManager.Instance;

        if (progressManager == null || currentCase == null)
        {
            SetButtonState(button, buttonText, false, "LOCKED", lockedColor);
            return;
        }

        bool implemented = progressManager.IsRewardImplemented(tier);
        bool complete = progressManager.IsTierComplete(currentCase, tier);
        bool claimed = progressManager.IsRewardClaimed(currentCase, tier);

        if (!implemented)
        {
            SetButtonState(button, buttonText, false, "REWARD LATER", comingLaterColor);
            return;
        }

        if (claimed)
        {
            SetButtonState(button, buttonText, false, "CLAIMED", claimedColor);
            return;
        }

        if (complete)
        {
            SetButtonState(button, buttonText, true, "CLAIM REWARD", claimableColor);
            return;
        }

        SetButtonState(button, buttonText, false, "LOCKED", lockedColor);
    }

    private void SetButtonState(
        Button button,
        TMP_Text buttonText,
        bool interactable,
        string label,
        Color color)
    {
        button.interactable = interactable;

        Graphic targetGraphic = button.targetGraphic;

        if (targetGraphic != null)
            targetGraphic.color = color;

        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = color;
        colors.pressedColor = color * 0.9f;
        colors.selectedColor = color;
        colors.disabledColor = color;
        colors.colorMultiplier = 1f;
        button.colors = colors;

        TMP_Text targetText = buttonText;

        if (targetText == null)
            targetText = button.GetComponentInChildren<TMP_Text>(true);

        if (targetText != null)
        {
            targetText.text = label;
            targetText.color = Color.white;
            targetText.raycastTarget = false;
        }
    }

    private void ClaimBronzeReward()
    {
        ClaimReward(ContainerCompletionTier.Bronze);
    }

    private void ClaimSilverReward()
    {
        ClaimReward(ContainerCompletionTier.Silver);
    }

    private void ClaimGoldReward()
    {
        ClaimReward(ContainerCompletionTier.Gold);
    }

    private void ClaimDiamondReward()
    {
        ClaimReward(ContainerCompletionTier.Diamond);
    }

    private void ClaimReward(ContainerCompletionTier tier)
    {
        if (currentCase == null || ContainerProgressManager.Instance == null)
            return;

        bool claimed = ContainerProgressManager.Instance.ClaimReward(currentCase, tier);

        if (!claimed)
            return;

        Refresh();
    }

    private void SetupButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }
}
