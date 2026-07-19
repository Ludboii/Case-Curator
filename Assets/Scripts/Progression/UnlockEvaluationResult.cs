using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class UnlockRequirementEvaluation
{
    public UnlockRequirementType requirementType;
    public bool passed;
    public string message;

    public bool hasNumericProgress;
    public double currentValue;
    public double targetValue;

    public float NormalizedProgress
    {
        get
        {
            if (!hasNumericProgress || targetValue <= 0d)
                return passed ? 1f : 0f;

            return Mathf.Clamp01(
                (float)(currentValue / targetValue));
        }
    }

    public string ProgressText
    {
        get
        {
            if (!hasNumericProgress)
                return "";

            return $"{currentValue:N0} / {targetValue:N0}";
        }
    }
}

/// <summary>
/// Complete evaluation result intended for both gameplay checks and UI.
/// It contains every requirement result so a locked screen can explain why.
/// </summary>
[Serializable]
public class UnlockEvaluationResult
{
    public bool isUnlocked;
    public string unlockId;
    public FeatureId featureId;
    public string displayName;

    public List<UnlockRequirementEvaluation> requirementResults =
        new List<UnlockRequirementEvaluation>();

    public string FirstFailureReason
    {
        get
        {
            // An ANY-group can contain failed alternatives while still being
            // unlocked, so overall state must take priority over child results.
            if (isUnlocked)
                return "Unlocked.";

            if (requirementResults == null)
                return "Unlock requirements were not evaluated.";

            for (int i = 0; i < requirementResults.Count; i++)
            {
                UnlockRequirementEvaluation result = requirementResults[i];

                if (result != null && !result.passed)
                {
                    return !string.IsNullOrWhiteSpace(result.message)
                        ? result.message
                        : "A requirement has not been met.";
                }
            }

            return "Unlock requirements have not been met.";
        }
    }

    public UnlockRequirementEvaluation GetPrimaryProgress()
    {
        if (requirementResults == null)
            return null;

        for (int i = 0; i < requirementResults.Count; i++)
        {
            UnlockRequirementEvaluation result = requirementResults[i];

            if (result != null &&
                result.hasNumericProgress &&
                !result.passed)
            {
                return result;
            }
        }

        for (int i = 0; i < requirementResults.Count; i++)
        {
            UnlockRequirementEvaluation result = requirementResults[i];

            if (result != null && result.hasNumericProgress)
                return result;
        }

        return null;
    }

    public static UnlockEvaluationResult MissingDefinition()
    {
        return new UnlockEvaluationResult
        {
            isUnlocked = false,
            unlockId = "",
            featureId = FeatureId.None,
            displayName = "Missing Unlock Definition",
            requirementResults = new List<UnlockRequirementEvaluation>
            {
                new UnlockRequirementEvaluation
                {
                    passed = false,
                    message = "No UnlockDefinition was supplied."
                }
            }
        };
    }
}
