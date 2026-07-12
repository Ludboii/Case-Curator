using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public const int CurrentVersion = 2;

    public int saveVersion = CurrentVersion;
    public long savedAtUtcTicks;

    public PlayerSaveData player = new PlayerSaveData();
    public InventorySaveData inventory = new InventorySaveData();
    public ProgressionSaveData progression = new ProgressionSaveData();
    public ContainerProgressSaveData containerProgress = new ContainerProgressSaveData();
    public MuseumSaveData museum = new MuseumSaveData();
    public TradeupSaveData tradeups = new TradeupSaveData();
}

[Serializable]
public class PlayerSaveData
{
    public float gold;
    public int diamonds;
    public int xp;
}

[Serializable]
public class InventorySaveData
{
    public int unlockedStoragePages = 1;
    public int itemsPerStoragePage = 50;

    public List<InventoryItemSaveData> items =
        new List<InventoryItemSaveData>();

    public List<CaseInventoryEntrySaveData> cases =
        new List<CaseInventoryEntrySaveData>();
}

[Serializable]
public class ProgressionSaveData
{
    public int unlockedOpeningSlots = 1;

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
public class MuseumSaveData
{
    public double museumPoints;
    public double lifetimeMuseumPoints;
    public float unclaimedIdleGold;
    public long lastIdleIncomeUtcTicks;

    public List<MuseumDonationSaveData> donations =
        new List<MuseumDonationSaveData>();

    public List<string> claimedMilestoneIds =
        new List<string>();

    public List<string> unlockedWeaponWingIds =
        new List<string>();

    public GiftDeskSaveData giftDesk = new GiftDeskSaveData();
    public TrophyRoomSaveData trophyRoom = new TrophyRoomSaveData();
}

[Serializable]
public class MuseumDonationSaveData
{
    public string museumEntryId;
    public string skinApiId;
    public int donatedCount;
    public double totalMuseumPointsEarned;
    public double bestFloat = -1d;
    public float highestMarketValue;
    public bool hasNormal;
    public bool hasStatTrak;
    public bool hasSouvenir;
}

[Serializable]
public class GiftDeskSaveData
{
    public int presentShards;

    public List<string> claimedGiftIds =
        new List<string>();
}

[Serializable]
public class TrophyRoomSaveData
{
    public List<TrophyDisplaySaveData> displays =
        new List<TrophyDisplaySaveData>();
}

[Serializable]
public class TrophyDisplaySaveData
{
    public string slotId;
    public string inventoryInstanceId;
}

[Serializable]
public class TradeupSaveData
{
    public int completedTradeups;

    public List<TradeupHistorySaveData> history =
        new List<TradeupHistorySaveData>();
}

[Serializable]
public class TradeupHistorySaveData
{
    public long completedAtUtcTicks;
    public string resultInstanceId;

    public List<string> inputInstanceIds =
        new List<string>();
}

// Exact layout used by the original v1 JSON file. It is retained only so
// existing players can be migrated into SaveData v2 without losing progress.
[Serializable]
public class LegacySaveDataV1
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
public class SaveVersionHeader
{
    public int saveVersion;
}
