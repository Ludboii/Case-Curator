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
    public Color unavailableColor = new Color(0.18f, 0.18f, 0.18f, 1f);

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

    private void OnEnable()
    {
        SubscribeToProgress();
    }

    private void OnDisable()
    {
        UnsubscribeFromProgress();
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

        SubscribeToProgress();
        Refresh();
    }

    public void Close()
    {
        UnsubscribeFromProgress();
        currentCase = null;

        if (root != null)
            root.SetActive(false);
    }

    public void Refresh()
    {
        if (currentCase == null)
            return;

        ContainerProgressManager progressManager =
            ContainerProgressManager.Instance;

        if (progressManager == null)
        {
            SetMissingManagerState();
            return;
        }

        int foundCount = progressManager.GetFoundCount(currentCase);
        int foundTarget = progressManager.GetTargetCount(currentCase);
        int normalTarget = progressManager.GetNormalSkinTargetCount(currentCase);
        int bestWearCount = progressManager.GetBestWearCount(currentCase);
        int topQuarterCount =
            progressManager.GetTopQuarterFloatCount(currentCase);
        int topQuarterStatTrakCount =
            progressManager.GetTopQuarterFloatStatTrakCount(currentCase);

        bool diamondAvailable =
            progressManager.CanCompleteDiamond(currentCase);

        string containerName = string.IsNullOrWhiteSpace(currentCase.caseName)
            ? "this container"
            : currentCase.caseName;

        bool hasRareSpecial = foundTarget > normalTarget;
        string bronzeRequirement = hasRareSpecial
            ? "Open every normal skin and at least one knife/glove from the Rare Special pool."
            : "Open every normal skin in this container.";

        if (bronzeExplanationText != null)
        {
            bronzeExplanationText.text =
                "<color=#CD7F32>BRONZE COMPLETION</color>\n" +
                bronzeRequirement + "\n" +
                $"Progress: {foundCount} / {foundTarget}\n" +
                $"Reward: 20x {containerName}.";
        }

        if (silverExplanationText != null)
        {
            silverExplanationText.text =
                "<color=#D6DEE5>SILVER COMPLETION</color>\n" +
                "Open every normal skin in its highest possible wear quality. " +
                "Rare Special items are not required.\n" +
                $"Progress: {bestWearCount} / {normalTarget}\n" +
                $"Reward: 40x {containerName}.";
        }

        if (goldExplanationText != null)
        {
            goldExplanationText.text =
                "<color=#FFD12A>GOLD COMPLETION</color>\n" +
                "Open every normal skin with a float in the best 50% of that " +
                "skin's highest possible wear quality. Normal, StatTrak and " +
                "Souvenir items can satisfy this requirement. Rare Special " +
                "items are not required.\n" +
                $"Progress: {topQuarterCount} / {normalTarget}\n" +
                "Reward: 20-40 Present Shards from the current Museum band + " +
                "25% discount on this container.";
        }

        if (diamondExplanationText != null)
        {
            diamondExplanationText.text = diamondAvailable
                ? "<color=#67E8FF>DIAMOND COMPLETION</color>\n" +
                  "Open every normal skin as StatTrak with a float in the best " +
                  "50% of that skin's highest possible wear quality. Rare " +
                  "Special items are not required.\n" +
                  $"Progress: {topQuarterStatTrakCount} / {normalTarget}\n" +
                  "Reward: +0.05% Museum Points when donating + " +
                  "+0.025% Museum idle Gold income."
                : "<color=#67E8FF>DIAMOND COMPLETION</color>\n" +
                  "Unavailable: this container cannot generate StatTrak items. " +
                  "Normal Collection Packages and Souvenir Packages therefore " +
                  "cannot reach Diamond Completion.";
        }

        RefreshClaimButton(
            bronzeClaimButton,
            bronzeClaimButtonText,
            ContainerCompletionTier.Bronze,
            true);

        RefreshClaimButton(
            silverClaimButton,
            silverClaimButtonText,
            ContainerCompletionTier.Silver,
            true);

        RefreshClaimButton(
            goldClaimButton,
            goldClaimButtonText,
            ContainerCompletionTier.Gold,
            true);

        RefreshClaimButton(
            diamondClaimButton,
            diamondClaimButtonText,
            ContainerCompletionTier.Diamond,
            diamondAvailable);
    }

    private void RefreshClaimButton(
        Button button,
        TMP_Text buttonText,
        ContainerCompletionTier tier,
        bool tierAvailable)
    {
        if (button == null)
            return;

        ContainerProgressManager progressManager =
            ContainerProgressManager.Instance;

        if (progressManager == null || currentCase == null)
        {
            SetButtonState(button, buttonText, false, "LOCKED", lockedColor);
            return;
        }

        if (!tierAvailable)
        {
            SetButtonState(
                button,
                buttonText,
                false,
                "UNAVAILABLE",
                unavailableColor);
            return;
        }

        bool complete = progressManager.IsTierComplete(currentCase, tier);
        bool claimed = progressManager.IsRewardClaimed(currentCase, tier);
        bool implemented = progressManager.IsRewardImplemented(tier);

        if (claimed)
        {
            SetButtonState(button, buttonText, false, "CLAIMED", claimedColor);
            return;
        }

        if (!implemented)
        {
            SetButtonState(
                button,
                buttonText,
                false,
                complete ? "REWARD COMING LATER" : "LOCKED",
                complete ? comingLaterColor : lockedColor);
            return;
        }

        bool canClaim = progressManager.CanClaimReward(currentCase, tier);

        SetButtonState(
            button,
            buttonText,
            canClaim,
            canClaim ? "CLAIM" : "LOCKED",
            canClaim ? claimableColor : lockedColor);
    }

    private void ClaimBronzeReward()
    {
        Claim(ContainerCompletionTier.Bronze);
    }

    private void ClaimSilverReward()
    {
        Claim(ContainerCompletionTier.Silver);
    }

    private void ClaimGoldReward()
    {
        Claim(ContainerCompletionTier.Gold);
    }

    private void ClaimDiamondReward()
    {
        Claim(ContainerCompletionTier.Diamond);
    }

    private void Claim(ContainerCompletionTier tier)
    {
        if (currentCase == null || ContainerProgressManager.Instance == null)
            return;

        ContainerProgressManager.Instance.ClaimReward(currentCase, tier);
        Refresh();
    }

    private void SubscribeToProgress()
    {
        if (ContainerProgressManager.Instance != null)
        {
            ContainerProgressManager.Instance.OnContainerProgressChanged -= Refresh;
            ContainerProgressManager.Instance.OnContainerProgressChanged += Refresh;
        }
    }

    private void UnsubscribeFromProgress()
    {
        if (ContainerProgressManager.Instance != null)
            ContainerProgressManager.Instance.OnContainerProgressChanged -= Refresh;
    }

    private static void SetupButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void SetMissingManagerState()
    {
        if (bronzeExplanationText != null)
            bronzeExplanationText.text = "ContainerProgressManager is unavailable.";

        if (silverExplanationText != null)
            silverExplanationText.text = "ContainerProgressManager is unavailable.";

        if (goldExplanationText != null)
            goldExplanationText.text = "ContainerProgressManager is unavailable.";

        if (diamondExplanationText != null)
            diamondExplanationText.text = "ContainerProgressManager is unavailable.";

        SetButtonState(bronzeClaimButton, bronzeClaimButtonText, false, "LOCKED", lockedColor);
        SetButtonState(silverClaimButton, silverClaimButtonText, false, "LOCKED", lockedColor);
        SetButtonState(goldClaimButton, goldClaimButtonText, false, "LOCKED", lockedColor);
        SetButtonState(diamondClaimButton, diamondClaimButtonText, false, "LOCKED", lockedColor);
    }

    private static void SetButtonState(
        Button button,
        TMP_Text buttonText,
        bool interactable,
        string text,
        Color color)
    {
        if (button == null)
            return;

        button.interactable = interactable;

        if (button.image != null)
            button.image.color = color;

        if (buttonText != null)
            buttonText.text = text;
    }
}
