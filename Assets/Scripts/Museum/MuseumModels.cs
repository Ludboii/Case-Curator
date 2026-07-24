using System;
using System.Collections.Generic;

public enum MuseumDonationFailureReason
{
    None = 0,
    ServiceUnavailable = 1,
    MissingInventoryItem = 2,
    MissingSkinData = 3,
    MissingStableSkinId = 4,
    ItemNotOwned = 5,
    FavoriteItem = 6,
    TrophyRoomItem = 7,
    VariantDisabled = 8,
    SlotAlreadyFilled = 9,
    InventoryTransactionFailed = 10,
    SlotUnavailable = 11,
    SlotLocked = 12,
    ItemDoesNotMatchSlot = 13
}

public enum MuseumDonationWarningType
{
    OnlyOwnedCopy = 0,
    BestFloatOwned = 1,
    HighestValueOwned = 2,
    RarePattern = 3,
    HighMarketValue = 4
}

[Serializable]
public sealed class MuseumPointBreakdown
{
    // Legacy fields retained for existing UI and serialized compatibility.
    public double basePoints;
    public double wearMultiplier = 1d;

    public double rarityWearPoints;
    public double variantMultiplier = 1d;
    public double rareSpecialMultiplier = 1d;
    public double vanillaMultiplier = 1d;
    public double pointsBeforeMarketBonus;
    public double marketValueBonus;
    public double marketValueBonusRate;
    public double totalPoints;
}

public sealed class MuseumSlotUnlockState
{
    public bool isUnlocked = true;
    public string reason = "";
    public MuseumWingEntry wing;
    public MuseumCategoryEntry category;
    public MuseumSkinEntry skin;
    public MuseumSlotEntry slot;
}

public sealed class MuseumDonationWarning
{
    public MuseumDonationWarningType type;
    public string message;
    public bool severe;
}

public sealed class MuseumDonationCandidate
{
    public string instanceId;
    public InventoryItem item;
    public MuseumDonationPreview preview;
    public bool selectable;
    public string blockedReason;
    public List<MuseumDonationWarning> warnings =
        new List<MuseumDonationWarning>();

    public int WarningCount => warnings != null ? warnings.Count : 0;
    public float MarketValue => item != null ? item.marketValue : 0f;
    public long AcquisitionSequence => item != null ? item.acquisitionSequence : 0L;
}

public sealed class MuseumDonationPreview
{
    public bool canDonate;
    public MuseumDonationFailureReason failureReason;
    public string message;

    public InventoryItem item;
    public SkinData skin;
    public string donationKey;
    public int wearIndex;
    public MuseumWearTier wearTier;
    public MuseumDonationVariant variant;
    public bool isVanilla;
    public bool isFirstDonationForSlot;
    public MuseumPointBreakdown points;

    public double MuseumPoints =>
        points != null ? points.totalPoints : 0d;

    public static MuseumDonationPreview Rejected(
        MuseumDonationFailureReason reason,
        string message,
        InventoryItem item = null)
    {
        return new MuseumDonationPreview
        {
            canDonate = false,
            failureReason = reason,
            message = message ?? "Museum donation is unavailable.",
            item = item,
            skin = item != null ? item.skin : null
        };
    }
}

public sealed class MuseumDonationResult
{
    public bool success;
    public MuseumDonationFailureReason failureReason;
    public string message;
    public InventoryItem donatedItem;
    public MuseumDonationRecordSaveData donationRecord;
    public double museumPointsAwarded;
    public double totalMuseumPoints;

    public static MuseumDonationResult Failed(
        MuseumDonationPreview preview,
        string overrideMessage = null)
    {
        return new MuseumDonationResult
        {
            success = false,
            failureReason = preview != null
                ? preview.failureReason
                : MuseumDonationFailureReason.ServiceUnavailable,
            message = !string.IsNullOrWhiteSpace(overrideMessage)
                ? overrideMessage
                : preview != null
                    ? preview.message
                    : "Museum donation failed.",
            donatedItem = preview != null ? preview.item : null
        };
    }
}

public sealed class MuseumDonationSummary
{
    public SkinData skin;
    public int donatedSlots;
    public int totalDonations;
    public float totalMarketValueDonated;
    public float highestMarketValueDonated;
    public double totalMuseumPointsAwarded;
    public double bestFloat = -1d;
    public bool hasNormal;
    public bool hasStatTrak;
    public bool hasSouvenir;
}

public sealed class MuseumSlotEntry
{
    public string donationKey;
    public SkinData skin;
    public int wearIndex;
    public MuseumWearTier wearTier;
    public MuseumDonationVariant variant;
    public bool isVanilla;
    public bool donated;
}

public sealed class MuseumSkinEntry
{
    public SkinData skin;
    public string weaponName;
    public List<MuseumSlotEntry> slots = new List<MuseumSlotEntry>();

    public int TotalSlots => slots != null ? slots.Count : 0;

    public int DonatedSlots
    {
        get
        {
            int count = 0;

            if (slots == null)
                return count;

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].donated)
                    count++;
            }

            return count;
        }
    }
}

public sealed class MuseumWeaponEntry
{
    public string weaponName;
    public List<MuseumSkinEntry> skins = new List<MuseumSkinEntry>();
    public int totalSlots;
    public int donatedSlots;
}

public sealed class MuseumCategoryEntry
{
    public MuseumCategoryConfig config;
    public List<MuseumWeaponEntry> weapons = new List<MuseumWeaponEntry>();
    public int totalSlots;
    public int donatedSlots;

    public string CategoryId =>
        config != null ? config.categoryId : "";

    public string DisplayName =>
        config != null ? config.DisplayName : "Museum Category";
}

public sealed class MuseumWingEntry
{
    public MuseumWingConfig config;
    public List<MuseumCategoryEntry> categories =
        new List<MuseumCategoryEntry>();

    public int totalSlots;
    public int donatedSlots;

    public string WingId => config != null ? config.wingId : "";
    public string DisplayName =>
        config != null ? config.DisplayName : "Museum Wing";
}

public sealed class MuseumCatalogSnapshot
{
    public List<MuseumWingEntry> wings = new List<MuseumWingEntry>();
    public int totalSkins;
    public int totalSlots;
    public int donatedSlots;

    public float Completion01 =>
        totalSlots > 0
            ? donatedSlots / (float)totalSlots
            : 0f;
}
