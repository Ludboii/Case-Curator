using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public const int CurrentVersion = 2;

    public int saveVersion = CurrentVersion;
    public SaveMetadataSaveData metadata = new SaveMetadataSaveData();
    public PlayerProfileSaveData player = new PlayerProfileSaveData();
    public InventoryStateSaveData inventories = new InventoryStateSaveData();
    public UpgradeStateSaveData upgrades = new UpgradeStateSaveData();
    public MuseumStateSaveData museum = new MuseumStateSaveData();
    public TradeupStateSaveData tradeups = new TradeupStateSaveData();
    public ContainerProgressSaveData containerProgress = new ContainerProgressSaveData();
}

[Serializable]
public class SaveVersionHeader
{
    public int saveVersion;
}

/// <summary>
/// Exact layout used by SaveData 1.0. This class is retained only so existing
/// saves can be read and migrated into SaveData 2.0.
/// </summary>
[Serializable]
public class SaveDataV1
{
    public int saveVersion = 1;

    public float gold;
    public int diamonds;
    public int xp;

    public int unlockedStoragePages = 1;
    public int itemsPerStoragePage = 50;

    public List<InventoryItemSaveData> inventory =
        new List<InventoryItemSaveData>();

    public List<CaseInventoryEntrySaveData> caseInventory =
        new List<CaseInventoryEntrySaveData>();
}

[Serializable]
public class SaveMetadataSaveData
{
    public string saveId;
    public long createdUtcTicks;
    public long lastSavedUtcTicks;
}

[Serializable]
public class PlayerProfileSaveData
{
    public float gold;
    public int diamonds;
    public int xp;
}

[Serializable]
public class InventoryStateSaveData
{
    public int unlockedStoragePages = 1;
    public int itemsPerStoragePage = 50;

    public List<InventoryItemSaveData> skinInventory =
        new List<InventoryItemSaveData>();

    public List<CaseInventoryEntrySaveData> caseInventory =
        new List<CaseInventoryEntrySaveData>();
}

[Serializable]
public class UpgradeStateSaveData
{
    public int openingSlotsOwned = 1;

    // Generic ID/level storage for future upgrade trees. Existing systems can
    // add upgrades without changing the root save format again.
    public List<UpgradeLevelSaveData> upgradeLevels =
        new List<UpgradeLevelSaveData>();
}

[Serializable]
public class UpgradeLevelSaveData
{
    public string upgradeId;
    public int level;
}

[Serializable]
public class MuseumStateSaveData
{
    public double museumPoints;

    // M5 Museum idle income. Fractional values are retained so low rates do not
    // lose progress between claims or save/load cycles.
    public double unclaimedIdleGold;
    public double unclaimedIdleDiamonds;
    public long lastIdleGoldCalculationUtcTicks;
    public double lifetimeIdleGoldClaimed;
    public int lifetimeIdleDiamondsClaimed;

    // Donations are aggregated by donationKey to prevent the save from
    // growing by one full record for every donated item.
    public List<MuseumDonationRecordSaveData> donations =
        new List<MuseumDonationRecordSaveData>();

    // One record per manually claimed skin, weapon or category completion.
    // Reward keys include the wing ID so Arsenal, Souvenir and Rare Special
    // completion remain independent.
    public List<MuseumCompletionRewardClaimSaveData> claimedCompletionRewards =
        new List<MuseumCompletionRewardClaimSaveData>();

    public List<string> claimedMilestoneIds =
        new List<string>();

    public List<string> unlockedPlaqueIds =
        new List<string>();

    public MuseumPresentStateSaveData presents =
        new MuseumPresentStateSaveData();

    public GiftDeskSaveData giftDesk = new GiftDeskSaveData();
    public TrophyRoomSaveData trophyRoom = new TrophyRoomSaveData();
    public AutoAcquisitionStateSaveData automatedAcquisitions =
        new AutoAcquisitionStateSaveData();

    // Applied sticker crafts are keyed by the owning skin's stable inventory
    // instance ID. Keeping this additive sidecar inside the Museum-era save
    // state preserves existing SaveData 2.0 compatibility.
    public StickerSystemSaveData stickerSystem = new StickerSystemSaveData();
}

[Serializable]
public class MuseumPresentStateSaveData
{
    public List<MuseumPresentTierBalanceSaveData> tierBalances =
        new List<MuseumPresentTierBalanceSaveData>();

    // Prevents a claimed milestone from granting its fragment/present payload
    // more than once. This also makes M4.5 retroactive for milestones claimed
    // before the present system was added.
    public List<string> processedMilestoneRewardIds =
        new List<string>();

    public int totalPresentsOpened;
}

[Serializable]
public class MuseumPresentTierBalanceSaveData
{
    public MuseumPresentTier tier;
    public int fragments;
    public int presents;
    public int presentsOpened;
}

[Serializable]
public class MuseumDonationRecordSaveData
{
    public string donationKey;
    public string skinApiId;
    public int wearIndex;
    public bool statTrak;
    public bool souvenir;
    public bool isVanilla;

    public int donatedCount;
    public float totalMarketValueDonated;
    public double totalMuseumPointsAwarded;

    public double bestFloat = -1d;
    public float highestMarketValue;
}

[Serializable]
public class MuseumCompletionRewardClaimSaveData
{
    public string rewardKey;
    public MuseumCompletionRewardKind rewardKind;
    public string wingId;
    public string categoryId;
    public string displayName;
    public double museumPointsAwarded;
    public long claimedUtcTicks;
}

[Serializable]
public class GiftDeskSaveData
{
    public List<GiftShardBalanceSaveData> shardBalances =
        new List<GiftShardBalanceSaveData>();

    public List<string> claimedGiftIds =
        new List<string>();
}

[Serializable]
public class GiftShardBalanceSaveData
{
    public string museumBandId;
    public int shardAmount;
}

[Serializable]
public class TrophyRoomSaveData
{
    // Legacy cached value retained for existing saves and diagnostics. Runtime
    // availability is derived from the eleven purchased pedestal upgrades.
    public int unlockedSlots;

    public TrophyRoomFocus focus = TrophyRoomFocus.MuseumGoldIncome;

    public List<TrophyDisplaySlotSaveData> displayedItems =
        new List<TrophyDisplaySlotSaveData>();
}

[Serializable]
public class TrophyDisplaySlotSaveData
{
    public int slotIndex;

    // Legacy M7 prototype field. During migration, TrophyRoomService resolves
    // this item from InventoryManager and moves it into storedItem.
    public string inventoryItemInstanceId;

    // A displayed trophy is outside normal inventory capacity and candidate
    // lists, so its complete item state is persisted here until retrieval.
    public InventoryItemSaveData storedItem;
}

/// <summary>
/// Persistent state for the Automated Acquisitions Wing. Category licences and
/// research are permanent; processing budgets, UTC timers and Intake Vault
/// rewards survive closing the game.
/// </summary>
[Serializable]
public class AutoAcquisitionStateSaveData
{
    public List<string> ownedCategoryIds = new List<string>();
    public List<string> researchedContainerIds = new List<string>();

    public List<AutoAcquisitionLineSaveData> lines =
        new List<AutoAcquisitionLineSaveData>();

    public List<AutoAcquisitionPendingItemSaveData> intakeItems =
        new List<AutoAcquisitionPendingItemSaveData>();

    public long lastProcessingUtcTicks;

    public int lifetimeItemsProcessed;
    public double lifetimeGoldSpent;
    public double lifetimeValueReceived;
    public float bestPullMarketValue;
    public string bestPullSkinApiId;
    public int lifetimeRareSpecialPulls;
    public int lifetimeNewMuseumPlaques;
}

[Serializable]
public class AutoAcquisitionLineSaveData
{
    public int lineIndex;
    public string selectedContainerId;
    public double depositedGold;
    public bool active;
    public long nextCompletionUtcTicks;
    public bool pausedByCuratorAlert;
    public string pauseReason;
}

[Serializable]
public class AutoAcquisitionPendingItemSaveData
{
    public string rewardId;
    public string sourceContainerId;
    public string sourceContainerName;
    public int lineIndex;
    public long createdUtcTicks;
    public InventoryItemSaveData item;
    public bool exceptional;
    public string alertReason;
}

/// <summary>
/// Persistent lifetime state for the tradeup system. This is additive to
/// SaveData 2.0, so existing v2 saves deserialize with safe defaults.
/// </summary>
[Serializable]
public class TradeupStateSaveData
{
    public int completedTradeups;
    public int completedStandardTradeups;
    public int completedCovertTradeups;
    public int totalInputsConsumed;

    public float totalInputMarketValue;
    public float totalOutputMarketValue;
    public float bestOutputMarketValue;
    public string bestOutputSkinApiId;

    // -1 means no non-vanilla tradeup output has been recorded yet.
    public double lowestOutputFloat = -1d;
    public string lowestFloatOutputSkinApiId;

    // Kept here so the future upgrade system can affect the resolver without
    // another save-schema change.
    public int floatTuningLevel;

    // Bounded by SaveManager when records are added.
    public List<TradeupHistorySaveData> recentHistory =
        new List<TradeupHistorySaveData>();
}

/// <summary>
/// One completed tradeup record. The history contains enough information for
/// a future stats screen, result recap, debugging and save verification.
/// </summary>
[Serializable]
public class TradeupHistorySaveData
{
    public string tradeupId;
    public long completedUtcTicks;

    public Rarity inputRarity;
    public Rarity outputRarity;
    public int inputCount;
    public bool statTrak;
    public bool covertToRareSpecial;

    public double averageInputFloat;
    public float totalInputMarketValue;

    public string outputSkinApiId;
    public string outputInstanceId;
    public double outputFloat = -1d;
    public int outputPatternId = -1;
    public PatternTier outputPatternTier = PatternTier.None;
    public float outputMarketValue;

    // These preserve the exact consumed inputs and their source weighting.
    public List<string> inputInstanceIds = new List<string>();
    public List<string> inputSkinApiIds = new List<string>();
    public List<string> inputSourceApiIds = new List<string>();
}
