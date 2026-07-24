#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates or updates the persistent ScriptableObject assets for the approved
/// 80-step / 5,000,000 MP M4 staircase. Re-running the normal command preserves
/// manually edited reward payloads while updating thresholds and presentation.
/// </summary>
public static class MuseumMilestone80Generator
{
    private const string RootFolder =
        "Assets/Data/Museum/Milestones80";
    private const string MilestoneFolder =
        RootFolder + "/Milestones";
    private const string RewardFolder =
        RootFolder + "/Rewards";

    [MenuItem(
        "Tools/Case Curator/Museum/Generate or Update 80-Step Staircase")]
    public static void GeneratePreservingRewardPayloads()
    {
        Generate(false);
    }

    [MenuItem(
        "Tools/Case Curator/Museum/Reset 80-Step Reward Defaults")]
    public static void GenerateAndResetRewardPayloads()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Reset Museum Milestone Rewards",
            "This updates all 80 milestones and replaces the Gold, Diamond and " +
            "XP payloads on the generated reward assets. Container references " +
            "are preserved. Continue?",
            "Reset Rewards",
            "Cancel");

        if (confirmed)
            Generate(true);
    }

    private static void Generate(bool overwriteRewardPayloads)
    {
        GameDatabase database = FindTargetDatabase();

        if (database == null)
            return;

        EnsureFolder("Assets", "Data");
        EnsureFolder("Assets/Data", "Museum");
        EnsureFolder("Assets/Data/Museum", "Milestones80");
        EnsureFolder(RootFolder, "Milestones");
        EnsureFolder(RootFolder, "Rewards");

        List<MuseumMilestoneData> generated =
            new List<MuseumMilestoneData>(
                MuseumMilestone80Defaults.All.Length);

        AssetDatabase.StartAssetEditing();

        try
        {
            for (int i = 0;
                 i < MuseumMilestone80Defaults.All.Length;
                 i++)
            {
                MuseumMilestone80Definition definition =
                    MuseumMilestone80Defaults.All[i];

                MuseumRewardData reward =
                    GetOrCreateReward(
                        definition,
                        overwriteRewardPayloads);

                MuseumMilestoneData milestone =
                    GetOrCreateMilestone(definition);

                ApplyDefinition(milestone, reward, definition);
                generated.Add(milestone);
            }

            Undo.RecordObject(
                database,
                "Assign Museum Milestone Staircase");

            database.museumMilestones = generated;
            EditorUtility.SetDirty(database);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Generated {generated.Count} Museum milestone assets and assigned " +
            $"them to {AssetDatabase.GetAssetPath(database)}. " +
            $"Final step: {MuseumMilestone80Defaults.FinalMuseumPoints:N0} MP. " +
            $"Passive diamonds unlock at step " +
            $"{MuseumMilestone80Defaults.PassiveDiamondUnlockStep}.");
    }

    private static MuseumRewardData GetOrCreateReward(
        MuseumMilestone80Definition definition,
        bool overwritePayload)
    {
        string path =
            $"{RewardFolder}/MuseumReward_{definition.step:00}.asset";

        MuseumRewardData reward =
            AssetDatabase.LoadAssetAtPath<MuseumRewardData>(path);

        bool created = reward == null;

        if (created)
        {
            reward =
                ScriptableObject.CreateInstance<MuseumRewardData>();

            AssetDatabase.CreateAsset(reward, path);
        }

        Undo.RecordObject(
            reward,
            $"Update Museum Reward {definition.step:00}");

        reward.rewardId =
            $"museum-reward-step-{definition.step:00}";
        reward.displayName =
            $"Museum Step {definition.step:00} Reward";
        reward.description =
            BuildRewardDescription(definition);

        if (created || overwritePayload)
            ApplyDefaultPayload(reward, definition);

        EditorUtility.SetDirty(reward);
        return reward;
    }

    private static MuseumMilestoneData GetOrCreateMilestone(
        MuseumMilestone80Definition definition)
    {
        string path =
            $"{MilestoneFolder}/MuseumMilestone_{definition.step:00}.asset";

        MuseumMilestoneData milestone =
            AssetDatabase.LoadAssetAtPath<MuseumMilestoneData>(path);

        if (milestone == null)
        {
            milestone =
                ScriptableObject.CreateInstance<MuseumMilestoneData>();

            AssetDatabase.CreateAsset(milestone, path);
        }

        Undo.RecordObject(
            milestone,
            $"Update Museum Milestone {definition.step:00}");

        return milestone;
    }

    private static void ApplyDefinition(
        MuseumMilestoneData milestone,
        MuseumRewardData reward,
        MuseumMilestone80Definition definition)
    {
        milestone.milestoneId = definition.MilestoneId;
        milestone.stairNumber = definition.step;
        milestone.displayName = definition.displayName;
        milestone.description = definition.notes;
        milestone.majorMilestone = definition.IsMajor;
        milestone.band = definition.band;
        milestone.milestoneType = definition.milestoneType;
        milestone.rewardSummary = definition.rewardSummary;
        milestone.presentTier = definition.presentTier;
        milestone.requiredMuseumPoints =
            definition.requiredMuseumPoints;
        milestone.reward = reward;
        milestone.unlockedPlaqueId = definition.PlaqueId;
        milestone.unlocksPassiveMuseumGold =
            definition.unlocksPassiveMuseumGold;
        milestone.unlocksPassiveDiamonds =
            definition.unlocksPassiveDiamonds;
        milestone.announcedSystemId =
            definition.announcedSystemId;

        EditorUtility.SetDirty(milestone);
    }

    private static void ApplyDefaultPayload(
        MuseumRewardData reward,
        MuseumMilestone80Definition definition)
    {
        string summary = definition.rewardSummary ?? "";
        float typeMultiplier =
            GetTypeMultiplier(definition.milestoneType);

        reward.gold = HasOneTimeGoldReward(summary)
            ? RoundGold(
                GetBandBaseGold(definition.band) *
                typeMultiplier)
            : 0f;

        reward.xp = summary.IndexOf(
            "XP",
            StringComparison.OrdinalIgnoreCase) >= 0
                ? Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        GetBandBaseXp(definition.band) *
                        typeMultiplier))
                : 0;

        // Step 75 unlocks a future passive diamond system rather than granting
        // recurring diamonds during M4. The finale includes a small one-time
        // premium-currency reward that remains editable in its reward asset.
        reward.diamonds = definition.milestoneType ==
                          MuseumMilestoneType.Finale
            ? 10
            : 0;
    }

    private static bool HasOneTimeGoldReward(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            return false;

        string value = summary.Trim();

        return value.StartsWith(
                   "Gold +",
                   StringComparison.OrdinalIgnoreCase) ||
               value.IndexOf(
                   "+ Gold",
                   StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.IndexOf(
                   "Gold bundle",
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static float GetBandBaseGold(
        MuseumMilestoneBand band)
    {
        switch (band)
        {
            case MuseumMilestoneBand.DustyLobby:
                return 40f;
            case MuseumMilestoneBand.StarterArchive:
                return 100f;
            case MuseumMilestoneBand.CollectorHall:
                return 250f;
            case MuseumMilestoneBand.PremiumVault:
                return 650f;
            case MuseumMilestoneBand.MythicGallery:
                return 1600f;
            case MuseumMilestoneBand.GlobalExhibit:
                return 4000f;
            default:
                return 40f;
        }
    }

    private static float GetBandBaseXp(
        MuseumMilestoneBand band)
    {
        switch (band)
        {
            case MuseumMilestoneBand.DustyLobby:
                return 10f;
            case MuseumMilestoneBand.StarterArchive:
                return 25f;
            case MuseumMilestoneBand.CollectorHall:
                return 60f;
            case MuseumMilestoneBand.PremiumVault:
                return 150f;
            case MuseumMilestoneBand.MythicGallery:
                return 350f;
            case MuseumMilestoneBand.GlobalExhibit:
                return 800f;
            default:
                return 10f;
        }
    }

    private static float GetTypeMultiplier(
        MuseumMilestoneType type)
    {
        switch (type)
        {
            case MuseumMilestoneType.MajorPresent:
                return 2.5f;
            case MuseumMilestoneType.IncomeNode:
                return 2f;
            case MuseumMilestoneType.BandTransition:
                return 4f;
            case MuseumMilestoneType.SystemUnlock:
                return 5f;
            case MuseumMilestoneType.Finale:
                return 10f;
            default:
                return 1f;
        }
    }

    private static float RoundGold(float value)
    {
        return Mathf.Max(
            0f,
            Mathf.Round(value / 5f) * 5f);
    }

    private static string BuildRewardDescription(
        MuseumMilestone80Definition definition)
    {
        string description =
            definition.rewardSummary ?? "";

        if (!string.IsNullOrWhiteSpace(definition.notes))
        {
            description +=
                string.IsNullOrWhiteSpace(description)
                    ? definition.notes
                    : "\n\n" + definition.notes;
        }

        if (definition.unlocksPassiveDiamonds)
        {
            description +=
                "\n\nUnlocks slow, capped passive diamond generation. " +
                "The generation rate and capacity are configured in phase M5.";
        }

        return description.Trim();
    }

    private static GameDatabase FindTargetDatabase()
    {
        if (Selection.activeObject is GameDatabase selected)
            return selected;

        string[] guids =
            AssetDatabase.FindAssets("t:GameDatabase");

        if (guids == null || guids.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "GameDatabase Not Found",
                "Create or select a GameDatabase asset before generating the " +
                "Museum milestone staircase.",
                "OK");
            return null;
        }

        if (guids.Length > 1)
        {
            EditorUtility.DisplayDialog(
                "Select GameDatabase",
                "More than one GameDatabase asset exists. Select the intended " +
                "GameDatabase in the Project window, then run the command again.",
                "OK");
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);

        return AssetDatabase.LoadAssetAtPath<GameDatabase>(path);
    }

    private static void EnsureFolder(
        string parent,
        string child)
    {
        string path = $"{parent}/{child}";

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
