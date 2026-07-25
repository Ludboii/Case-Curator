#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies the first M5 balance pass and the approved Staircase income-node
/// weights to the selected GameDatabase's MuseumBalanceData asset.
/// </summary>
public static class MuseumIdleIncomeSetup
{
    [MenuItem("Tools/Case Curator/Museum/Apply M5 Idle Income Defaults")]
    public static void ApplyDefaults()
    {
        GameDatabase database = FindTargetDatabase();

        if (database == null)
            return;

        if (database.museumBalance == null)
        {
            EditorUtility.DisplayDialog(
                "Museum Balance Missing",
                "Assign MuseumBalanceData on the selected GameDatabase first.",
                "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Apply M5 Idle Income Defaults",
            "This replaces the passive-income rates, capacities and milestone " +
            "modifier list on the assigned MuseumBalanceData asset. Donation " +
            "point settings are not changed.",
            "Apply",
            "Cancel");

        if (!confirmed)
            return;

        MuseumBalanceData balance = database.museumBalance;
        Undo.RecordObject(balance, "Apply M5 Idle Income Defaults");

        if (balance.idleIncome == null)
            balance.idleIncome = new MuseumIdleIncomeSettings();

        MuseumIdleIncomeSettings settings = balance.idleIncome;
        settings.goldPerMuseumPointPerHour = 0.000005d;
        settings.unclaimedGoldCapacity = 2500d;
        settings.diamondsPerHour = 0.05d;
        settings.unclaimedDiamondCapacity = 3d;
        settings.maximumOfflineHours = 8f;
        settings.minimumCalculationIntervalSeconds = 30f;
        settings.milestoneModifiers = BuildModifiers();

        EditorUtility.SetDirty(balance);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = balance;
        EditorGUIUtility.PingObject(balance);

        Debug.Log(
            "Applied M5 Museum idle-income defaults. Visitor Gold begins when " +
            "Step 10 is claimed, Step 38 increases Gold capacity, Step 40 uses " +
            "a large income-node weight, Step 75 unlocks passive Diamonds, and " +
            "Step 80 uses the largest income-node weight.",
            balance);
    }

    private static List<MuseumIdleMilestoneModifier> BuildModifiers()
    {
        return new List<MuseumIdleMilestoneModifier>
        {
            Node(10, 1f),
            Node(20, 1f),
            Node(25, 1f),
            Node(35, 1f),
            Capacity(38, 0.5f),
            Node(40, 2f),
            Node(50, 1f),
            Node(55, 1f),
            Node(65, 1f),
            Node(70, 1f),
            Node(80, 3f)
        };
    }

    private static MuseumIdleMilestoneModifier Node(
        int step,
        float weight)
    {
        return new MuseumIdleMilestoneModifier
        {
            milestoneId = $"museum-step-{step:00}",
            goldNodeWeight = Mathf.Max(0f, weight)
        };
    }

    private static MuseumIdleMilestoneModifier Capacity(
        int step,
        float multiplierBonus)
    {
        return new MuseumIdleMilestoneModifier
        {
            milestoneId = $"museum-step-{step:00}",
            goldCapacityMultiplierBonus =
                Mathf.Max(0f, multiplierBonus)
        };
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
                "More than one GameDatabase asset exists. Select the intended " +
                "asset in the Project window, then run this command again.",
                "OK");
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<GameDatabase>(path);
    }
}
#endif
