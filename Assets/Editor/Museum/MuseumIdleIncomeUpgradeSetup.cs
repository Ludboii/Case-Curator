#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates or updates the five M5.1 Museum idle-income UpgradeData assets,
/// their Staircase unlock definitions and their UpgradeCatalog registrations.
/// Re-running the command is safe and preserves assigned icons.
/// </summary>
public static class MuseumIdleIncomeUpgradeSetup
{
    private const string RootFolder =
        "Assets/Data/Upgrades/MuseumIdleIncome";

    private const string UnlockFolder =
        RootFolder + "/Unlocks";

    private static readonly float[] IncomeEffects =
    {
        1.05f, 1.10f, 1.15f, 1.20f, 1.25f, 1.30f,
        1.40f, 1.50f, 1.60f, 1.70f, 1.80f, 1.90f,
        2.00f, 2.15f, 2.30f, 2.50f, 2.75f, 3.00f,
        3.25f, 3.50f, 4.00f, 4.50f, 5.00f
    };

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

        if (database.museumBalance == null)
        {
            EditorUtility.DisplayDialog(
                "Museum Balance Missing",
                "Assign MuseumBalanceData on the selected GameDatabase first.",
                "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Apply M5.1 Idle Income Upgrades",
            "This creates or updates five Museum idle-income upgrades, " +
            "registers them in the assigned UpgradeCatalog and sets the base " +
            "offline duration to 1 hour. Existing purchased levels remain " +
            "stored by stable upgrade ID.",
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
            "Claim Museum Staircase Step 10 to unlock Gold visitor-income, " +
            "offline-duration and Gold-storage upgrades.");

        UnlockDefinition diamondIncomeUnlock = GetOrCreateUnlock(
            UnlockFolder + "/Unlock_MuseumDiamondCapacity.asset",
            "museum-diamond-endowment-upgrades",
            "Diamond Endowment Upgrades",
            "museum-step-75",
            "Claim Museum Staircase Step 75 to unlock Diamond income and " +
            "Diamond-storage upgrades.");

        UpgradeData goldIncome = GetOrCreateUpgrade(
            RootFolder + "/Upgrade_MuseumGoldIncomeMultiplier.asset",
            MuseumIdleIncomeUpgradeUtility.GoldIncomeMultiplierId,
            "Museum Gold Visitor Income",
            "Multiplies visitor Gold generation. Staircase income nodes still " +
            "determine the base rate.",
            100,
            1f,
            visitorIncomeUnlock,
            BuildIncomeMultiplierLevels(
                "Gold Visitor Income",
                UpgradeCurrency.Gold,
                BuildGoldIncomeCosts(),
                "Visitor Gold generation"));

        UpgradeData diamondIncome = GetOrCreateUpgrade(
            RootFolder + "/Upgrade_MuseumDiamondIncomeMultiplier.asset",
            MuseumIdleIncomeUpgradeUtility.DiamondIncomeMultiplierId,
            "Diamond Endowment Income",
            "Multiplies passive Diamond generation after the Diamond Endowment " +
            "has been unlocked at Museum Staircase Step 75.",
            110,
            1f,
            diamondIncomeUnlock,
            BuildIncomeMultiplierLevels(
                "Diamond Endowment Income",
                UpgradeCurrency.Diamonds,
                BuildDiamondIncomeCosts(),
                "Passive Diamond generation"));

        UpgradeData offlineHours = GetOrCreateUpgrade(
            RootFolder + "/Upgrade_MuseumOfflineHours.asset",
            MuseumIdleIncomeUpgradeUtility.OfflineHoursId,
            "Museum Offline Duration",
            "Extends the shared eligible offline-generation duration for both " +
            "Gold and Diamonds from the 1-hour base up to 24 hours.",
            120,
            0f,
            visitorIncomeUnlock,
            BuildOfflineLevels());

        UpgradeData goldCapacity = GetOrCreateUpgrade(
            RootFolder + "/Upgrade_MuseumGoldCapacity.asset",
            MuseumIdleIncomeUpgradeUtility.GoldCapacityId,
            "Museum Gold Storage",
            "Multiplies the maximum unclaimed visitor Gold that can be stored " +
            "before Gold generation pauses.",
            130,
            1f,
            visitorIncomeUnlock,
            BuildCapacityLevels(
                "Gold Storage",
                "Unclaimed Museum Gold capacity",
                UpgradeCurrency.Gold,
                new float[]
                {
                    1500f, 4000f, 10000f, 25000f,
                    60000f, 150000f, 400000f, 1000000f
                }));

        UpgradeData diamondCapacity = GetOrCreateUpgrade(
            RootFolder + "/Upgrade_MuseumDiamondCapacity.asset",
            MuseumIdleIncomeUpgradeUtility.DiamondCapacityId,
            "Diamond Endowment Storage",
            "Multiplies the maximum unclaimed passive Diamonds that can be " +
            "stored before Diamond generation pauses.",
            140,
            1f,
            diamondIncomeUnlock,
            BuildCapacityLevels(
                "Diamond Storage",
                "Unclaimed passive Diamond capacity",
                UpgradeCurrency.Diamonds,
                new float[] { 1f, 2f, 4f, 7f, 11f, 16f, 24f, 36f }));

        RegisterUpgrade(database.upgradeCatalog, goldIncome);
        RegisterUpgrade(database.upgradeCatalog, diamondIncome);
        RegisterUpgrade(database.upgradeCatalog, offlineHours);
        RegisterUpgrade(database.upgradeCatalog, goldCapacity);
        RegisterUpgrade(database.upgradeCatalog, diamondCapacity);

        RemoveUpgradeById(
            database.upgradeCatalog,
            MuseumIdleIncomeUpgradeUtility.LegacySharedIncomeMultiplierId);

        if (database.museumBalance.idleIncome == null)
            database.museumBalance.idleIncome = new MuseumIdleIncomeSettings();

        Undo.RecordObject(
            database.museumBalance,
            "Set Museum base offline duration");

        database.museumBalance.idleIncome.maximumOfflineHours = 1f;
        EditorUtility.SetDirty(database.museumBalance);

        EditorUtility.SetDirty(database.upgradeCatalog);
        database.upgradeCatalog.RebuildLookup();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = goldIncome;
        EditorGUIUtility.PingObject(goldIncome);

        Debug.Log(
            "Applied M5.1 Museum idle-income upgrades: separate Gold and " +
            "Diamond income multipliers, shared offline duration, separate " +
            "Gold and Diamond storage, and a 1-hour base offline cap.",
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

    private static List<UpgradeLevelData> BuildIncomeMultiplierLevels(
        string levelPrefix,
        UpgradeCurrency currency,
        float[] costs,
        string effectDescription)
    {
        List<UpgradeLevelData> result =
            new List<UpgradeLevelData>(IncomeEffects.Length);

        for (int i = 0; i < IncomeEffects.Length; i++)
        {
            float effect = IncomeEffects[i];
            float cost = costs != null && i < costs.Length
                ? costs[i]
                : 0f;

            result.Add(Level(
                $"{levelPrefix} {ToRoman(i + 1)}",
                $"{effectDescription} x{effect:0.##}.",
                currency,
                cost,
                effect));
        }

        return result;
    }

    private static float[] BuildGoldIncomeCosts()
    {
        return new float[]
        {
            500f, 900f, 1500f, 2500f, 4000f, 6500f,
            10000f, 16000f, 25000f, 40000f, 65000f, 100000f,
            160000f, 250000f, 400000f, 650000f, 1000000f,
            1600000f, 2500000f, 4000000f, 6500000f,
            10000000f, 16000000f
        };
    }

    private static float[] BuildDiamondIncomeCosts()
    {
        return new float[]
        {
            1f, 2f, 3f, 4f, 5f, 6f,
            8f, 10f, 12f, 15f, 18f, 22f,
            27f, 33f, 40f, 48f, 58f, 70f,
            85f, 100f, 120f, 145f, 175f
        };
    }

    private static List<UpgradeLevelData> BuildOfflineLevels()
    {
        // Base duration is 1 hour. Nine +1-hour levels reach 10 hours.
        // Seven +2-hour levels are required to reach the requested 24 hours.
        float[] bonusHours =
        {
            1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f,
            11f, 13f, 15f, 17f, 19f, 21f, 23f
        };

        float[] costs =
        {
            1000f, 2000f, 3500f, 6000f, 10000f, 16000f,
            25000f, 40000f, 65000f, 100000f, 160000f,
            250000f, 400000f, 650000f, 1000000f, 1600000f
        };

        List<UpgradeLevelData> result =
            new List<UpgradeLevelData>(bonusHours.Length);

        for (int i = 0; i < bonusHours.Length; i++)
        {
            float totalHours = 1f + bonusHours[i];

            result.Add(Level(
                $"Offline Duration {ToRoman(i + 1)}",
                $"Shared Gold and Diamond offline cap: " +
                $"{totalHours:0} hours total.",
                UpgradeCurrency.Gold,
                costs[i],
                bonusHours[i]));
        }

        return result;
    }

    private static List<UpgradeLevelData> BuildCapacityLevels(
        string levelPrefix,
        string effectDescription,
        UpgradeCurrency currency,
        float[] costs)
    {
        float[] effects =
        {
            1.5f, 2.0f, 2.5f, 3.0f,
            3.5f, 4.0f, 4.5f, 5.0f
        };

        List<UpgradeLevelData> result =
            new List<UpgradeLevelData>(effects.Length);

        for (int i = 0; i < effects.Length; i++)
        {
            result.Add(Level(
                $"{levelPrefix} {ToRoman(i + 1)}",
                $"{effectDescription} x{effects[i]:0.##}.",
                currency,
                costs[i],
                effects[i]));
        }

        return result;
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

    private static string ToRoman(int value)
    {
        if (value <= 0)
            return value.ToString();

        int[] values =
        {
            1000, 900, 500, 400, 100, 90, 50, 40,
            10, 9, 5, 4, 1
        };

        string[] symbols =
        {
            "M", "CM", "D", "CD", "C", "XC", "L", "XL",
            "X", "IX", "V", "IV", "I"
        };

        string result = "";

        for (int i = 0; i < values.Length; i++)
        {
            while (value >= values[i])
            {
                result += symbols[i];
                value -= values[i];
            }
        }

        return result;
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
                    StringComparison.OrdinalIgnoreCase))
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

    private static void RemoveUpgradeById(
        UpgradeCatalog catalog,
        string upgradeId)
    {
        if (catalog == null || string.IsNullOrWhiteSpace(upgradeId))
            return;

        SerializedObject serializedCatalog =
            new SerializedObject(catalog);

        SerializedProperty upgrades =
            serializedCatalog.FindProperty("upgrades");

        if (upgrades == null)
            return;

        for (int i = upgrades.arraySize - 1; i >= 0; i--)
        {
            UpgradeData existing =
                upgrades.GetArrayElementAtIndex(i).objectReferenceValue
                as UpgradeData;

            if (existing == null ||
                !string.Equals(
                    existing.upgradeId,
                    upgradeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int sizeBefore = upgrades.arraySize;
            upgrades.DeleteArrayElementAtIndex(i);

            if (upgrades.arraySize == sizeBefore)
                upgrades.DeleteArrayElementAtIndex(i);
        }

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
