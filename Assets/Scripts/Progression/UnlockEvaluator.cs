using System;
using System.Collections.Generic;

/// <summary>
/// Central, side-effect-free caller API for evaluating UnlockDefinition assets.
/// The evaluator reads the current managers and returns detailed failure reasons;
/// it never spends currency or changes progression itself.
/// </summary>
public static class UnlockEvaluator
{
    public static bool IsUnlocked(UnlockDefinition definition)
    {
        return Evaluate(definition).isUnlocked;
    }

    public static UnlockEvaluationResult Evaluate(
        UnlockDefinition definition)
    {
        return EvaluateInternal(
            definition,
            new HashSet<UnlockDefinition>());
    }

    private static UnlockEvaluationResult EvaluateInternal(
        UnlockDefinition definition,
        HashSet<UnlockDefinition> evaluationStack)
    {
        if (definition == null)
            return UnlockEvaluationResult.MissingDefinition();

        UnlockEvaluationResult result = new UnlockEvaluationResult
        {
            unlockId = definition.unlockId ?? "",
            featureId = definition.featureId,
            displayName = definition.DisplayName,
            requirementResults = new List<UnlockRequirementEvaluation>()
        };

        if (!evaluationStack.Add(definition))
        {
            result.isUnlocked = false;
            result.requirementResults.Add(
                new UnlockRequirementEvaluation
                {
                    passed = false,
                    requirementType = UnlockRequirementType.FeatureUnlocked,
                    message =
                        $"Circular unlock dependency detected at " +
                        $"{definition.DisplayName}."
                });

            return result;
        }

        List<UnlockRequirement> requirements = definition.requirements;

        if (requirements == null || requirements.Count == 0)
        {
            result.isUnlocked = definition.noRequirementsMeansUnlocked;

            if (!result.isUnlocked)
            {
                result.requirementResults.Add(
                    new UnlockRequirementEvaluation
                    {
                        passed = false,
                        message =
                            $"{definition.DisplayName} has no configured " +
                            "unlock requirements and is set to remain locked."
                    });
            }

            evaluationStack.Remove(definition);
            return result;
        }

        bool unlocked =
            definition.requirementMode == UnlockRequirementGroupMode.All;

        for (int i = 0; i < requirements.Count; i++)
        {
            UnlockRequirementEvaluation requirementResult =
                EvaluateRequirement(requirements[i], evaluationStack);

            result.requirementResults.Add(requirementResult);

            if (definition.requirementMode == UnlockRequirementGroupMode.All)
                unlocked &= requirementResult.passed;
            else
                unlocked |= requirementResult.passed;
        }

        result.isUnlocked = unlocked;
        evaluationStack.Remove(definition);
        return result;
    }

    private static UnlockRequirementEvaluation EvaluateRequirement(
        UnlockRequirement requirement,
        HashSet<UnlockDefinition> evaluationStack)
    {
        if (requirement == null)
        {
            return new UnlockRequirementEvaluation
            {
                passed = false,
                message = "An unlock requirement entry is empty."
            };
        }

        UnlockRequirementEvaluation result;

        switch (requirement.requirementType)
        {
            case UnlockRequirementType.PlayerRankAtLeast:
                result = EvaluateRank(requirement);
                break;

            case UnlockRequirementType.PlayerXpAtLeast:
                result = EvaluateXp(requirement);
                break;

            case UnlockRequirementType.GoldAtLeast:
                result = EvaluateGold(requirement);
                break;

            case UnlockRequirementType.DiamondsAtLeast:
                result = EvaluateDiamonds(requirement);
                break;

            case UnlockRequirementType.MuseumPointsAtLeast:
                result = EvaluateMuseumPoints(requirement);
                break;

            case UnlockRequirementType.MuseumMilestoneClaimed:
                result = EvaluateMuseumMilestone(requirement);
                break;

            case UnlockRequirementType.ClaimedMuseumMilestonesAtLeast:
                result = EvaluateClaimedMilestoneCount(requirement);
                break;

            case UnlockRequirementType.CompletedTradeupsAtLeast:
                result = EvaluateCompletedTradeups(requirement);
                break;

            case UnlockRequirementType.ContainerCompletionAtLeast:
                result = EvaluateContainerCompletion(requirement);
                break;

            case UnlockRequirementType.UpgradeLevelAtLeast:
                result = EvaluateUpgradeLevel(requirement);
                break;

            case UnlockRequirementType.FeatureUnlocked:
                result = EvaluateRequiredFeature(
                    requirement,
                    evaluationStack);
                break;

            case UnlockRequirementType.OpeningSlotsAtLeast:
                result = EvaluateOpeningSlots(requirement);
                break;

            case UnlockRequirementType.StoragePagesAtLeast:
                result = EvaluateStoragePages(requirement);
                break;

            default:
                result = CreateResult(
                    requirement.requirementType,
                    false,
                    $"Unsupported unlock requirement: " +
                    $"{requirement.requirementType}.");
                break;
        }

        if (!result.passed &&
            !string.IsNullOrWhiteSpace(requirement.lockedReasonOverride))
        {
            result.message = requirement.lockedReasonOverride.Trim();
        }

        return result;
    }

    private static UnlockRequirementEvaluation EvaluateRank(
        UnlockRequirement requirement)
    {
        SaveManager save = SaveManager.Instance;

        if (save == null)
            return MissingSaveManager(requirement.requirementType);

        PlayerRank current = save.CurrentRank;
        bool passed = (int)current >= (int)requirement.minimumRank;

        return CreateResult(
            requirement.requirementType,
            passed,
            passed
                ? "Rank requirement met."
                : $"Requires " +
                  $"{PlayerProgressUtility.GetRankDisplayName(requirement.minimumRank)}. " +
                  $"Current rank: {PlayerProgressUtility.GetRankDisplayName(current)}.");
    }

    private static UnlockRequirementEvaluation EvaluateXp(
        UnlockRequirement requirement)
    {
        SaveManager save = SaveManager.Instance;

        if (save == null)
            return MissingSaveManager(requirement.requirementType);

        int target = Math.Max(0, requirement.minimumXp);
        bool passed = save.XP >= target;

        return CreateResult(
            requirement.requirementType,
            passed,
            passed
                ? "XP requirement met."
                : $"Requires {target:N0} XP. Current XP: {save.XP:N0}.");
    }

    private static UnlockRequirementEvaluation EvaluateGold(
        UnlockRequirement requirement)
    {
        SaveManager save = SaveManager.Instance;

        if (save == null)
            return MissingSaveManager(requirement.requirementType);

        float target = Math.Max(0f, requirement.minimumGold);
        bool passed = save.Gold + 0.0001f >= target;

        return CreateResult(
            requirement.requirementType,
            passed,
            passed
                ? "Gold requirement met."
                : $"Requires {target:N2} Gold. Current Gold: {save.Gold:N2}.");
    }

    private static UnlockRequirementEvaluation EvaluateDiamonds(
        UnlockRequirement requirement)
    {
        SaveManager save = SaveManager.Instance;

        if (save == null)
            return MissingSaveManager(requirement.requirementType);

        int target = Math.Max(0, requirement.minimumDiamonds);
        bool passed = save.Diamonds >= target;

        return CreateResult(
            requirement.requirementType,
            passed,
            passed
                ? "Diamond requirement met."
                : $"Requires {target:N0} Diamonds. " +
                  $"Current Diamonds: {save.Diamonds:N0}.");
    }

    private static UnlockRequirementEvaluation EvaluateMuseumPoints(
        UnlockRequirement requirement)
    {
        SaveManager save = SaveManager.Instance;

        if (save == null)
            return MissingSaveManager(requirement.requirementType);

        if (save.Museum == null)
        {
            return CreateResult(
                requirement.requirementType,
                false,
                "Museum progression state is unavailable.");
        }

        double target = Math.Max(0d, requirement.minimumMuseumPoints);
        double current = Math.Max(0d, save.Museum.museumPoints);
        bool passed = current >= target;

        return CreateResult(
            requirement.requirementType,
            passed,
            passed
                ? "Museum Point requirement met."
                : $"Requires {target:N0} Museum Points. " +
                  $"Current Museum Points: {current:N0}.");
    }

    private static UnlockRequirementEvaluation EvaluateMuseumMilestone(
        UnlockRequirement requirement)
    {
        SaveManager save = SaveManager.Instance;

        if (save == null)
            return MissingSaveManager(requirement.requirementType);

        string milestoneId = requirement.requiredMilestoneId != null
            ? requirement.requiredMilestoneId.Trim()
            : "";

        if (string.IsNullOrWhiteSpace(milestoneId))
        {
            return CreateResult(
                requirement.requirementType,
                false,
                "No Museum milestone ID is configured.");
        }

        bool passed = ContainsId(
            save.Museum != null
                ? save.Museum.claimedMilestoneIds
                : null,
            milestoneId);

        return CreateResult(
            requirement.requirementType,
            passed,
            passed
                ? "Museum milestone requirement met."
                : $"Requires claimed Museum milestone '{milestoneId}'.");
    }

    private static UnlockRequirementEvaluation EvaluateClaimedMilestoneCount(
        UnlockRequirement requirement)
    {
        SaveManager save = SaveManager.Instance;

        if (save == null)
            return MissingSaveManager(requirement.requirementType);

        int target = Math.Max(0, requirement.minimumClaimedMilestones);
        int current = CountUniqueIds(
            save.Museum != null
                ? save.Museum.claimedMilestoneIds
                : null);

        bool passed = current >= target;

        return CreateResult(
            requirement.requirementType,
            passed,
            passed
                ? "Museum Staircase requirement met."
                : $"Requires {target:N0} claimed Museum Staircase steps. " +
                  $"Current claimed steps: {current:N0}.");
    }

    private static UnlockRequirementEvaluation EvaluateCompletedTradeups(
        UnlockRequirement requirement)
    {
        SaveManager save = SaveManager.Instance;

        if (save == null)
            return MissingSaveManager(requirement.requirementType);

        int target = Math.Max(0, requirement.minimumCompletedTradeups);
        int current = save.Tradeups != null
            ? Math.Max(0, save.Tradeups.completedTradeups)
            : 0;

        bool passed = current >= target;

        return CreateResult(
            requirement.requirementType,
            passed,
            passed
                ? "Tradeup requirement met."
                : $"Requires {target:N0} completed tradeups. " +
                  $"Current tradeups: {current:N0}.");
    }

    private static UnlockRequirementEvaluation EvaluateContainerCompletion(
        UnlockRequirement requirement)
    {
        if (requirement.requiredContainer == null)
        {
            return CreateResult(
                requirement.requirementType,
                false,
                "No container is assigned to this completion requirement.");
        }

        if (requirement.minimumContainerTier == ContainerCompletionTier.None)
        {
            return CreateResult(
                requirement.requirementType,
                false,
                "The required container completion tier is not configured.");
        }

        ContainerProgressManager progress =
            ContainerProgressManager.Instance;

        if (progress == null)
        {
            return CreateResult(
                requirement.requirementType,
                false,
                "Container progression is unavailable.");
        }

        ContainerCompletionTier current =
            progress.GetCompletionTier(requirement.requiredContainer);

        bool passed =
            (int)current >= (int)requirement.minimumContainerTier;

        string containerName =
            !string.IsNullOrWhiteSpace(requirement.requiredContainer.caseName)
                ? requirement.requiredContainer.caseName
                : requirement.requiredContainer.name;

        return CreateResult(
            requirement.requirementType,
            passed,
            passed
                ? "Container completion requirement met."
                : $"Requires {requirement.minimumContainerTier} Completion " +
                  $"for {containerName}. Current tier: {current}.");
    }

    private static UnlockRequirementEvaluation EvaluateUpgradeLevel(
        UnlockRequirement requirement)
    {
        SaveManager save = SaveManager.Instance;

        if (save == null)
            return MissingSaveManager(requirement.requirementType);

        string upgradeId = requirement.requiredUpgradeId != null
            ? requirement.requiredUpgradeId.Trim()
            : "";

        if (string.IsNullOrWhiteSpace(upgradeId))
        {
            return CreateResult(
                requirement.requirementType,
                false,
                "No required upgrade ID is configured.");
        }

        int target = Math.Max(0, requirement.minimumUpgradeLevel);
        int current = GetUpgradeLevel(save.Upgrades, upgradeId);
        bool passed = current >= target;

        return CreateResult(
            requirement.requirementType,
            passed,
            passed
                ? "Upgrade requirement met."
                : $"Requires upgrade '{upgradeId}' level {target}. " +
                  $"Current level: {current}.");
    }

    private static UnlockRequirementEvaluation EvaluateRequiredFeature(
        UnlockRequirement requirement,
        HashSet<UnlockDefinition> evaluationStack)
    {
        if (requirement.requiredFeature == null)
        {
            return CreateResult(
                requirement.requirementType,
                false,
                "No required feature definition is assigned.");
        }

        UnlockEvaluationResult nested = EvaluateInternal(
            requirement.requiredFeature,
            evaluationStack);

        return CreateResult(
            requirement.requirementType,
            nested.isUnlocked,
            nested.isUnlocked
                ? $"Required feature {nested.displayName} is unlocked."
                : $"Requires {nested.displayName}. " +
                  nested.FirstFailureReason);
    }

    private static UnlockRequirementEvaluation EvaluateOpeningSlots(
        UnlockRequirement requirement)
    {
        SaveManager save = SaveManager.Instance;

        if (save == null)
            return MissingSaveManager(requirement.requirementType);

        int target = Math.Max(1, requirement.minimumOpeningSlots);
        int current = save.OwnedOpeningSlots;
        bool passed = current >= target;

        return CreateResult(
            requirement.requirementType,
            passed,
            passed
                ? "Opening-slot requirement met."
                : $"Requires {target} owned opening slots. " +
                  $"Current slots: {current}.");
    }

    private static UnlockRequirementEvaluation EvaluateStoragePages(
        UnlockRequirement requirement)
    {
        InventoryManager inventory = InventoryManager.Instance;

        if (inventory == null)
        {
            return CreateResult(
                requirement.requirementType,
                false,
                "Inventory progression is unavailable.");
        }

        int target = Math.Max(1, requirement.minimumStoragePages);
        int current = inventory.UnlockedStoragePages;
        bool passed = current >= target;

        return CreateResult(
            requirement.requirementType,
            passed,
            passed
                ? "Storage requirement met."
                : $"Requires {target} unlocked storage pages. " +
                  $"Current pages: {current}.");
    }

    private static int GetUpgradeLevel(
        UpgradeStateSaveData upgradeState,
        string upgradeId)
    {
        if (upgradeState == null ||
            upgradeState.upgradeLevels == null ||
            string.IsNullOrWhiteSpace(upgradeId))
        {
            return 0;
        }

        for (int i = 0; i < upgradeState.upgradeLevels.Count; i++)
        {
            UpgradeLevelSaveData entry =
                upgradeState.upgradeLevels[i];

            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.upgradeId))
            {
                continue;
            }

            if (string.Equals(
                    entry.upgradeId.Trim(),
                    upgradeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Math.Max(0, entry.level);
            }
        }

        return 0;
    }

    private static bool ContainsId(
        List<string> values,
        string target)
    {
        if (values == null || string.IsNullOrWhiteSpace(target))
            return false;

        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(
                    values[i],
                    target,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountUniqueIds(List<string> values)
    {
        if (values == null || values.Count == 0)
            return 0;

        HashSet<string> unique =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < values.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                unique.Add(values[i].Trim());
        }

        return unique.Count;
    }

    private static UnlockRequirementEvaluation MissingSaveManager(
        UnlockRequirementType type)
    {
        return CreateResult(
            type,
            false,
            "Player progression is unavailable because SaveManager is missing.");
    }

    private static UnlockRequirementEvaluation CreateResult(
        UnlockRequirementType type,
        bool passed,
        string message)
    {
        return new UnlockRequirementEvaluation
        {
            requirementType = type,
            passed = passed,
            message = message ?? ""
        };
    }
}
