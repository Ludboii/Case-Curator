using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data asset describing when a major feature becomes available.
/// UI and gameplay systems should query UnlockEvaluator instead of duplicating
/// rank, Museum, completion or upgrade checks.
/// </summary>
[CreateAssetMenu(
    fileName = "UnlockDefinition",
    menuName = "Case Curator/Progression/Unlock Definition")]
public class UnlockDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip(
        "Stable identifier used by save data, debug tools and future catalogs. " +
        "Do not change this after release.")]
    public string unlockId;

    public FeatureId featureId = FeatureId.Custom;
    public string displayName;

    [Header("Requirements")]
    public UnlockRequirementGroupMode requirementMode =
        UnlockRequirementGroupMode.All;

    [Tooltip(
        "When enabled, an asset with no requirements is available. " +
        "Disable this for placeholder definitions that must remain locked.")]
    public bool noRequirementsMeansUnlocked = true;

    public List<UnlockRequirement> requirements =
        new List<UnlockRequirement>();

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : name;

    private void OnValidate()
    {
        if (unlockId != null)
            unlockId = unlockId.Trim();

        if (requirements == null)
            requirements = new List<UnlockRequirement>();
    }
}
