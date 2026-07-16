using System;

public enum UpgradePurchaseStatus
{
    Ready,
    Success,
    MissingUpgrade,
    MissingCatalog,
    SaveUnavailable,
    InvalidDefinition,
    MaximumLevelReached,
    Locked,
    InvalidCost,
    InsufficientGold,
    InsufficientDiamonds,
    CurrencySpendFailed,
    SaveStateUpdateFailed
}

/// <summary>
/// Complete purchase evaluation/result for gameplay and UI. EvaluatePurchase
/// returns Ready without spending; TryPurchase returns Success after mutation.
/// </summary>
[Serializable]
public class UpgradePurchaseResult
{
    public bool success;
    public UpgradePurchaseStatus status;
    public string message;

    public UpgradeData upgrade;
    public UpgradeLevelData levelData;

    public int previousLevel;
    public int newLevel;

    public UpgradeCurrency currency;
    public float cost;

    public UnlockEvaluationResult unlockResult;

    public bool CanPurchase =>
        status == UpgradePurchaseStatus.Ready;

    public static UpgradePurchaseResult Ready(
        UpgradeData upgrade,
        UpgradeLevelData levelData,
        int previousLevel)
    {
        return new UpgradePurchaseResult
        {
            success = false,
            status = UpgradePurchaseStatus.Ready,
            message = "Ready to purchase.",
            upgrade = upgrade,
            levelData = levelData,
            previousLevel = previousLevel,
            newLevel = previousLevel + 1,
            currency = levelData != null
                ? levelData.currency
                : UpgradeCurrency.Gold,
            cost = levelData != null
                ? levelData.cost
                : 0f
        };
    }

    public static UpgradePurchaseResult Failed(
        UpgradePurchaseStatus status,
        string message,
        UpgradeData upgrade = null,
        int previousLevel = 0,
        UpgradeLevelData levelData = null,
        UnlockEvaluationResult unlockResult = null)
    {
        return new UpgradePurchaseResult
        {
            success = false,
            status = status,
            message = message ?? "Upgrade purchase failed.",
            upgrade = upgrade,
            levelData = levelData,
            previousLevel = Math.Max(0, previousLevel),
            newLevel = Math.Max(0, previousLevel),
            currency = levelData != null
                ? levelData.currency
                : UpgradeCurrency.Gold,
            cost = levelData != null
                ? levelData.cost
                : 0f,
            unlockResult = unlockResult
        };
    }

    public static UpgradePurchaseResult Completed(
        UpgradePurchaseResult readyResult)
    {
        if (readyResult == null)
        {
            return Failed(
                UpgradePurchaseStatus.SaveStateUpdateFailed,
                "Upgrade purchase completed without a valid result.");
        }

        return new UpgradePurchaseResult
        {
            success = true,
            status = UpgradePurchaseStatus.Success,
            message =
                $"Purchased {readyResult.upgrade?.DisplayName ?? "upgrade"} " +
                $"level {readyResult.newLevel}.",
            upgrade = readyResult.upgrade,
            levelData = readyResult.levelData,
            previousLevel = readyResult.previousLevel,
            newLevel = readyResult.newLevel,
            currency = readyResult.currency,
            cost = readyResult.cost,
            unlockResult = readyResult.unlockResult
        };
    }
}
