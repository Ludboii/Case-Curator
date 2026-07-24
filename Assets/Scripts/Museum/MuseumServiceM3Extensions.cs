using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// M3 query and instance-ID command surface layered on MuseumService.
/// </summary>
public static class MuseumServiceM3Extensions
{
    public static MuseumDonationPreview PreviewDonation(
        this MuseumService service,
        string instanceId)
    {
        if (service == null || InventoryManager.Instance == null)
        {
            return MuseumDonationPreview.Rejected(
                MuseumDonationFailureReason.ServiceUnavailable,
                "Museum or inventory services are unavailable.");
        }

        InventoryItem item = InventoryManager.Instance.GetItemByInstanceId(instanceId);

        if (item != null && item.skin != null)
        {
            // Repair the legacy mirror before the authoritative service evaluates
            // the item. Some knife-generation paths marked all knife finishes as
            // vanilla, which made finishes such as Karambit | Stained impossible
            // to match against their wear-based Museum slots.
            item.isVanilla = item.skin.isVanilla;
        }

        MuseumDonationPreview preview = service.PreviewDonation(item);

        if (preview == null || !preview.canDonate)
            return preview;

        MuseumSlotEntry slot = service.FindCatalogSlot(preview.donationKey);

        if (slot == null)
        {
            return MuseumDonationPreview.Rejected(
                MuseumDonationFailureReason.SlotUnavailable,
                "This exact skin, wear and variant is not part of the active Museum catalog.",
                item);
        }

        MuseumSlotUnlockState unlock = service.EvaluateSlotUnlock(slot);

        if (unlock != null && !unlock.isUnlocked)
        {
            return MuseumDonationPreview.Rejected(
                MuseumDonationFailureReason.SlotLocked,
                unlock.reason,
                item);
        }

        return preview;
    }

    public static MuseumDonationResult Donate(
        this MuseumService service,
        string instanceId)
    {
        MuseumDonationPreview preview = service.PreviewDonation(instanceId);

        if (preview == null || !preview.canDonate)
            return MuseumDonationResult.Failed(preview);

        return service.Donate(preview.item);
    }

    public static IReadOnlyList<MuseumDonationCandidate> GetDonationCandidates(
        this MuseumService service,
        MuseumSlotEntry slot)
    {
        List<MuseumDonationCandidate> candidates = new List<MuseumDonationCandidate>();

        if (service == null || slot == null || slot.skin == null ||
            InventoryManager.Instance == null)
        {
            return candidates;
        }

        List<InventoryItem> inventory = InventoryManager.Instance.GetItemsCopy();
        List<InventoryItem> comparable = new List<InventoryItem>();

        for (int i = 0; i < inventory.Count; i++)
        {
            InventoryItem item = inventory[i];

            if (item != null && item.skin != null)
                item.isVanilla = item.skin.isVanilla;

            if (item != null && IsSameSkinAndVariant(item, slot.skin, slot.variant))
                comparable.Add(item);
        }

        for (int i = 0; i < inventory.Count; i++)
        {
            InventoryItem item = inventory[i];

            if (!MatchesSlot(item, slot))
                continue;

            MuseumDonationPreview preview = service.PreviewDonation(item.instanceId);
            MuseumDonationCandidate candidate = new MuseumDonationCandidate
            {
                instanceId = item.instanceId,
                item = item,
                preview = preview,
                selectable = preview != null && preview.canDonate,
                blockedReason = preview != null && !preview.canDonate
                    ? preview.message
                    : ""
            };

            AddCandidateWarnings(service, candidate, comparable);
            candidates.Add(candidate);
        }

        candidates.Sort(CompareCandidates);
        return candidates;
    }

    public static MuseumSlotEntry FindCatalogSlot(
        this MuseumService service,
        string donationKey)
    {
        if (service == null || string.IsNullOrWhiteSpace(donationKey))
            return null;

        MuseumCatalogSnapshot snapshot = service.GetCatalogSnapshot(false);

        if (snapshot == null || snapshot.wings == null)
            return null;

        for (int wi = 0; wi < snapshot.wings.Count; wi++)
        {
            MuseumWingEntry wing = snapshot.wings[wi];
            if (wing == null || wing.categories == null) continue;

            for (int ci = 0; ci < wing.categories.Count; ci++)
            {
                MuseumCategoryEntry category = wing.categories[ci];
                if (category == null || category.weapons == null) continue;

                for (int wei = 0; wei < category.weapons.Count; wei++)
                {
                    MuseumWeaponEntry weapon = category.weapons[wei];
                    if (weapon == null || weapon.skins == null) continue;

                    for (int si = 0; si < weapon.skins.Count; si++)
                    {
                        MuseumSkinEntry skin = weapon.skins[si];
                        if (skin == null || skin.slots == null) continue;

                        for (int sli = 0; sli < skin.slots.Count; sli++)
                        {
                            MuseumSlotEntry slot = skin.slots[sli];

                            if (slot != null && string.Equals(
                                    slot.donationKey,
                                    donationKey,
                                    StringComparison.Ordinal))
                            {
                                return slot;
                            }
                        }
                    }
                }
            }
        }

        return null;
    }

    public static MuseumSlotUnlockState EvaluateSlotUnlock(
        this MuseumService service,
        MuseumSlotEntry targetSlot)
    {
        MuseumSlotUnlockState result = new MuseumSlotUnlockState
        {
            slot = targetSlot,
            isUnlocked = true
        };

        if (service == null || targetSlot == null)
        {
            result.isUnlocked = false;
            result.reason = "This Museum slot is unavailable.";
            return result;
        }

        MuseumCatalogSnapshot snapshot = service.GetCatalogSnapshot(false);

        if (snapshot == null || snapshot.wings == null)
            return result;

        for (int wi = 0; wi < snapshot.wings.Count; wi++)
        {
            MuseumWingEntry wing = snapshot.wings[wi];
            if (wing == null || wing.categories == null) continue;

            for (int ci = 0; ci < wing.categories.Count; ci++)
            {
                MuseumCategoryEntry category = wing.categories[ci];
                if (category == null || category.weapons == null) continue;

                for (int wei = 0; wei < category.weapons.Count; wei++)
                {
                    MuseumWeaponEntry weapon = category.weapons[wei];
                    if (weapon == null || weapon.skins == null) continue;

                    for (int si = 0; si < weapon.skins.Count; si++)
                    {
                        MuseumSkinEntry skin = weapon.skins[si];
                        if (skin == null || skin.slots == null) continue;

                        for (int sli = 0; sli < skin.slots.Count; sli++)
                        {
                            MuseumSlotEntry slot = skin.slots[sli];

                            if (slot == null || !string.Equals(
                                    slot.donationKey,
                                    targetSlot.donationKey,
                                    StringComparison.Ordinal))
                            {
                                continue;
                            }

                            result.wing = wing;
                            result.category = category;
                            result.skin = skin;
                            result.slot = slot;

                            bool wingUnlocked = wing.config == null ||
                                wing.config.unlockDefinition == null ||
                                UnlockEvaluator.IsUnlocked(wing.config.unlockDefinition);

                            if (!wingUnlocked)
                            {
                                result.isUnlocked = false;
                                result.reason = $"{wing.DisplayName} is locked.";
                                return result;
                            }

                            bool categoryUnlocked = category.config == null ||
                                category.config.unlockDefinition == null ||
                                UnlockEvaluator.IsUnlocked(category.config.unlockDefinition);

                            if (!categoryUnlocked)
                            {
                                result.isUnlocked = false;
                                result.reason = $"{category.DisplayName} is locked.";
                            }

                            return result;
                        }
                    }
                }
            }
        }

        result.isUnlocked = false;
        result.reason = "This Museum slot is not part of the active catalog.";
        return result;
    }

    private static void AddCandidateWarnings(
        MuseumService service,
        MuseumDonationCandidate candidate,
        List<InventoryItem> comparable)
    {
        if (candidate == null || candidate.item == null)
            return;

        InventoryItem item = candidate.item;
        int ownedCopies = 0;
        double bestFloat = double.MaxValue;
        float highestValue = 0f;

        for (int i = 0; i < comparable.Count; i++)
        {
            InventoryItem other = comparable[i];
            if (other == null) continue;

            ownedCopies++;
            highestValue = Mathf.Max(highestValue, other.marketValue);

            bool vanilla = other.skin != null && other.skin.isVanilla;
            if (!vanilla && other.floatValue >= 0d)
                bestFloat = Math.Min(bestFloat, other.floatValue);
        }

        if (ownedCopies <= 1)
            AddWarning(candidate, MuseumDonationWarningType.OnlyOwnedCopy,
                "This is your only owned copy of this skin and variant.", true);

        bool itemVanilla = item.skin != null && item.skin.isVanilla;

        if (!itemVanilla && bestFloat < double.MaxValue &&
            Math.Abs(item.floatValue - bestFloat) <= 0.0000001d)
        {
            AddWarning(candidate, MuseumDonationWarningType.BestFloatOwned,
                "This is your best-float owned copy of this skin and variant.", true);
        }

        if (Mathf.Abs(item.marketValue - highestValue) <= 0.0001f && highestValue > 0f)
        {
            AddWarning(candidate, MuseumDonationWarningType.HighestValueOwned,
                "This is your highest-value owned copy of this skin and variant.", true);
        }

        if ((int)item.patternTier > 0)
        {
            AddWarning(candidate, MuseumDonationWarningType.RarePattern,
                $"This item has pattern tier {item.patternTier}.", true);
        }

        MuseumBalanceData balance = service != null ? service.Balance : null;

        if (balance != null && balance.marketValueBonus != null &&
            item.marketValue >= balance.marketValueBonus.minimumMarketValue)
        {
            AddWarning(candidate, MuseumDonationWarningType.HighMarketValue,
                $"High-value item: {item.marketValue:0.##} Gold. Its Museum reward includes a capped market-value bonus.",
                item.marketValue >= 1000f);
        }
    }

    private static void AddWarning(
        MuseumDonationCandidate candidate,
        MuseumDonationWarningType type,
        string message,
        bool severe)
    {
        candidate.warnings.Add(new MuseumDonationWarning
        {
            type = type,
            message = message,
            severe = severe
        });
    }

    private static int CompareCandidates(
        MuseumDonationCandidate a,
        MuseumDonationCandidate b)
    {
        if (ReferenceEquals(a, b)) return 0;
        if (a == null) return 1;
        if (b == null) return -1;

        int selectable = b.selectable.CompareTo(a.selectable);
        if (selectable != 0) return selectable;

        int warnings = a.WarningCount.CompareTo(b.WarningCount);
        if (warnings != 0) return warnings;

        int value = a.MarketValue.CompareTo(b.MarketValue);
        if (value != 0) return value;

        return a.AcquisitionSequence.CompareTo(b.AcquisitionSequence);
    }

    private static bool MatchesSlot(InventoryItem item, MuseumSlotEntry slot)
    {
        if (item == null || item.skin == null || slot == null || slot.skin == null)
            return false;

        if (!IsSameSkin(item.skin, slot.skin))
            return false;

        bool vanilla = item.skin.isVanilla;

        if (vanilla != slot.isVanilla)
            return false;

        if (MuseumDonationKeyUtility.GetVariant(item) != slot.variant)
            return false;

        int wearIndex = vanilla ? -1 : MuseumDonationKeyUtility.GetWearIndex(item);
        return wearIndex == slot.wearIndex;
    }

    private static bool IsSameSkinAndVariant(
        InventoryItem item,
        SkinData skin,
        MuseumDonationVariant variant)
    {
        return item != null && item.skin != null &&
               IsSameSkin(item.skin, skin) &&
               MuseumDonationKeyUtility.GetVariant(item) == variant;
    }

    private static bool IsSameSkin(SkinData a, SkinData b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;

        return !string.IsNullOrWhiteSpace(a.apiId) &&
               !string.IsNullOrWhiteSpace(b.apiId) &&
               string.Equals(a.apiId, b.apiId, StringComparison.Ordinal);
    }
}
