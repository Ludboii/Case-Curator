using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents one UpgradeData asset and delegates purchases to UpgradeService.
/// This component never edits currency or save data directly.
/// </summary>
[DisallowMultipleComponent]
public class UpgradeCardUI : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private Image upgradeIcon;
    [SerializeField] private GameObject iconRoot;
    [SerializeField] private TMP_Text upgradeNameText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Requirement Progress")]
    [SerializeField] private GameObject requirementRoot;
    [SerializeField] private TMP_Text requirementText;
    [SerializeField] private Slider requirementProgressSlider;
    [SerializeField] private Image requirementProgressFill;

    [Header("Upgrade Level Progress")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Slider levelProgressSlider;
    [SerializeField] private Image levelProgressFill;

    [Header("Purchase")]
    [SerializeField] private Button buyButton;
    [SerializeField] private Image buyButtonBackground;
    [SerializeField] private TMP_Text buyButtonText;
    [SerializeField] private Image currencyIcon;
    [SerializeField] private Sprite goldIcon;
    [SerializeField] private Sprite diamondIcon;
    [SerializeField] private TMP_Text feedbackText;

    [Header("Button Colors")]
    [SerializeField] private Color affordableColor =
        new Color(0.35f, 0.78f, 0.20f, 1f);

    [SerializeField] private Color insufficientColor =
        new Color(0.65f, 0.18f, 0.14f, 1f);

    [SerializeField] private Color lockedColor =
        new Color(0.25f, 0.28f, 0.33f, 1f);

    [SerializeField] private Color maxedColor =
        new Color(0.75f, 0.58f, 0.12f, 1f);

    private UpgradeData upgrade;
    private Action<UpgradePurchaseResult> purchaseCompleted;

    public UpgradeData Upgrade => upgrade;

    private void Awake()
    {
        if (buyButton != null)
            buyButton.onClick.AddListener(HandleBuyClicked);
    }

    private void OnDestroy()
    {
        if (buyButton != null)
            buyButton.onClick.RemoveListener(HandleBuyClicked);
    }

    public void Bind(
        UpgradeData data,
        Action<UpgradePurchaseResult> onPurchaseCompleted)
    {
        upgrade = data;
        purchaseCompleted = onPurchaseCompleted;
        Refresh();
    }

    public void Refresh()
    {
        UpgradeService service = UpgradeService.Instance;

        if (upgrade == null || service == null)
        {
            SetInvalidState(
                service == null
                    ? "Upgrade service unavailable."
                    : "Upgrade data missing.");
            return;
        }

        int currentLevel = service.GetLevel(upgrade);
        int maxLevel = Mathf.Max(0, upgrade.MaxLevel);
        bool maxed = maxLevel > 0 && currentLevel >= maxLevel;
        UpgradeLevelData nextLevel = maxed
            ? null
            : upgrade.GetNextLevelData(currentLevel);

        if (upgradeIcon != null)
        {
            upgradeIcon.sprite = upgrade.icon;
            upgradeIcon.enabled = upgrade.icon != null;
        }

        if (iconRoot != null)
            iconRoot.SetActive(upgrade.icon != null);

        if (upgradeNameText != null)
        {
            upgradeNameText.text = maxed
                ? $"{upgrade.DisplayName} — MAXED"
                : BuildLevelTitle(upgrade, nextLevel, currentLevel + 1);
        }

        if (descriptionText != null)
        {
            descriptionText.text = nextLevel != null &&
                                   !string.IsNullOrWhiteSpace(
                                       nextLevel.description)
                ? nextLevel.description.Trim()
                : upgrade.description ?? "";
        }

        SetLevelProgress(currentLevel, maxLevel);
        UpgradePurchaseResult evaluation =
            service.EvaluatePurchase(upgrade);

        SetRequirementProgress(
            GetRelevantUnlockResult(upgrade, nextLevel, evaluation));

        if (feedbackText != null)
            feedbackText.text = "";

        ApplyPurchaseState(evaluation, maxed);
    }

    private void HandleBuyClicked()
    {
        if (upgrade == null || UpgradeService.Instance == null)
            return;

        UpgradePurchaseResult result =
            UpgradeService.Instance.TryPurchase(upgrade);

        if (feedbackText != null)
            feedbackText.text = result != null ? result.message : "";

        purchaseCompleted?.Invoke(result);
    }

    private void SetLevelProgress(int currentLevel, int maxLevel)
    {
        float normalized = maxLevel > 0
            ? Mathf.Clamp01(currentLevel / (float)maxLevel)
            : 0f;

        if (levelText != null)
            levelText.text = $"{currentLevel} / {maxLevel}";

        if (levelProgressSlider != null)
        {
            levelProgressSlider.minValue = 0f;
            levelProgressSlider.maxValue = 1f;
            levelProgressSlider.value = normalized;
        }

        if (levelProgressFill != null)
            levelProgressFill.fillAmount = normalized;
    }

    private void SetRequirementProgress(
        UnlockEvaluationResult unlockResult)
    {
        UnlockRequirementEvaluation progress =
            unlockResult != null
                ? unlockResult.GetPrimaryProgress()
                : null;

        bool hasRequirement = unlockResult != null &&
                              unlockResult.requirementResults != null &&
                              unlockResult.requirementResults.Count > 0;

        if (requirementRoot != null)
            requirementRoot.SetActive(hasRequirement);

        if (!hasRequirement)
            return;

        if (requirementText != null)
        {
            if (progress != null)
            {
                requirementText.text = progress.passed
                    ? $"REQUIREMENT COMPLETE • {progress.ProgressText}"
                    : progress.message;
            }
            else
            {
                requirementText.text = unlockResult.isUnlocked
                    ? "REQUIREMENT COMPLETE"
                    : unlockResult.FirstFailureReason;
            }
        }

        float normalized = progress != null
            ? progress.NormalizedProgress
            : unlockResult.isUnlocked ? 1f : 0f;

        if (requirementProgressSlider != null)
        {
            requirementProgressSlider.minValue = 0f;
            requirementProgressSlider.maxValue = 1f;
            requirementProgressSlider.value = normalized;
        }

        if (requirementProgressFill != null)
            requirementProgressFill.fillAmount = normalized;
    }

    private void ApplyPurchaseState(
        UpgradePurchaseResult evaluation,
        bool maxed)
    {
        if (maxed ||
            (evaluation != null &&
             evaluation.status ==
                 UpgradePurchaseStatus.MaximumLevelReached))
        {
            SetButton(false, "MAXED", maxedColor, null);
            return;
        }

        if (evaluation == null)
        {
            SetButton(false, "UNAVAILABLE", lockedColor, null);
            return;
        }

        switch (evaluation.status)
        {
            case UpgradePurchaseStatus.Ready:
                SetButton(
                    true,
                    evaluation.cost <= 0f
                        ? "UNLOCK"
                        : $"BUY FOR {FormatCost(evaluation)}",
                    affordableColor,
                    GetCurrencySprite(evaluation.currency));
                break;

            case UpgradePurchaseStatus.InsufficientGold:
            case UpgradePurchaseStatus.InsufficientDiamonds:
                SetButton(
                    false,
                    $"NEED {FormatCost(evaluation)}",
                    insufficientColor,
                    GetCurrencySprite(evaluation.currency));
                break;

            case UpgradePurchaseStatus.Locked:
                SetButton(false, "LOCKED", lockedColor, null);
                break;

            default:
                SetButton(false, "UNAVAILABLE", lockedColor, null);
                break;
        }
    }

    private void SetButton(
        bool interactable,
        string text,
        Color color,
        Sprite currencySprite)
    {
        if (buyButton != null)
            buyButton.interactable = interactable;

        if (buyButtonText != null)
            buyButtonText.text = text ?? "";

        if (buyButtonBackground != null)
            buyButtonBackground.color = color;

        if (currencyIcon != null)
        {
            currencyIcon.sprite = currencySprite;
            currencyIcon.enabled = currencySprite != null;
        }
    }

    private void SetInvalidState(string message)
    {
        if (upgradeNameText != null)
            upgradeNameText.text = "INVALID UPGRADE";

        if (descriptionText != null)
            descriptionText.text = message ?? "";

        if (requirementRoot != null)
            requirementRoot.SetActive(false);

        SetLevelProgress(0, 0);
        SetButton(false, "UNAVAILABLE", lockedColor, null);
    }

    private static string BuildLevelTitle(
        UpgradeData upgradeData,
        UpgradeLevelData nextLevel,
        int nextLevelNumber)
    {
        if (nextLevel != null &&
            !string.IsNullOrWhiteSpace(nextLevel.levelName))
        {
            return nextLevel.levelName.Trim();
        }

        return $"{upgradeData.DisplayName} {ToRoman(nextLevelNumber)}";
    }

    private static string FormatCost(
        UpgradePurchaseResult evaluation)
    {
        if (evaluation == null)
            return "0";

        if (evaluation.currency == UpgradeCurrency.Diamonds)
            return $"{Mathf.RoundToInt(evaluation.cost):N0}";

        return evaluation.cost >= 1000f
            ? $"{evaluation.cost:N0} G"
            : $"{evaluation.cost:N2} G";
    }

    private Sprite GetCurrencySprite(UpgradeCurrency currency)
    {
        return currency == UpgradeCurrency.Diamonds
            ? diamondIcon
            : goldIcon;
    }

    private static UnlockEvaluationResult GetRelevantUnlockResult(
        UpgradeData data,
        UpgradeLevelData nextLevel,
        UpgradePurchaseResult purchaseEvaluation)
    {
        if (purchaseEvaluation != null &&
            purchaseEvaluation.unlockResult != null)
        {
            return purchaseEvaluation.unlockResult;
        }

        UnlockEvaluationResult baseResult = data != null &&
                                            data.unlockDefinition != null
            ? UnlockEvaluator.Evaluate(data.unlockDefinition)
            : null;

        if (baseResult != null && !baseResult.isUnlocked)
            return baseResult;

        UnlockEvaluationResult levelResult = nextLevel != null &&
                                             nextLevel.additionalRequirement != null
            ? UnlockEvaluator.Evaluate(nextLevel.additionalRequirement)
            : null;

        if (levelResult != null)
            return levelResult;

        return baseResult;
    }

    private static string ToRoman(int value)
    {
        switch (Mathf.Clamp(value, 1, 10))
        {
            case 1: return "I";
            case 2: return "II";
            case 3: return "III";
            case 4: return "IV";
            case 5: return "V";
            case 6: return "VI";
            case 7: return "VII";
            case 8: return "VIII";
            case 9: return "IX";
            case 10: return "X";
            default: return value.ToString();
        }
    }
}
