using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class MuseumBulkDonationPlanEntry
{
    public string donationKey;
    public string instanceId;
    public InventoryItem item;
    public MuseumDonationPreview preview;
    public int warningCount;
}

public sealed class MuseumBulkDonationPlan
{
    public SkinData skin;
    public readonly List<MuseumBulkDonationPlanEntry> entries =
        new List<MuseumBulkDonationPlanEntry>();
    public int alreadyFilledSlots;
    public int unfilledSlotsWithoutUsableItem;
    public int entriesWithWarnings;
    public float totalMarketValue;
    public double estimatedMuseumPoints;

    public int DonationCount => entries.Count;
}

public sealed class MuseumBulkDonationResult
{
    public int attempted;
    public int donated;
    public int failed;
    public float donatedMarketValue;
    public double museumPointsAwarded;
    public string firstFailure;

    public bool success => donated > 0 && failed == 0;
}

/// <summary>
/// Builds and executes a conservative per-skin bulk donation. It fills each
/// currently empty wear/variant slot with at most one owned item, preferring the
/// candidate ordering supplied by MuseumService (fewest warnings, then lowest
/// value). Warning-only items remain eligible. Hard protection rules such as
/// Favorite, Trophy Room use, locked exhibits and filled slots are respected.
/// </summary>
public static class MuseumBulkDonationUtility
{
    public static MuseumBulkDonationPlan BuildPlan(
        MuseumService service,
        MuseumSkinEntry skinEntry)
    {
        MuseumBulkDonationPlan plan = new MuseumBulkDonationPlan
        {
            skin = skinEntry != null ? skinEntry.skin : null
        };

        if (service == null ||
            skinEntry == null ||
            skinEntry.skin == null ||
            skinEntry.slots == null ||
            InventoryManager.Instance == null)
        {
            return plan;
        }

        HashSet<string> reservedInstanceIds =
            new HashSet<string>(StringComparer.Ordinal);

        for (int slotIndex = 0;
             slotIndex < skinEntry.slots.Count;
             slotIndex++)
        {
            MuseumSlotEntry slot = skinEntry.slots[slotIndex];

            if (slot == null)
                continue;

            if (slot.donated || service.HasDonatedSlot(slot.donationKey))
            {
                plan.alreadyFilledSlots++;
                continue;
            }

            IReadOnlyList<MuseumDonationCandidate> candidates =
                service.GetDonationCandidates(slot);
            MuseumDonationCandidate selected = null;

            if (candidates != null)
            {
                for (int candidateIndex = 0;
                     candidateIndex < candidates.Count;
                     candidateIndex++)
                {
                    MuseumDonationCandidate candidate = candidates[candidateIndex];

                    if (candidate == null ||
                        !candidate.selectable ||
                        candidate.item == null ||
                        candidate.item.favorite ||
                        string.IsNullOrWhiteSpace(candidate.instanceId) ||
                        reservedInstanceIds.Contains(candidate.instanceId))
                    {
                        continue;
                    }

                    selected = candidate;
                    break;
                }
            }

            if (selected == null)
            {
                plan.unfilledSlotsWithoutUsableItem++;
                continue;
            }

            reservedInstanceIds.Add(selected.instanceId);

            MuseumBulkDonationPlanEntry planEntry =
                new MuseumBulkDonationPlanEntry
                {
                    donationKey = slot.donationKey,
                    instanceId = selected.instanceId,
                    item = selected.item,
                    preview = selected.preview,
                    warningCount = selected.WarningCount
                };

            plan.entries.Add(planEntry);
            plan.totalMarketValue += Mathf.Max(0f, selected.item.marketValue);

            if (selected.preview != null)
            {
                plan.estimatedMuseumPoints +=
                    Math.Max(0d, selected.preview.MuseumPoints);
            }

            if (selected.WarningCount > 0)
                plan.entriesWithWarnings++;
        }

        return plan;
    }

    public static MuseumBulkDonationResult Execute(
        MuseumService service,
        MuseumBulkDonationPlan plan)
    {
        MuseumBulkDonationResult result = new MuseumBulkDonationResult();

        if (service == null || plan == null || plan.entries == null)
            return result;

        result.attempted = plan.entries.Count;

        // Execute the frozen plan rather than rebuilding after every successful
        // donation. Every entry targets a unique Museum slot and inventory ID.
        for (int i = 0; i < plan.entries.Count; i++)
        {
            MuseumBulkDonationPlanEntry entry = plan.entries[i];

            if (entry == null || string.IsNullOrWhiteSpace(entry.instanceId))
            {
                RecordFailure(result, "A planned inventory item was unavailable.");
                continue;
            }

            MuseumDonationResult donation = service.Donate(entry.instanceId);

            if (donation == null || !donation.success)
            {
                string reason = donation != null &&
                                !string.IsNullOrWhiteSpace(donation.message)
                    ? donation.message
                    : "An item could not be donated.";

                RecordFailure(result, reason);
                continue;
            }

            result.donated++;
            result.donatedMarketValue += entry.item != null
                ? Mathf.Max(0f, entry.item.marketValue)
                : 0f;
            result.museumPointsAwarded +=
                Math.Max(0d, donation.museumPointsAwarded);
        }

        return result;
    }

    private static void RecordFailure(
        MuseumBulkDonationResult result,
        string reason)
    {
        result.failed++;

        if (string.IsNullOrWhiteSpace(result.firstFailure))
            result.firstFailure = reason ?? "Bulk donation failed.";
    }
}
