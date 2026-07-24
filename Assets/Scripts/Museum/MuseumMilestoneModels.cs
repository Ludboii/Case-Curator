using System;
using System.Collections.Generic;

public enum MuseumMilestoneClaimStatus
{
    Locked = 0,
    Claimable = 1,
    Claimed = 2
}

public sealed class MuseumMilestoneState
{
    public MuseumMilestoneData data;
    public MuseumMilestoneClaimStatus status;
    public double currentMuseumPoints;
    public double previousRequiredMuseumPoints;
    public double requiredMuseumPoints;
    public float segmentProgress01;
    public bool additionalRequirementMet = true;
    public string lockedReason;
    public bool runtimePreviewOnly;

    public bool IsClaimed => status == MuseumMilestoneClaimStatus.Claimed;
    public bool IsClaimable => status == MuseumMilestoneClaimStatus.Claimable;
    public bool IsLocked => status == MuseumMilestoneClaimStatus.Locked;

    public int Step => data != null ? data.stairNumber : 0;
    public string MilestoneId => data != null ? data.milestoneId : "";
}

public sealed class MuseumMilestoneClaimResult
{
    public bool success;
    public string message;
    public MuseumMilestoneData milestone;
    public MuseumRewardData reward;
    public List<string> grantedRewardLines = new List<string>();
    public int totalClaimedMilestones;
    public int remainingClaimableMilestones;

    public static MuseumMilestoneClaimResult Failed(
        MuseumMilestoneData milestone,
        string message)
    {
        return new MuseumMilestoneClaimResult
        {
            success = false,
            milestone = milestone,
            reward = milestone != null ? milestone.reward : null,
            message = string.IsNullOrWhiteSpace(message)
                ? "The Museum milestone could not be claimed."
                : message
        };
    }
}

public sealed class MuseumMilestoneValidationIssue
{
    public string milestoneId;
    public string message;
    public bool severe;

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(milestoneId)
            ? message
            : $"{milestoneId}: {message}";
    }
}
