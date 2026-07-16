using System;
using System.Collections.Generic;

/// <summary>
/// Central read/write helper for UpgradeStateSaveData.upgradeLevels.
/// This keeps ID matching, duplicate cleanup and level normalization consistent.
/// </summary>
public static class UpgradeSaveUtility
{
    public static int GetLevel(
        UpgradeStateSaveData state,
        string upgradeId)
    {
        if (state == null ||
            state.upgradeLevels == null ||
            string.IsNullOrWhiteSpace(upgradeId))
        {
            return 0;
        }

        string target = upgradeId.Trim();
        int highestLevel = 0;

        for (int i = 0; i < state.upgradeLevels.Count; i++)
        {
            UpgradeLevelSaveData entry = state.upgradeLevels[i];

            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.upgradeId))
            {
                continue;
            }

            if (!string.Equals(
                    entry.upgradeId.Trim(),
                    target,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            highestLevel = Math.Max(
                highestLevel,
                Math.Max(0, entry.level));
        }

        return highestLevel;
    }

    public static int GetLevel(
        UpgradeStateSaveData state,
        UpgradeData upgrade)
    {
        return upgrade != null
            ? GetLevel(state, upgrade.upgradeId)
            : 0;
    }

    public static bool SetLevel(
        UpgradeStateSaveData state,
        string upgradeId,
        int level)
    {
        if (state == null || string.IsNullOrWhiteSpace(upgradeId))
            return false;

        if (state.upgradeLevels == null)
        {
            state.upgradeLevels =
                new List<UpgradeLevelSaveData>();
        }

        string target = upgradeId.Trim();
        int normalizedLevel = Math.Max(0, level);
        int previousLevel = GetLevel(state, target);

        for (int i = state.upgradeLevels.Count - 1; i >= 0; i--)
        {
            UpgradeLevelSaveData entry = state.upgradeLevels[i];

            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.upgradeId))
            {
                continue;
            }

            if (string.Equals(
                    entry.upgradeId.Trim(),
                    target,
                    StringComparison.OrdinalIgnoreCase))
            {
                state.upgradeLevels.RemoveAt(i);
            }
        }

        if (normalizedLevel > 0)
        {
            state.upgradeLevels.Add(
                new UpgradeLevelSaveData
                {
                    upgradeId = target,
                    level = normalizedLevel
                });
        }

        return previousLevel != normalizedLevel;
    }

    /// <summary>
    /// Removes null/empty/zero entries and merges duplicate IDs by keeping the
    /// highest saved level. Unknown upgrade IDs are intentionally preserved.
    /// </summary>
    public static bool Normalize(UpgradeStateSaveData state)
    {
        if (state == null)
            return false;

        if (state.upgradeLevels == null)
        {
            state.upgradeLevels =
                new List<UpgradeLevelSaveData>();
            return true;
        }

        Dictionary<string, int> highestById =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        List<string> originalOrder = new List<string>();
        bool changed = false;

        for (int i = 0; i < state.upgradeLevels.Count; i++)
        {
            UpgradeLevelSaveData entry = state.upgradeLevels[i];

            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.upgradeId) ||
                entry.level <= 0)
            {
                changed = true;
                continue;
            }

            string id = entry.upgradeId.Trim();
            int level = Math.Max(0, entry.level);

            if (!string.Equals(
                    entry.upgradeId,
                    id,
                    StringComparison.Ordinal))
            {
                changed = true;
            }

            if (!highestById.ContainsKey(id))
            {
                highestById.Add(id, level);
                originalOrder.Add(id);
            }
            else
            {
                highestById[id] = Math.Max(
                    highestById[id],
                    level);
                changed = true;
            }
        }

        if (!changed &&
            state.upgradeLevels.Count == originalOrder.Count)
        {
            return false;
        }

        state.upgradeLevels.Clear();

        for (int i = 0; i < originalOrder.Count; i++)
        {
            string id = originalOrder[i];

            state.upgradeLevels.Add(
                new UpgradeLevelSaveData
                {
                    upgradeId = id,
                    level = highestById[id]
                });
        }

        return true;
    }
}
