using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry for every UpgradeData asset used by the game.
/// IDs are matched case-insensitively and duplicate IDs are rejected.
/// </summary>
[CreateAssetMenu(
    fileName = "UpgradeCatalog",
    menuName = "Case Curator/Upgrades/Upgrade Catalog")]
public class UpgradeCatalog : ScriptableObject
{
    [SerializeField]
    private List<UpgradeData> upgrades =
        new List<UpgradeData>();

    private Dictionary<string, UpgradeData> upgradeById;

    public IReadOnlyList<UpgradeData> Upgrades => upgrades;

    public UpgradeData GetUpgradeById(string upgradeId)
    {
        if (string.IsNullOrWhiteSpace(upgradeId))
            return null;

        EnsureLookup();
        upgradeById.TryGetValue(
            upgradeId.Trim(),
            out UpgradeData upgrade);

        return upgrade;
    }

    public bool TryGetUpgrade(
        string upgradeId,
        out UpgradeData upgrade)
    {
        upgrade = GetUpgradeById(upgradeId);
        return upgrade != null;
    }

    public bool ValidateCatalog(out List<string> errors)
    {
        errors = new List<string>();
        HashSet<string> usedIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (upgrades == null)
        {
            errors.Add("Upgrade catalog list is null.");
            return false;
        }

        for (int i = 0; i < upgrades.Count; i++)
        {
            UpgradeData upgrade = upgrades[i];

            if (upgrade == null)
            {
                errors.Add($"Catalog entry {i + 1} is empty.");
                continue;
            }

            if (!upgrade.IsDefinitionValid(out string definitionError))
                errors.Add(definitionError);

            string id = upgrade.upgradeId != null
                ? upgrade.upgradeId.Trim()
                : "";

            if (!string.IsNullOrWhiteSpace(id) && !usedIds.Add(id))
            {
                errors.Add(
                    $"Duplicate upgradeId '{id}' in {name}.");
            }
        }

        return errors.Count == 0;
    }

    public void RebuildLookup()
    {
        upgradeById = new Dictionary<string, UpgradeData>(
            StringComparer.OrdinalIgnoreCase);

        if (upgrades == null)
            return;

        for (int i = 0; i < upgrades.Count; i++)
        {
            UpgradeData upgrade = upgrades[i];

            if (upgrade == null ||
                string.IsNullOrWhiteSpace(upgrade.upgradeId))
            {
                continue;
            }

            string id = upgrade.upgradeId.Trim();

            if (upgradeById.ContainsKey(id))
            {
                Debug.LogWarning(
                    $"UpgradeCatalog '{name}' contains duplicate ID '{id}'. " +
                    "The first asset will be used.",
                    this);

                continue;
            }

            upgradeById.Add(id, upgrade);
        }
    }

    private void EnsureLookup()
    {
        if (upgradeById == null)
            RebuildLookup();
    }

    private void OnEnable()
    {
        RebuildLookup();
    }

    private void OnValidate()
    {
        if (upgrades == null)
            upgrades = new List<UpgradeData>();

        RebuildLookup();
    }
}
