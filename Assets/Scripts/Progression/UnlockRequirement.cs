using System;
using UnityEngine;

/// <summary>
/// One data-driven condition used by an UnlockDefinition.
/// Only the fields belonging to the selected requirementType are evaluated.
/// </summary>
[Serializable]
public class UnlockRequirement
{
    [Header("Requirement")]
    public UnlockRequirementType requirementType;

    [Tooltip(
        "Optional player-facing text used when this requirement fails. " +
        "Leave empty to use the evaluator's generated explanation.")]
    [TextArea(1, 3)]
    public string lockedReasonOverride;

    [Header("Player Progress")]
    public PlayerRank minimumRank;
    [Min(0)] public int minimumXp;
    [Min(0f)] public float minimumGold;
    [Min(0)] public int minimumDiamonds;

    [Header("Museum")]
    [Min(0f)] public double minimumMuseumPoints;

    [Tooltip("Stable ID from the Museum milestone definition.")]
    public string requiredMilestoneId;

    [Min(0)] public int minimumClaimedMilestones;

    [Header("Tradeups")]
    [Min(0)] public int minimumCompletedTradeups;

    [Header("Container Completion")]
    public CaseData requiredContainer;
    public ContainerCompletionTier minimumContainerTier =
        ContainerCompletionTier.Bronze;

    [Header("Upgrade / Feature Dependencies")]
    [Tooltip("Stable UpgradeData ID. Matching is case-insensitive.")]
    public string requiredUpgradeId;

    [Min(0)] public int minimumUpgradeLevel = 1;

    [Tooltip(
        "Another UnlockDefinition that must currently evaluate as unlocked. " +
        "Circular references are rejected by UnlockEvaluator.")]
    public UnlockDefinition requiredFeature;

    [Header("Inventory Capacity")]
    [Range(1, 3)] public int minimumOpeningSlots = 1;
    [Min(1)] public int minimumStoragePages = 1;
}
