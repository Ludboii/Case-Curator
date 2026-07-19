using System;
using System.Collections.Generic;
using UnityEngine;

public enum UpgradeCategory
{
    General,
    Opening,
    Inventory,
    Museum,
    GiftDesk,
    TrophyRoom,
    Tradeups,
    AutomatedAcquisitions,
    Debug,
    Minigames,
    SingleUse,
    Others
}

public enum UpgradeCurrency
{
    Gold,
    Diamonds
}

/// <summary>
/// Most upgrades are queried by gameplay systems through GenericValue.
/// The other values synchronize existing legacy save fields after purchase.
/// </summary>
public enum UpgradeEffectType
{
    GenericValue,
    OpeningSlotsOwned,
    TradeupFloatTuningLevel
}

[Serializable]
public class UpgradeLevelData
{
    [Header("Presentation")]
    public string levelName;

    [TextArea(1, 4)]
    public string description;

    [Header("Purchase")]
    public UpgradeCurrency currency = UpgradeCurrency.Gold;

    [Min(0f)]
    public float cost;

    [Tooltip(
        "Optional requirement applied only when purchasing this level. " +
        "The UpgradeData base requirement is evaluated first.")]
    public UnlockDefinition additionalRequirement;

    [Header("Effect")]
    [Tooltip(
        "Absolute effect value at this level. This is not automatically added " +
        "to the previous level. Consumers decide how the value is interpreted.")]
    public float effectValue;

    public string GetDisplayName(int level)
    {
        return !string.IsNullOrWhiteSpace(levelName)
            ? levelName.Trim()
            : $"Level {Mathf.Max(1, level)}";
    }
}

/// <summary>
/// One complete upgrade definition. Level list index 0 represents purchased
/// level 1, index 1 represents level 2, and so on.
/// </summary>
[CreateAssetMenu(
    fileName = "Upgrade",
    menuName = "Case Curator/Upgrades/Upgrade")]
public class UpgradeData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip(
        "Stable save ID. Do not change this after the upgrade has shipped.")]
    public string upgradeId;

    public string displayName;

    [TextArea(2, 6)]
    public string description;

    public Sprite icon;
    public UpgradeCategory category = UpgradeCategory.General;
    public int sortOrder;

    [Header("Visibility and Base Unlock")]
    public bool hiddenUntilUnlocked;

    [Tooltip(
        "Optional requirement that must pass before any level can be bought.")]
    public UnlockDefinition unlockDefinition;

    [Header("Effect")]
    public UpgradeEffectType effectType = UpgradeEffectType.GenericValue;

    [Tooltip("Effect value returned while the upgrade is level 0.")]
    public float defaultEffectValue;

    [Header("Levels")]
    public List<UpgradeLevelData> levels =
        new List<UpgradeLevelData>();

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(displayName)
            ? displayName.Trim()
            : name;

    public int MaxLevel => levels != null ? levels.Count : 0;

    public UpgradeLevelData GetLevelData(int level)
    {
        if (levels == null || level < 1 || level > levels.Count)
            return null;

        return levels[level - 1];
    }

    public UpgradeLevelData GetNextLevelData(int currentLevel)
    {
        return GetLevelData(Mathf.Max(0, currentLevel) + 1);
    }

    public float GetEffectValue(int level)
    {
        if (level <= 0 || levels == null || levels.Count == 0)
            return defaultEffectValue;

        int clampedLevel = Mathf.Clamp(level, 1, levels.Count);
        UpgradeLevelData data = levels[clampedLevel - 1];

        return data != null
            ? data.effectValue
            : defaultEffectValue;
    }

    public bool IsDefinitionValid(out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(upgradeId))
        {
            errorMessage = $"{name} has no upgradeId.";
            return false;
        }

        if (levels == null || levels.Count == 0)
        {
            errorMessage = $"{DisplayName} has no configured levels.";
            return false;
        }

        for (int i = 0; i < levels.Count; i++)
        {
            UpgradeLevelData level = levels[i];

            if (level == null)
            {
                errorMessage =
                    $"{DisplayName} level {i + 1} is empty.";
                return false;
            }

            if (float.IsNaN(level.cost) ||
                float.IsInfinity(level.cost) ||
                level.cost < 0f)
            {
                errorMessage =
                    $"{DisplayName} level {i + 1} has an invalid cost.";
                return false;
            }

            if (level.currency == UpgradeCurrency.Diamonds &&
                !Mathf.Approximately(level.cost, Mathf.Round(level.cost)))
            {
                errorMessage =
                    $"{DisplayName} level {i + 1} uses Diamonds but its " +
                    "cost is not a whole number.";
                return false;
            }

            if (float.IsNaN(level.effectValue) ||
                float.IsInfinity(level.effectValue))
            {
                errorMessage =
                    $"{DisplayName} level {i + 1} has an invalid effect value.";
                return false;
            }
        }

        errorMessage = "";
        return true;
    }

    private void OnValidate()
    {
        if (upgradeId != null)
            upgradeId = upgradeId.Trim();

        if (displayName != null)
            displayName = displayName.Trim();

        if (levels == null)
            levels = new List<UpgradeLevelData>();

        for (int i = 0; i < levels.Count; i++)
        {
            UpgradeLevelData level = levels[i];

            if (level == null)
                continue;

            level.cost = Mathf.Max(0f, level.cost);
        }
    }
}
