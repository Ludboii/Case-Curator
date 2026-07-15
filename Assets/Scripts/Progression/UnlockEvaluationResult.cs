using System;
using System.Collections.Generic;

[Serializable]
public class UnlockRequirementEvaluation
{
    public UnlockRequirementType requirementType;
    public bool passed;
    public string message;
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

            return isUnlocked
                ? "Unlocked."
                : "Unlock requirements have not been met.";
        }
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
