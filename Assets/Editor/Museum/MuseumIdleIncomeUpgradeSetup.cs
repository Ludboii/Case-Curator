#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates or updates the four M5.1 Museum idle-income UpgradeData assets,
/// their Staircase unlock definitions and their UpgradeCatalog registrations.
/// Re-running the command is safe and preserves assigned icons.
/// </summary>
public static class MuseumIdleIncomeUpgradeSetup
{
    private const string RootFolder =
        "Assets/Data/Upgrades/MuseumIdleIncome";

    private const string UnlockFolder =
        RootFolder + "/Unlocks";

    [MenuItem(
        "Tools/Case Curator/Museum/Apply M5.1 Idle Income Upgrades")]
    public static void ApplyDefaults()
    {
        GameDatabase database = FindTargetDatabase();

        if (database == null)
            return;

        if (database.upgradeCatalog == null)
        {
            EditorUtility.DisplayDialog(
                "Upgrade Catalog Missing",
                "Assign UpgradeCatalog on the selected GameDatabase first.",
                "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Apply M5.1 Idle Income Upgrades",
            "This creates or updates four Museum idle-income upgrades and " +
            "registers them in the assigned UpgradeCatalog. Existing purchased " +
            "levels remain stored by stable upgrade ID.",
            "Apply",
            "Cancel");

        if (!confirmed)
            return;

        EnsureFolder("Assets", "Data");
        EnsureFolder("Assets/Data", "Upgrades");
        EnsureFolder("Assets/Data/Upgrades", "MuseumIdleIncome");
        EnsureFolder(RootFolder, "Unlocks");

        UnlockDefinition visitorIncomeUnlock = GetOrCreateUnlock(
            UnlockFolder + "/Unlock_MuseumVisitorIncome.asset",
            "museum-visitor-income-upgrades",
            "Museum Visitor Income Upgrades",
            "museum-step-10",
            "Claim Museum Staircase Step 10 to unlock visitor-income upgrades.");

        UnlockDefinition diamondCapacityUnlock = GetOrCreateUnlock(
            UnlockFolder + "/Unlock_MuseumDiamondCapacity.asset",
            "museum-diamond-capacity-upgrades",
            "Diamond Endowment Capacity Upgrades",
            "museum-step-75",
            "Claim Museum Staircase Step 75 to unlock Diamond storage upgrades.");

        UpgradeData incomeMultiplier = GetOrCreateUpgrade(
            RootFolder + "/Upgrade_MuseumIncomeMultiplier.asset",
            MuseumIdleIncomeUpgradeUtility.IncomeMultiplierId,
            "Museum Visitor Income",
            "Multiplies both visitor Gold generation and passive Diamond " +
            "generation. Staircase income nodes still determine the base rate.",
            100,
            1f,
            visitorIncomeUnlock,
            BuildIncomeMultiplierLevels());

        UpgradeData offlineHours = GetOrCreateUpgrade(
            RootFolder + "/Upgrade_MuseumOfflineHours.asset",
            MuseumIdleIncomeUpgradeUtility.OfflineHoursId,
            "Museum Offline Duration",
            "Adds eligible offline-generation hours. Normal progression is " +
            "capped at 24 total offline hours.",
            110,
            0f,
            visitorIncomeUnlock,
            BuildOfflineLevels());

        UpgradeData goldCapacity = GetOrCreateUpgrade(
            RootFolder + "/Upgrade_MuseumGoldCapacity.asset",
            MuseumIdleIncomeUpgradeUtility.GoldCapacityId,
            "Museum Gold Storage",
            "Multiplies the maximum unclaimed visitor Gold that can be stored " +
            "before Gold generation pauses.",
            120,
            1f,
            visitorIncomeUnlock,
            BuildGoldCapacityLevels());

        UpgradeData diamondCapacity = GetOrCreateUpgrade(
            RootFolder + "/Upgrade_MuseumDiamondCapacity.asset",
            MuseumIdleIncomeUpgradeUtility.DiamondCapacityId,
            "Diamond Endowment Storage",
            "Multiplies the maximum unclaimed passive Diamonds that can be " +
            "stored before Diamond generation pauses.",
            130,
            1f,
            diamondCapacityUnlock,
            BuildDiamondCapacityLevels());

        RegisterUpgrade(database.upgradeCatalog, incomeMultiplier);
        RegisterUpgrade(database.upgradeCatalog, offlineHours);
        RegisterUpgrade(database.upgradeCatalog, goldCapacity);
        RegisterUpgrade(database.upgradeCatalog, diamondCapacity);

        EditorUtility.SetDirty(database.upgradeCatalog);
        database.upgradeCatalog.RebuildLookup();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = incomeMultiplier;
        EditorGUIUtility.PingObject(incomeMultiplier);

        Debug.Log(
            "Applied M5.1 Museum idle-income upgrades: shared income " +
            "multiplier, offline duration, Gold storage and Diamond storage. " +
            "Upgrade IDs are stable and existing save levels are preserved.",
            database.upgradeCatalog);
    }

    private static UnlockDefinition GetOrCreateUnlock(
        string path,
        string unlockId,
        string displayName,
        string milestoneId,
        string lockedReason)
    {
        UnlockDefinition definition =
            AssetDatabase.LoadAssetAtPath<UnlockDefinition>(path);

        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<UnlockDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        Undo.RecordObject(definition, "Update Museum idle-income unlock");
        definition.unlockId = unlockId;
        definition.featureId = FeatureId.Custom;
        definition.displayName = displayName;
        definition.requirementMode = UnlockRequirementGroupMode.All;
        definition.noRequirementsMeansUnlocked = false;
        definition.requirements = new List<UnlockRequirement>
        {
            new UnlockRequirement
            {
                requirementType =
                    UnlockRequirementType.MuseumMilestoneClaimed,
                requiredMilestoneId = milestoneId,
                lockedReasonOverride = lockedReason
            }
        };

        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static UpgradeData GetOrCreateUpgrade(
        string path,
        string upgradeId,
        string displayName,
        string description,
        int sortOrder,
        float defaultEffect,
        UnlockDefinition unlock,
        List<UpgradeLevelData> levels)
    {
        UpgradeData upgrade =
            AssetDatabase.LoadAssetAtPath<UpgradeData>(path);

        if (upgrade == null)
        {
            upgrade = ScriptableObject.CreateInstance<UpgradeData>();
            AssetDatabase.CreateAsset(upgrade, path);
        }

        Undo.RecordObject(upgrade, "Update Museum idle-income upgrade");
        upgrade.upgradeId = upgradeId;
        upgrade.displayName = displayName;
        upgrade.description = description;
        upgrade.category = UpgradeCategory.Museum;
        upgrade.sortOrder = sortOrder;
        upgrade.hiddenUntilUnlocked = false;
        upgrade.unlockDefinition = unlock;
        upgrade.effectType = UpgradeEffectType.GenericValue;
        upgrade.defaultEffectValue = defaultEffect;
        upgrade.levels = levels;

        EditorUtility.SetDirty(upgrade);
        return upgrade;
    }

    private static List<UpgradeLevelData> BuildIncomeMultiplierLevels()
    {
        float[] costs =
        {
            500f, 1500f, 4000f, 10000f,
            25000f, 60000f, 150000f, 400000f
        };

        float[] effects =
        {
            1.10f, 1.20f, 1.35f, 1.50f,
            1.75f, 2.00f, 2.50f, 3.00f
        };

        string[] numerals =
        {
            "I", "II", "III", "IV", "V", "VI", "VII", "VIII"
        };

        List<UpgradeLevelData> result =
            new List<UpgradeLevelData>(effects.Length);

        for (int i = 0; i < effects.Length; i++)
        {
            result.Add(Level(
                $"Museum Income {numerals[i]}",
                $"Visitor Gold and passive Diamond generation x{effects[i]:0.##}.",
                UpgradeCurrency.Gold,
                costs[i],
                effects[i]));
        }

        return result;
    }

    private static List<UpgradeLevelData> BuildOfflineLevels()
    {
        return new List<UpgradeLevelData>
        {
            Level(
                "Offline Duration I",
                "+2 offline hours.",
                UpgradeCurrency.Gold,
                2000f,
                2f),
            Level(
                "Offline Duration II",
                "+6 offline hours total from this upgrade.",
                UpgradeCurrency.Gold,
                10000f,
                6f),
            Level(
                "Offline Duration III",
                "+12 offline hours total from this upgrade.",
                UpgradeCurrency.Gold,
                50000f,
                12f),
            Level(
                "Offline Duration IV",
                "+20 offline hours total from this upgrade, up to the " +
                "24-hour Museum limit.",
                UpgradeCurrency.Gold,
                250000f,
                20f)
        };
    }

    private static List<UpgradeLevelData> BuildGoldCapacityLevels()
    {
        return new List<UpgradeLevelData>
        {
            Level(
                "Gold Storage I",
                "Unclaimed Museum Gold capacity x1.5.",
                UpgradeCurrency.Gold,
                1500f,
                1.5f),
            Level(
                "Gold Storage II",
                "Unclaimed Museum Gold capacity x2.",
                UpgradeCurrency.Gold,
                7500f,
                2f),
            Level(
                "Gold Storage III",
                "Unclaimed Museum Gold capacity x3.",
                UpgradeCurrency.Gold,
                30000f,
                3f),
            Level(
                "Gold Storage IV",
                "Unclaimed Museum Gold capacity x5.",
                UpgradeCurrency.Gold,
                120000f,
                5f)
        };
    }

    private static List<UpgradeLevelData> BuildDiamondCapacityLevels()
    {
        return new List<UpgradeLevelData>
        {
            Level(
                "Diamond Storage I",
                "Unclaimed passive Diamond capacity x1.5.",
                UpgradeCurrency.Diamonds,
                1f,
                1.5f),
            Level(
                "Diamond Storage II",
                "Unclaimed passive Diamond capacity x2.",
                UpgradeCurrency.Diamonds,
                2f,
                2f),
            Level(
                "Diamond Storage III",
                "Unclaimed passive Diamond capacity x3.",
                UpgradeCurrency.Diamonds,
                4f,
                3f),
            Level(
                "Diamond Storage IV",
                "Unclaimed passive Diamond capacity x5.",
                UpgradeCurrency.Diamonds,
                8f,
                5f)
        };
    }

    private static UpgradeLevelData Level(
        string name,
        string description,
        UpgradeCurrency currency,
        float cost,
        float effect)
    {
        return new UpgradeLevelData
        {
            levelName = name,
            description = description,
            currency = currency,
            cost = Mathf.Max(0f, cost),
            effectValue = effect
        };
    }

    private static void RegisterUpgrade(
        UpgradeCatalog catalog,
        UpgradeData upgrade)
    {
        if (catalog == null || upgrade == null)
            return;

        SerializedObject serializedCatalog =
            new SerializedObject(catalog);

        SerializedProperty upgrades =
            serializedCatalog.FindProperty("upgrades");

        if (upgrades == null)
            return;

        for (int i = upgrades.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty element =
                upgrades.GetArrayElementAtIndex(i);

            UpgradeData existing =
                element.objectReferenceValue as UpgradeData;

            if (existing == upgrade)
                return;

            if (existing != null &&
                string.Equals(
                    existing.upgradeId,
                    upgrade.upgradeId,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                element.objectReferenceValue = upgrade;
                serializedCatalog.ApplyModifiedProperties();
                return;
            }
        }

        int index = upgrades.arraySize;
        upgrades.InsertArrayElementAtIndex(index);
        upgrades.GetArrayElementAtIndex(index).objectReferenceValue = upgrade;
        serializedCatalog.ApplyModifiedProperties();
    }

    private static GameDatabase FindTargetDatabase()
    {
        if (Selection.activeObject is GameDatabase selected)
            return selected;

        string[] guids = AssetDatabase.FindAssets("t:GameDatabase");

        if (guids == null || guids.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "GameDatabase Not Found",
                "Create or select a GameDatabase asset, then run this command again.",
                "OK");
            return null;
        }

        if (guids.Length > 1)
        {
            EditorUtility.DisplayDialog(
                "Select GameDatabase",
                "More than one GameDatabase exists. Select the intended asset " +
                "in the Project window, then run the command again.",
                "OK");
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<GameDatabase>(path);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
