using System;
using UnityEngine;

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

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : name;

    private void OnValidate()
    {
        milestoneId = Trim(milestoneId);
        displayName = Trim(displayName);
        unlockedPlaqueId = Trim(unlockedPlaqueId);
        stairNumber = Mathf.Max(1, stairNumber);
        requiredMuseumPoints = Math.Max(0d, requiredMuseumPoints);
    }

    private static string Trim(string value)
    {
        return value != null ? value.Trim() : "";
    }
}
