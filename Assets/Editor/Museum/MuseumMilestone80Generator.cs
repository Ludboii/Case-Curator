#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates or updates the persistent ScriptableObject assets for the approved
/// 80-step / 5,000,000 MP Museum staircase.
/// </summary>
public static class MuseumMilestone80Generator
{
    private const string RootFolder =
        "Assets/Data/Museum/Milestones80";
    private const string MilestoneFolder =
        RootFolder + "/Milestones";
    private const string RewardFolder =
        RootFolder + "/Rewards";
    private const string PresentConfigPath =
        "Assets/Data/Museum/MuseumPresentConfig.asset";

    [MenuItem(
        "Tools/Case Curator/Museum/Generate or Update 80-Step Staircase")]
    public static void GeneratePreservingRewardPayloads()
    {
        Generate(false, false);
    }

    [MenuItem(
        "Tools/Case Curator/Museum/Apply M4.5 Present Rewards")]
    public static void ApplyM45PresentRewards()
    {
        Generate(false, true);
    }

    [MenuItem(
        "Tools/Case Curator/Museum/Reset 80-Step Reward Defaults")]
    public static void GenerateAndResetRewardPayloads()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Reset Museum Milestone Rewards",
            "This updates all 80 milestones and replaces the generated Gold, " +
            "Diamond, XP, fragment and full-present payloads. Container " +
            "references are preserved. Continue?",
            "Reset Rewards",
            "Cancel");

        if (confirmed)
            Generate(true, true);
    }

    private static void Generate(
        bool overwriteCurrencyPayloads,
        bool overwritePresentPayloads)
    {
        GameDatabase database = FindTargetDatabase();

        if (database == null)
            return;

        EnsureFolder("Assets", "Data");
        EnsureFolder("Assets/Data", "Museum");
        EnsureFolder("Assets/Data/Museum", "Milestones80");
        EnsureFolder(RootFolder, "Milestones");
        EnsureFolder(RootFolder, "Rewards");

        MuseumPresentConfig presentConfig =
            GetOrCreatePresentConfig();

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
                        overwriteCurrencyPayloads,
                        overwritePresentPayloads);

                MuseumMilestoneData milestone =
                    GetOrCreateMilestone(definition);

                ApplyDefinition(milestone, reward, definition);
                generated.Add(milestone);
            }

            Undo.RecordObject(
                database,
                "Assign Museum Milestone Staircase");

            database.museumMilestones = generated;
            database.museumPresentConfig = presentConfig;
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
            "All upgrade-token placeholder rewards were removed. " +
            "M4.5 fragment and present rewards are ready.");
    }

    private static MuseumPresentConfig GetOrCreatePresentConfig()
    {
        MuseumPresentConfig config =
            AssetDatabase.LoadAssetAtPath<MuseumPresentConfig>(
                PresentConfigPath);

        if (config == null)
        {
            config = ScriptableObject.CreateInstance<MuseumPresentConfig>();
            config.tiers = new List<MuseumPresentTierConfig>();

            for (int i = 0; i < MuseumPresentUtility.AllTiers.Length; i++)
            {
                config.tiers.Add(
                    MuseumPresentConfig.CreateFallbackTier(
                        MuseumPresentUtility.AllTiers[i]));
            }

            AssetDatabase.CreateAsset(config, PresentConfigPath);
        }
        else if (config.tiers == null || config.tiers.Count == 0)
        {
            Undo.RecordObject(config, "Populate Museum Present Config");
            config.tiers = new List<MuseumPresentTierConfig>();

            for (int i = 0; i < MuseumPresentUtility.AllTiers.Length; i++)
            {
                config.tiers.Add(
                    MuseumPresentConfig.CreateFallbackTier(
                        MuseumPresentUtility.AllTiers[i]));
            }

            EditorUtility.SetDirty(config);
        }

        return config;
    }

    private static MuseumRewardData GetOrCreateReward(
        MuseumMilestone80Definition definition,
        bool overwriteCurrencyPayload,
        bool overwritePresentPayload)
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

        if (created || overwriteCurrencyPayload)
            ApplyDefaultCurrencyPayload(reward, definition);

        if (created || overwritePresentPayload)
            ApplyDefaultPresentPayload(reward, definition);

        if (reward.containerRewards == null)
            reward.containerRewards = new List<MuseumContainerReward>();

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

    private static void ApplyDefaultCurrencyPayload(
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

        reward.diamonds = definition.milestoneType ==
                          MuseumMilestoneType.Finale
            ? 10
            : 0;
    }

    private static void ApplyDefaultPresentPayload(
        MuseumRewardData reward,
        MuseumMilestone80Definition definition)
    {
        if (reward.presentRewards == null)
        {
            reward.presentRewards =
                new List<MuseumPresentRewardEntry>();
        }
        else
        {
            reward.presentRewards.Clear();
        }

        string summary = definition.rewardSummary ?? "";
        string lower = summary.ToLowerInvariant();
        MuseumPresentTier fallbackTier;

        if (!MuseumPresentUtility.TryParseTier(
                definition.presentTier,
                out fallbackTier))
        {
            fallbackTier = MuseumPresentTier.Dusty;
        }

        MuseumPresentTier rewardTier =
            ResolveRewardTier(lower, fallbackTier);

        int fragments = 0;
        int presents = 0;

        if (lower.Contains("fragment"))
        {
            fragments = lower.Contains("large")
                ? 40
                : lower.Contains("first") || lower.Contains("small")
                    ? 10
                    : 20;
        }

        if (lower.Contains("3x") && lower.Contains("present"))
            presents = 3;
        else if (lower.Contains("full") && lower.Contains("present"))
            presents = 1;

        if (fragments <= 0 && presents <= 0)
            return;

        reward.presentRewards.Add(new MuseumPresentRewardEntry
        {
            tier = rewardTier,
            fragments = fragments,
            presents = presents
        });
    }

    private static MuseumPresentTier ResolveRewardTier(
        string lowerSummary,
        MuseumPresentTier fallback)
    {
        if (ContainsTierReward(lowerSummary, "global elite"))
            return MuseumPresentTier.GlobalElite;
        if (ContainsTierReward(lowerSummary, "diamond"))
            return MuseumPresentTier.Diamond;
        if (ContainsTierReward(lowerSummary, "silver"))
            return MuseumPresentTier.Silver;
        if (ContainsTierReward(lowerSummary, "bronze"))
            return MuseumPresentTier.Bronze;
        if (ContainsTierReward(lowerSummary, "dusty"))
            return MuseumPresentTier.Dusty;
        if (ContainsTierReward(lowerSummary, "gold"))
            return MuseumPresentTier.Gold;

        return fallback;
    }

    private static bool ContainsTierReward(
        string summary,
        string tierName)
    {
        return summary.Contains(tierName + " fragment") ||
               summary.Contains(tierName + " present") ||
               summary.Contains("full " + tierName + " present");
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
