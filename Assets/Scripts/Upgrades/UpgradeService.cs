using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single gameplay authority for reading upgrade levels, evaluating purchases,
/// spending currency and writing generic upgrade save state.
/// UI should never spend currency or edit UpgradeStateSaveData directly.
/// </summary>
[DisallowMultipleComponent]
public class UpgradeService : MonoBehaviour
{
    public static UpgradeService Instance { get; private set; }

    [Header("Catalog")]
    [Tooltip(
        "Optional direct catalog assignment. When empty, the service uses " +
        "SaveManager.database.upgradeCatalog.")]
    [SerializeField] private UpgradeCatalog catalog;

    [Header("Lifetime")]
    [SerializeField] private bool persistAcrossScenes = true;

    [Header("Diagnostics")]
    [SerializeField] private bool normalizeSavedLevelsOnStart = true;
    [SerializeField] private bool logSuccessfulPurchases = true;

    public event Action<UpgradeData, int, int> OnUpgradeLevelChanged;
    public event Action OnUpgradeStateChanged;

    public UpgradeCatalog Catalog => ResolveCatalog();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "Duplicate UpgradeService found. Destroying: " +
                gameObject.name);

            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (normalizeSavedLevelsOnStart)
            NormalizeSavedLevels();

        ApplyAllRuntimeEffectsFromSave();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetCatalog(UpgradeCatalog value)
    {
        catalog = value;

        if (catalog != null)
            catalog.RebuildLookup();

        NormalizeSavedLevels();
        ApplyAllRuntimeEffectsFromSave();
        OnUpgradeStateChanged?.Invoke();
    }

    public UpgradeData GetUpgrade(string upgradeId)
    {
        UpgradeCatalog activeCatalog = ResolveCatalog();

        return activeCatalog != null
            ? activeCatalog.GetUpgradeById(upgradeId)
            : null;
    }

    public IReadOnlyList<UpgradeData> GetAllUpgrades()
    {
        UpgradeCatalog activeCatalog = ResolveCatalog();

        return activeCatalog != null
            ? activeCatalog.Upgrades
            : Array.Empty<UpgradeData>();
    }

    public int GetLevel(string upgradeId)
    {
        SaveManager save = SaveManager.Instance;

        return save != null
            ? UpgradeSaveUtility.GetLevel(save.Upgrades, upgradeId)
            : 0;
    }

    public int GetLevel(UpgradeData upgrade)
    {
        return upgrade != null
            ? GetLevel(upgrade.upgradeId)
            : 0;
    }

    public bool IsMaxLevel(string upgradeId)
    {
        UpgradeData upgrade = GetUpgrade(upgradeId);
        return IsMaxLevel(upgrade);
    }

    public bool IsMaxLevel(UpgradeData upgrade)
    {
        return upgrade != null &&
               upgrade.MaxLevel > 0 &&
               GetLevel(upgrade) >= upgrade.MaxLevel;
    }

    public UpgradeLevelData GetNextLevelData(string upgradeId)
    {
        UpgradeData upgrade = GetUpgrade(upgradeId);

        return upgrade != null
            ? upgrade.GetNextLevelData(GetLevel(upgrade))
            : null;
    }

    public float GetCurrentEffect(string upgradeId)
    {
        UpgradeData upgrade = GetUpgrade(upgradeId);

        return upgrade != null
            ? upgrade.GetEffectValue(GetLevel(upgrade))
            : 0f;
    }

    public float GetCurrentEffect(UpgradeData upgrade)
    {
        return upgrade != null
            ? upgrade.GetEffectValue(GetLevel(upgrade))
            : 0f;
    }

    public float GetEffectAtLevel(
        string upgradeId,
        int level)
    {
        UpgradeData upgrade = GetUpgrade(upgradeId);

        return upgrade != null
            ? upgrade.GetEffectValue(level)
            : 0f;
    }

    public float GetNextCost(string upgradeId)
    {
        UpgradeLevelData level = GetNextLevelData(upgradeId);
        return level != null ? level.cost : 0f;
    }

    public UpgradeCurrency GetNextCurrency(string upgradeId)
    {
        UpgradeLevelData level = GetNextLevelData(upgradeId);

        return level != null
            ? level.currency
            : UpgradeCurrency.Gold;
    }

    public UpgradePurchaseResult EvaluatePurchase(string upgradeId)
    {
        UpgradeCatalog activeCatalog = ResolveCatalog();

        if (activeCatalog == null)
        {
            return UpgradePurchaseResult.Failed(
                UpgradePurchaseStatus.MissingCatalog,
                "Upgrade Catalog is not assigned.");
        }

        UpgradeData upgrade = activeCatalog.GetUpgradeById(upgradeId);

        if (upgrade == null)
        {
            return UpgradePurchaseResult.Failed(
                UpgradePurchaseStatus.MissingUpgrade,
                $"Upgrade '{upgradeId}' was not found in the catalog.");
        }

        return EvaluatePurchase(upgrade);
    }

    public UpgradePurchaseResult EvaluatePurchase(UpgradeData upgrade)
    {
        if (upgrade == null)
        {
            return UpgradePurchaseResult.Failed(
                UpgradePurchaseStatus.MissingUpgrade,
                "No UpgradeData was supplied.");
        }

        SaveManager save = SaveManager.Instance;

        if (save == null)
        {
            return UpgradePurchaseResult.Failed(
                UpgradePurchaseStatus.SaveUnavailable,
                "SaveManager is unavailable.",
                upgrade);
        }

        if (!upgrade.IsDefinitionValid(out string definitionError))
        {
            return UpgradePurchaseResult.Failed(
                UpgradePurchaseStatus.InvalidDefinition,
                definitionError,
                upgrade,
                GetLevel(upgrade));
        }

        int currentLevel = GetLevel(upgrade);

        if (currentLevel >= upgrade.MaxLevel)
        {
            return UpgradePurchaseResult.Failed(
                UpgradePurchaseStatus.MaximumLevelReached,
                $"{upgrade.DisplayName} is already at maximum level.",
                upgrade,
                currentLevel);
        }

        UpgradeLevelData nextLevel =
            upgrade.GetLevelData(currentLevel + 1);

        if (nextLevel == null)
        {
            return UpgradePurchaseResult.Failed(
                UpgradePurchaseStatus.InvalidDefinition,
                $"{upgrade.DisplayName} has no data for level " +
                $"{currentLevel + 1}.",
                upgrade,
                currentLevel);
        }

        UnlockEvaluationResult baseUnlock =
            EvaluateUnlock(upgrade.unlockDefinition);

        if (baseUnlock != null && !baseUnlock.isUnlocked)
        {
            return UpgradePurchaseResult.Failed(
                UpgradePurchaseStatus.Locked,
                baseUnlock.FirstFailureReason,
                upgrade,
                currentLevel,
                nextLevel,
                baseUnlock);
        }

        UnlockEvaluationResult levelUnlock =
            EvaluateUnlock(nextLevel.additionalRequirement);

        if (levelUnlock != null && !levelUnlock.isUnlocked)
        {
            return UpgradePurchaseResult.Failed(
                UpgradePurchaseStatus.Locked,
                levelUnlock.FirstFailureReason,
                upgrade,
                currentLevel,
                nextLevel,
                levelUnlock);
        }

        if (!IsCostValid(nextLevel, out string costError))
        {
            return UpgradePurchaseResult.Failed(
                UpgradePurchaseStatus.InvalidCost,
                costError,
                upgrade,
                currentLevel,
                nextLevel);
        }

        if (nextLevel.currency == UpgradeCurrency.Gold &&
            save.Gold + 0.0001f < nextLevel.cost)
        {
            return UpgradePurchaseResult.Failed(
                UpgradePurchaseStatus.InsufficientGold,
                $"Requires {nextLevel.cost:N2} Gold. " +
                $"Current Gold: {save.Gold:N2}.",
                upgrade,
                currentLevel,
                nextLevel);
        }

        if (nextLevel.currency == UpgradeCurrency.Diamonds)
        {
            int diamondCost = Mathf.RoundToInt(nextLevel.cost);

            if (save.Diamonds < diamondCost)
            {
                return UpgradePurchaseResult.Failed(
                    UpgradePurchaseStatus.InsufficientDiamonds,
                    $"Requires {diamondCost:N0} Diamonds. " +
                    $"Current Diamonds: {save.Diamonds:N0}.",
                    upgrade,
                    currentLevel,
                    nextLevel);
            }
        }

        return UpgradePurchaseResult.Ready(
            upgrade,
            nextLevel,
            currentLevel);
    }

    public UpgradePurchaseResult TryPurchase(string upgradeId)
    {
        UpgradePurchaseResult evaluation =
            EvaluatePurchase(upgradeId);

        return TryCompletePurchase(evaluation);
    }

    public UpgradePurchaseResult TryPurchase(UpgradeData upgrade)
    {
        UpgradePurchaseResult evaluation =
            EvaluatePurchase(upgrade);

        return TryCompletePurchase(evaluation);
    }

    public bool NormalizeSavedLevels()
    {
        SaveManager save = SaveManager.Instance;

        if (save == null || save.Upgrades == null)
            return false;

        bool changed = UpgradeSaveUtility.Normalize(save.Upgrades);
        UpgradeCatalog activeCatalog = ResolveCatalog();

        if (activeCatalog != null &&
            save.Upgrades.upgradeLevels != null)
        {
            List<UpgradeLevelSaveData> entries =
                save.Upgrades.upgradeLevels;

            for (int i = 0; i < entries.Count; i++)
            {
                UpgradeLevelSaveData entry = entries[i];

                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.upgradeId))
                {
                    continue;
                }

                UpgradeData upgrade =
                    activeCatalog.GetUpgradeById(entry.upgradeId);

                if (upgrade == null || upgrade.MaxLevel <= 0)
                    continue;

                int clampedLevel = Mathf.Clamp(
                    entry.level,
                    0,
                    upgrade.MaxLevel);

                if (entry.level == clampedLevel)
                    continue;

                entry.level = clampedLevel;
                changed = true;
            }
        }

        if (changed)
        {
            save.MarkDirty();
            OnUpgradeStateChanged?.Invoke();
        }

        return changed;
    }

    /// <summary>
    /// Reapplies special compatibility effects from generic saved levels.
    /// GenericValue upgrades need no mutation; consumers query their values.
    /// </summary>
    public void ApplyAllRuntimeEffectsFromSave()
    {
        SaveManager save = SaveManager.Instance;
        UpgradeCatalog activeCatalog = ResolveCatalog();

        if (save == null || activeCatalog == null)
            return;

        int openingSlots = save.OwnedOpeningSlots;
        int floatTuning = save.Tradeups != null
            ? Mathf.Max(0, save.Tradeups.floatTuningLevel)
            : 0;

        IReadOnlyList<UpgradeData> allUpgrades =
            activeCatalog.Upgrades;

        for (int i = 0; i < allUpgrades.Count; i++)
        {
            UpgradeData upgrade = allUpgrades[i];

            if (upgrade == null)
                continue;

            int level = GetLevel(upgrade);

            if (level <= 0)
                continue;

            int effect = Mathf.RoundToInt(
                upgrade.GetEffectValue(level));

            switch (upgrade.effectType)
            {
                case UpgradeEffectType.OpeningSlotsOwned:
                    openingSlots = Mathf.Max(openingSlots, effect);
                    break;

                case UpgradeEffectType.TradeupFloatTuningLevel:
                    floatTuning = Mathf.Max(floatTuning, effect);
                    break;
            }
        }

        save.SetOwnedOpeningSlots(openingSlots);
        save.SetTradeupFloatTuningLevel(floatTuning);
    }

    [ContextMenu("Validate Upgrade Catalog")]
    private void ValidateCatalogInEditor()
    {
        UpgradeCatalog activeCatalog = ResolveCatalog();

        if (activeCatalog == null)
        {
            Debug.LogError(
                "UpgradeService: No UpgradeCatalog is assigned.",
                this);
            return;
        }

        if (activeCatalog.ValidateCatalog(out List<string> errors))
        {
            Debug.Log(
                $"Upgrade Catalog '{activeCatalog.name}' is valid. " +
                $"Entries: {activeCatalog.Upgrades.Count}.",
                activeCatalog);
            return;
        }

        for (int i = 0; i < errors.Count; i++)
            Debug.LogError(errors[i], activeCatalog);
    }

    private UpgradePurchaseResult TryCompletePurchase(
        UpgradePurchaseResult evaluation)
    {
        if (evaluation == null || !evaluation.CanPurchase)
            return evaluation;

        SaveManager save = SaveManager.Instance;

        if (save == null)
        {
            return UpgradePurchaseResult.Failed(
                UpgradePurchaseStatus.SaveUnavailable,
                "SaveManager became unavailable before purchase.",
                evaluation.upgrade,
                evaluation.previousLevel,
                evaluation.levelData);
        }

        bool spent = SpendCurrency(
            save,
            evaluation.currency,
            evaluation.cost);

        if (!spent)
        {
            return UpgradePurchaseResult.Failed(
                UpgradePurchaseStatus.CurrencySpendFailed,
                "Currency could not be spent.",
                evaluation.upgrade,
                evaluation.previousLevel,
                evaluation.levelData);
        }

        bool levelChanged = UpgradeSaveUtility.SetLevel(
            save.Upgrades,
            evaluation.upgrade.upgradeId,
            evaluation.newLevel);

        if (!levelChanged)
        {
            RefundCurrency(
                save,
                evaluation.currency,
                evaluation.cost);

            return UpgradePurchaseResult.Failed(
                UpgradePurchaseStatus.SaveStateUpdateFailed,
                "Upgrade save state could not be updated. Currency was refunded.",
                evaluation.upgrade,
                evaluation.previousLevel,
                evaluation.levelData);
        }

        save.MarkDirty();
        ApplyRuntimeEffect(
            evaluation.upgrade,
            evaluation.newLevel);

        OnUpgradeLevelChanged?.Invoke(
            evaluation.upgrade,
            evaluation.previousLevel,
            evaluation.newLevel);

        OnUpgradeStateChanged?.Invoke();

        UpgradePurchaseResult completed =
            UpgradePurchaseResult.Completed(evaluation);

        if (logSuccessfulPurchases)
        {
            Debug.Log(
                $"Upgrade purchased: {evaluation.upgrade.DisplayName} " +
                $"{evaluation.previousLevel} → {evaluation.newLevel}. " +
                $"Cost: {evaluation.cost:0.##} {evaluation.currency}.");
        }

        return completed;
    }

    private void ApplyRuntimeEffect(
        UpgradeData upgrade,
        int level)
    {
        if (upgrade == null || SaveManager.Instance == null)
            return;

        int value = Mathf.RoundToInt(
            upgrade.GetEffectValue(level));

        switch (upgrade.effectType)
        {
            case UpgradeEffectType.OpeningSlotsOwned:
                SaveManager.Instance.SetOwnedOpeningSlots(
                    Mathf.Max(
                        SaveManager.Instance.OwnedOpeningSlots,
                        value));
                break;

            case UpgradeEffectType.TradeupFloatTuningLevel:
                int current = SaveManager.Instance.Tradeups != null
                    ? SaveManager.Instance.Tradeups.floatTuningLevel
                    : 0;

                SaveManager.Instance.SetTradeupFloatTuningLevel(
                    Mathf.Max(current, value));
                break;
        }
    }

    private UpgradeCatalog ResolveCatalog()
    {
        if (catalog != null)
            return catalog;

        SaveManager save = SaveManager.Instance;

        if (save != null &&
            save.database != null &&
            save.database.upgradeCatalog != null)
        {
            catalog = save.database.upgradeCatalog;
        }

        return catalog;
    }

    private static UnlockEvaluationResult EvaluateUnlock(
        UnlockDefinition definition)
    {
        return definition != null
            ? UnlockEvaluator.Evaluate(definition)
            : null;
    }

    private static bool IsCostValid(
        UpgradeLevelData level,
        out string error)
    {
        if (level == null)
        {
            error = "Upgrade level data is missing.";
            return false;
        }

        if (float.IsNaN(level.cost) ||
            float.IsInfinity(level.cost) ||
            level.cost < 0f)
        {
            error = "Upgrade cost is invalid.";
            return false;
        }

        if (level.currency == UpgradeCurrency.Diamonds &&
            !Mathf.Approximately(level.cost, Mathf.Round(level.cost)))
        {
            error = "Diamond upgrade costs must be whole numbers.";
            return false;
        }

        error = "";
        return true;
    }

    private static bool SpendCurrency(
        SaveManager save,
        UpgradeCurrency currency,
        float amount)
    {
        if (amount <= 0f)
            return true;

        switch (currency)
        {
            case UpgradeCurrency.Diamonds:
                return save.SpendDiamonds(Mathf.RoundToInt(amount));

            default:
                return save.SpendGold(amount);
        }
    }

    private static void RefundCurrency(
        SaveManager save,
        UpgradeCurrency currency,
        float amount)
    {
        if (save == null || amount <= 0f)
            return;

        if (currency == UpgradeCurrency.Diamonds)
            save.AddDiamonds(Mathf.RoundToInt(amount));
        else
            save.AddGold(amount);
    }
}
