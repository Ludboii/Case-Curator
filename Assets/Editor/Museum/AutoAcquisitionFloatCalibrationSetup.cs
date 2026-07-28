#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AutoAcquisitionFloatCalibrationSetup
{
    private const string Folder =
        "Assets/Data/Museum/AutomatedAcquisitions/Upgrades";
    private const string Path =
        Folder + "/Upgrade_FloatCalibration.asset";

    [MenuItem(
        "Tools/Case Curator/Museum/Automated Acquisitions/Apply Float Calibration Upgrade")]
    public static void Apply()
    {
        GameDatabase database = FindDatabase();

        if (database == null || database.upgradeCatalog == null)
        {
            EditorUtility.DisplayDialog(
                "Database Missing",
                "Select the main GameDatabase with an assigned UpgradeCatalog.",
                "OK");
            return;
        }

        EnsureFolder();

        UpgradeData upgrade = AssetDatabase.LoadAssetAtPath<UpgradeData>(Path);

        if (upgrade == null)
        {
            upgrade = ScriptableObject.CreateInstance<UpgradeData>();
            AssetDatabase.CreateAsset(upgrade, Path);
        }

        Undo.RecordObject(upgrade, "Update Float Calibration upgrade");
        upgrade.upgradeId = AutoAcquisitionUpgradeUtility.FloatCalibrationId;
        upgrade.displayName = "Float Calibration";
        upgrade.description =
            "Improves Automated Acquisition float quality until it matches " +
            "manual container opening.";
        upgrade.category = UpgradeCategory.AutomatedAcquisitions;
        upgrade.sortOrder = 415;
        upgrade.hiddenUntilUnlocked = false;
        upgrade.effectType = UpgradeEffectType.GenericValue;
        upgrade.defaultEffectValue = 0.55f;
        upgrade.levels = new List<UpgradeLevelData>
        {
            Level("Float Calibration I", 100000f, 0.65f),
            Level("Float Calibration II", 250000f, 0.75f),
            Level("Float Calibration III", 600000f, 0.88f),
            Level("Float Calibration IV", 1500000f, 1.00f)
        };

        Register(database.upgradeCatalog, upgrade);
        database.upgradeCatalog.RebuildLookup();
        EditorUtility.SetDirty(upgrade);
        EditorUtility.SetDirty(database.upgradeCatalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = upgrade;
        EditorGUIUtility.PingObject(upgrade);
    }

    private static UpgradeLevelData Level(
        string name,
        float cost,
        float exponent)
    {
        return new UpgradeLevelData
        {
            levelName = name,
            description =
                $"Float curve exponent {exponent:0.00}. " +
                "Higher values produce better average floats.",
            currency = UpgradeCurrency.Gold,
            cost = cost,
            effectValue = exponent
        };
    }

    private static void Register(UpgradeCatalog catalog, UpgradeData upgrade)
    {
        SerializedObject serialized = new SerializedObject(catalog);
        SerializedProperty upgrades = serialized.FindProperty("upgrades");

        for (int i = 0; i < upgrades.arraySize; i++)
        {
            SerializedProperty element = upgrades.GetArrayElementAtIndex(i);
            UpgradeData existing = element.objectReferenceValue as UpgradeData;

            if (existing == upgrade ||
                (existing != null && existing.upgradeId == upgrade.upgradeId))
            {
                element.objectReferenceValue = upgrade;
                serialized.ApplyModifiedProperties();
                return;
            }
        }

        int index = upgrades.arraySize;
        upgrades.InsertArrayElementAtIndex(index);
        upgrades.GetArrayElementAtIndex(index).objectReferenceValue = upgrade;
        serialized.ApplyModifiedProperties();
    }

    private static GameDatabase FindDatabase()
    {
        if (Selection.activeObject is GameDatabase selected)
            return selected;

        string[] guids = AssetDatabase.FindAssets("t:GameDatabase");

        if (guids.Length != 1)
            return null;

        return AssetDatabase.LoadAssetAtPath<GameDatabase>(
            AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(
                "Assets/Data/Museum/AutomatedAcquisitions"))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder(
                "Assets/Data/Museum/AutomatedAcquisitions",
                "Upgrades");
        }
    }
}
#endif
