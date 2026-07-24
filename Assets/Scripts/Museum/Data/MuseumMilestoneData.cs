using System;
using UnityEngine;

public enum MuseumMilestoneBand
{
    DustyLobby = 0,
    StarterArchive = 1,
    CollectorHall = 2,
    PremiumVault = 3,
    MythicGallery = 4,
    GlobalExhibit = 5
}

public enum MuseumMilestoneType
{
    Plaque = 0,
    MajorPresent = 1,
    IncomeNode = 2,
    BandTransition = 3,
    SystemUnlock = 4,
    Finale = 5
}

/// <summary>
/// One claimable step in the Museum Milestone Staircase. Claim state is stored
/// by milestoneId in MuseumStateSaveData; therefore milestone IDs must remain
/// stable after release.
/// </summary>
[CreateAssetMenu(
    fileName = "MuseumMilestoneData",
    menuName = "Case Curator/Museum/Museum Milestone")]
public class MuseumMilestoneData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable ID written to claimedMilestoneIds in save data.")]
    public string milestoneId;

    [Min(1)]
    public int stairNumber = 1;

    public string displayName;

    [TextArea(2, 5)]
    public string description;

    public Sprite icon;
    public bool majorMilestone;

    [Header("Staircase Presentation")]
    public MuseumMilestoneBand band = MuseumMilestoneBand.DustyLobby;
    public MuseumMilestoneType milestoneType = MuseumMilestoneType.Plaque;

    [Tooltip("Player-facing summary used even when a later reward system is not implemented yet.")]
    [TextArea(1, 4)]
    public string rewardSummary;

    [Tooltip("Dusty, Bronze, Silver, Gold, Diamond or Global Elite.")]
    public string presentTier;

    [Header("Requirements")]
    [Min(0f)]
    public double requiredMuseumPoints;

    [Tooltip(
        "Optional extra gate such as rank, previous feature, container " +
        "completion or claimed milestone. Museum Points are always checked " +
        "separately through Required Museum Points.")]
    public UnlockDefinition additionalRequirement;

    [Header("Claim Result")]
    public MuseumRewardData reward;

    [Tooltip(
        "Optional stable plaque/display ID unlocked when this step is claimed. " +
        "Leave empty when the milestone does not unlock a plaque.")]
    public string unlockedPlaqueId;

    [Tooltip(
        "Optional feature shown as the milestone's major unlock. The actual " +
        "availability check should still use an UnlockDefinition that requires " +
        "this milestone ID.")]
    public FeatureId announcedFeatureUnlock = FeatureId.None;

    [Header("Future Museum Systems")]
    [Tooltip("M5 can use this claimed milestone as a passive Museum Gold income node.")]
    public bool unlocksPassiveMuseumGold;

    [Tooltip(
        "M5 can use this claimed milestone to unlock very slow, capped passive " +
        "diamond generation. The final 80-step list uses this at step 75.")]
    public bool unlocksPassiveDiamonds;

    [Tooltip(
        "Optional stable system identifier used by future systems and UI. " +
        "Examples: automated-acquisitions or passive-diamond-generation.")]
    public string announcedSystemId;

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : name;

    public string BandDisplayName
    {
        get
        {
            switch (band)
            {
                case MuseumMilestoneBand.DustyLobby: return "Dusty Lobby";
                case MuseumMilestoneBand.StarterArchive: return "Starter Archive";
                case MuseumMilestoneBand.CollectorHall: return "Collector Hall";
                case MuseumMilestoneBand.PremiumVault: return "Premium Vault";
                case MuseumMilestoneBand.MythicGallery: return "Mythic Gallery";
                case MuseumMilestoneBand.GlobalExhibit: return "Global Exhibit";
                default: return band.ToString();
            }
        }
    }

    private void OnValidate()
    {
        milestoneId = Trim(milestoneId);
        displayName = Trim(displayName);
        presentTier = Trim(presentTier);
        unlockedPlaqueId = Trim(unlockedPlaqueId);
        announcedSystemId = Trim(announcedSystemId);
        stairNumber = Mathf.Max(1, stairNumber);
        requiredMuseumPoints = Math.Max(0d, requiredMuseumPoints);
    }

    private static string Trim(string value)
    {
        return value != null ? value.Trim() : "";
    }
}
