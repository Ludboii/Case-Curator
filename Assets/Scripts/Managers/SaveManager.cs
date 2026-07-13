using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("Database")]
    public GameDatabase database;

    [Header("Player Values")]
    public float gold;
    public int diamonds;
    public int xp;

    [Header("SaveData 2.0 State")]
    [SerializeField] private UpgradeStateSaveData upgrades =
        new UpgradeStateSaveData();

    [SerializeField] private MuseumStateSaveData museum =
        new MuseumStateSaveData();

    [SerializeField] private TradeupStateSaveData tradeups =
        new TradeupStateSaveData();

    [SerializeField] private SaveMetadataSaveData metadata =
        new SaveMetadataSaveData();

    [SerializeField] private ContainerProgressSaveData containerProgressState =
        new ContainerProgressSaveData();

    [Header("Tradeup History")]
    [Tooltip("Maximum completed tradeups retained in SaveData 2.0 history.")]
    [SerializeField, Min(0)] private int maximumTradeupHistoryEntries = 100;

    [Header("Save Scheduling")]
    [Tooltip("Dirty game state is written at most once per interval.")]
    [SerializeField, Min(5f)] private float autosaveIntervalSeconds = 30f;

    [SerializeField] private bool saveWhenApplicationPauses = true;
    [SerializeField] private bool saveWhenApplicationQuits = true;

    public event Action OnCurrencyChanged;
    public event Action OnProgressChanged;
    public event Action OnTradeupStateChanged;

    public float Gold => gold;
    public int Diamonds => diamonds;
    public int XP => xp;
    public int SaveVersion => SaveData.CurrentVersion;
    public UpgradeStateSaveData Upgrades => upgrades;
    public MuseumStateSaveData Museum => museum;
    public TradeupStateSaveData Tradeups => tradeups;
    public bool IsDirty => saveDirty;

    public int OwnedOpeningSlots => Mathf.Clamp(
        upgrades != null ? upgrades.openingSlotsOwned : 1,
        1,
        3);

    public PlayerRank CurrentRank => PlayerProgressUtility.GetRankFromXP(xp);

    private string SavePath =>
        Path.Combine(Application.persistentDataPath, "casecatcher_save.json");

    private string BackupPath => SavePath + ".bak";
    private string TempPath => SavePath + ".tmp";

    private bool saveDirty;
    private bool isWritingSave;
    private float nextAutosaveTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "Duplicate SaveManager found, destroying: " + gameObject.name);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureRuntimeState();
        ScheduleNextAutosave();
    }

    private void Update()
    {
        if (!saveDirty || isWritingSave)
            return;

        if (Time.unscaledTime < nextAutosaveTime)
            return;

        SaveNow();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && saveWhenApplicationPauses && saveDirty)
            SaveNow();
    }

    private void OnApplicationQuit()
    {
        if (saveWhenApplicationQuits && saveDirty)
            SaveNow();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void MarkDirty()
    {
        if (saveDirty)
            return;

        saveDirty = true;
        ScheduleNextAutosave();
    }

    /// <summary>
    /// Compatibility entry point used throughout the project. It marks the
    /// unified SaveData 2.0 state dirty instead of writing immediately.
    /// </summary>
    public void SaveGame()
    {
        MarkDirty();
    }

    /// <summary>
    /// Writes immediately. Intended for explicit manual saves, application
    /// pause/quit, migrations and backup recovery.
    /// </summary>
    public bool SaveNow()
    {
        return TryWriteCurrentState();
    }

    public void AddGold(float amount)
    {
        if (amount <= 0f)
            return;

        gold += amount;
        MarkDirty();
        OnCurrencyChanged?.Invoke();
    }

    public bool SpendGold(float amount)
    {
        if (amount <= 0f)
            return true;

        if (gold < amount)
            return false;

        gold -= amount;
        MarkDirty();
        OnCurrencyChanged?.Invoke();
        return true;
    }

    public void AddDiamonds(int amount)
    {
        if (amount <= 0)
            return;

        diamonds += amount;
        MarkDirty();
        OnCurrencyChanged?.Invoke();
    }

    public bool SpendDiamonds(int amount)
    {
        if (amount <= 0)
            return true;

        if (diamonds < amount)
            return false;

        diamonds -= amount;
        MarkDirty();
        OnCurrencyChanged?.Invoke();
        return true;
    }

    public void AddXP(int amount)
    {
        if (amount <= 0)
            return;

        PlayerRank oldRank = CurrentRank;
        xp += amount;
        PlayerRank newRank = CurrentRank;

        MarkDirty();
        OnProgressChanged?.Invoke();

        if (newRank != oldRank)
        {
            Debug.Log(
                $"Rank up! {PlayerProgressUtility.GetRankDisplayName(oldRank)} -> " +
                $"{PlayerProgressUtility.GetRankDisplayName(newRank)}");
        }
    }

    public void SetXP(int amount)
    {
        int newValue = Mathf.Max(0, amount);

        if (xp == newValue)
            return;

        xp = newValue;
        MarkDirty();
        OnProgressChanged?.Invoke();
    }

    public bool SetOwnedOpeningSlots(int amount)
    {
        EnsureRuntimeState();

        int value = Mathf.Clamp(amount, 1, 3);

        if (upgrades.openingSlotsOwned == value)
            return false;

        upgrades.openingSlotsOwned = value;
        MarkDirty();
        OnProgressChanged?.Invoke();
        return true;
    }

    public bool SetTradeupFloatTuningLevel(int level)
    {
        EnsureRuntimeState();

        int value = Mathf.Max(0, level);

        if (tradeups.floatTuningLevel == value)
            return false;

        tradeups.floatTuningLevel = value;
        MarkDirty();
        OnTradeupStateChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Records one completed tradeup after the inventory transaction succeeds.
    /// The resolver should populate the exact inputs and output on the record.
    /// </summary>
    public void RecordCompletedTradeup(TradeupHistorySaveData record)
    {
        if (record == null)
        {
            Debug.LogWarning(
                "SaveManager: Cannot record a null tradeup history entry.");
            return;
        }

        EnsureRuntimeState();
        NormalizeTradeupHistoryRecord(record);

        tradeups.completedTradeups++;
        tradeups.totalInputsConsumed += Mathf.Max(0, record.inputCount);
        tradeups.totalInputMarketValue +=
            Mathf.Max(0f, record.totalInputMarketValue);
        tradeups.totalOutputMarketValue +=
            Mathf.Max(0f, record.outputMarketValue);

        if (record.covertToRareSpecial)
            tradeups.completedCovertTradeups++;
        else
            tradeups.completedStandardTradeups++;

        if (record.outputMarketValue > tradeups.bestOutputMarketValue)
        {
            tradeups.bestOutputMarketValue = record.outputMarketValue;
            tradeups.bestOutputSkinApiId = record.outputSkinApiId;
        }

        bool hasValidFloat = record.outputFloat >= 0d;
        bool isNewLowest =
            hasValidFloat &&
            (tradeups.lowestOutputFloat < 0d ||
             record.outputFloat < tradeups.lowestOutputFloat);

        if (isNewLowest)
        {
            tradeups.lowestOutputFloat = record.outputFloat;
            tradeups.lowestFloatOutputSkinApiId = record.outputSkinApiId;
        }

        if (maximumTradeupHistoryEntries > 0)
        {
            tradeups.recentHistory.Add(Clone(record));
            TrimTradeupHistory(tradeups, maximumTradeupHistoryEntries);
        }
        else
        {
            tradeups.recentHistory.Clear();
        }

        MarkDirty();
        OnTradeupStateChanged?.Invoke();
    }

    public void ResetTradeupStateForTesting()
    {
        tradeups = new TradeupStateSaveData();
        EnsureTradeupState(tradeups);
        MarkDirty();
        OnTradeupStateChanged?.Invoke();
    }

    public void LoadGame()
    {
        if (database == null)
        {
            Debug.LogError(
                "Cannot load: GameDatabase is not assigned on SaveManager.");
            return;
        }

        if (InventoryManager.Instance == null)
        {
            Debug.LogError("Cannot load: no InventoryManager found.");
            return;
        }

        bool loadedFromBackup = false;

        if (!TryLoadSaveData(
                SavePath,
                out SaveData data,
                out bool migrated))
        {
            if (!TryLoadSaveData(
                    BackupPath,
                    out data,
                    out migrated))
            {
                Debug.LogWarning(
                    $"No readable save found at {SavePath} or {BackupPath}.");
                return;
            }

            loadedFromBackup = true;
            Debug.LogWarning(
                "SaveManager: Loaded backup because the main save was unreadable.");
        }

        ApplySaveData(data);
        saveDirty = false;
        ScheduleNextAutosave();

        Debug.Log(
            $"Game loaded. SaveData {data.saveVersion}. " +
            $"Items: {InventoryManager.Instance.Count}. " +
            $"Cases: {(CaseInventoryManager.Instance != null ? CaseInventoryManager.Instance.TotalCaseCount : 0)}. " +
            $"Tradeups: {tradeups.completedTradeups}.");

        if (migrated || loadedFromBackup)
        {
            if (loadedFromBackup && File.Exists(SavePath))
                File.Delete(SavePath);

            MarkDirty();
            SaveNow();
        }
    }

    public void DeleteSave()
    {
        bool deleted = DeleteFileIfPresent(SavePath);
        deleted |= DeleteFileIfPresent(BackupPath);
        deleted |= DeleteFileIfPresent(TempPath);

        if (ContainerProgressManager.Instance != null)
            ContainerProgressManager.Instance.DeleteLegacySaveAfterMigration();
        else
            ContainerProgressManager.DeleteLegacySaveFile();

        saveDirty = false;
        ScheduleNextAutosave();

        Debug.Log(
            deleted
                ? "Save files deleted."
                : "No save files exist to delete.");
    }

    private bool TryWriteCurrentState()
    {
        if (isWritingSave)
            return false;

        if (InventoryManager.Instance == null)
        {
            Debug.LogError("Cannot save: no InventoryManager found.");
            return false;
        }

        isWritingSave = true;

        try
        {
            SaveData data = BuildSaveData();
            WriteSaveAtomically(JsonUtility.ToJson(data, true));

            saveDirty = false;
            ScheduleNextAutosave();

            if (ContainerProgressManager.Instance != null)
                ContainerProgressManager.Instance.DeleteLegacySaveAfterMigration();
            else
                ContainerProgressManager.DeleteLegacySaveFile();

            Debug.Log(
                $"Game saved as SaveData {SaveData.CurrentVersion}: {SavePath}");

            return true;
        }
        catch (Exception exception)
        {
            saveDirty = true;
            ScheduleNextAutosave();
            Debug.LogError($"SaveManager: Failed to save game. {exception}");
            return false;
        }
        finally
        {
            isWritingSave = false;
        }
    }

    private SaveData BuildSaveData()
    {
        EnsureRuntimeState();

        long now = DateTime.UtcNow.Ticks;

        if (string.IsNullOrWhiteSpace(metadata.saveId))
            metadata.saveId = Guid.NewGuid().ToString();

        if (metadata.createdUtcTicks <= 0)
            metadata.createdUtcTicks = now;

        metadata.lastSavedUtcTicks = now;

        SaveData data = new SaveData
        {
            saveVersion = SaveData.CurrentVersion,
            metadata = Clone(metadata),
            player = new PlayerProfileSaveData
            {
                gold = gold,
                diamonds = diamonds,
                xp = xp
            },
            inventories = BuildInventoryState(),
            upgrades = Clone(upgrades),
            museum = Clone(museum),
            tradeups = Clone(tradeups),
            containerProgress = ContainerProgressManager.Instance != null
                ? ContainerProgressManager.Instance.ExportSaveData()
                : Clone(containerProgressState)
        };

        EnsureSaveData(data);
        return data;
    }

    private InventoryStateSaveData BuildInventoryState()
    {
        InventoryStateSaveData state = new InventoryStateSaveData
        {
            unlockedStoragePages = InventoryManager.Instance.UnlockedStoragePages,
            itemsPerStoragePage = InventoryManager.Instance.ItemsPerStoragePage
        };

        foreach (InventoryItem item in InventoryManager.Instance.Items)
        {
            InventoryItemSaveData saved = CreateItemSave(item);

            if (saved != null)
                state.skinInventory.Add(saved);
        }

        if (CaseInventoryManager.Instance != null)
        {
            foreach (CaseInventoryEntry entry
                     in CaseInventoryManager.Instance.Cases)
            {
                if (entry == null ||
                    entry.caseData == null ||
                    entry.amount <= 0)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.caseData.apiId))
                {
                    Debug.LogWarning(
                        $"Skipping case with missing apiId: " +
                        $"{entry.caseData.caseName}");
                    continue;
                }

                state.caseInventory.Add(new CaseInventoryEntrySaveData
                {
                    caseApiId = entry.caseData.apiId,
                    amount = entry.amount
                });
            }
        }

        return state;
    }

    private InventoryItemSaveData CreateItemSave(InventoryItem item)
    {
        if (item == null || item.skin == null)
            return null;

        if (string.IsNullOrWhiteSpace(item.skin.apiId))
        {
            Debug.LogWarning(
                $"Skipping item with missing apiId: " +
                $"{SkinDisplayUtility.GetDisplayName(item.skin)}");
            return null;
        }

        return new InventoryItemSaveData
        {
            instanceId = item.instanceId,
            acquisitionSequence = item.acquisitionSequence,
            skinApiId = item.skin.apiId,
            floatValue = item.floatValue,
            patternId = item.patternId,
            patternTier = item.patternTier,
            statTrak = item.statTrak && !item.souvenir,
            souvenir = item.souvenir,
            isVanilla = item.isVanilla,
            marketValue = item.marketValue,
            favorite = item.favorite,
            storageIndex = item.storageIndex
        };
    }

    private bool TryLoadSaveData(
        string path,
        out SaveData data,
        out bool migrated)
    {
        data = null;
        migrated = false;

        if (!File.Exists(path))
            return false;

        try
        {
            string json = File.ReadAllText(path);

            if (string.IsNullOrWhiteSpace(json) ||
                !json.Contains("\"saveVersion\""))
            {
                return false;
            }

            SaveVersionHeader header =
                JsonUtility.FromJson<SaveVersionHeader>(json);

            int version = header != null ? header.saveVersion : 0;

            if (version <= 1)
            {
                SaveDataV1 legacy = JsonUtility.FromJson<SaveDataV1>(json);

                if (legacy == null)
                    return false;

                data = MigrateV1ToV2(legacy);
                migrated = true;

                Debug.Log(
                    "SaveManager: Migrated SaveData 1.0 to SaveData 2.0.");

                return true;
            }

            if (version > SaveData.CurrentVersion)
            {
                Debug.LogError(
                    $"Save version {version} is newer than supported version " +
                    $"{SaveData.CurrentVersion}; load cancelled to prevent data loss.");
                return false;
            }

            data = JsonUtility.FromJson<SaveData>(json);

            if (data == null)
                return false;

            EnsureSaveData(data);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"SaveManager: Failed to read {path}. {exception.Message}");
            return false;
        }
    }

    private SaveData MigrateV1ToV2(SaveDataV1 legacy)
    {
        long now = DateTime.UtcNow.Ticks;

        SaveData data = new SaveData
        {
            saveVersion = SaveData.CurrentVersion,
            metadata = new SaveMetadataSaveData
            {
                saveId = Guid.NewGuid().ToString(),
                createdUtcTicks = now,
                lastSavedUtcTicks = now
            },
            player = new PlayerProfileSaveData
            {
                gold = legacy.gold,
                diamonds = legacy.diamonds,
                xp = legacy.xp
            },
            inventories = new InventoryStateSaveData
            {
                unlockedStoragePages = Mathf.Max(
                    1,
                    legacy.unlockedStoragePages),
                itemsPerStoragePage = Mathf.Max(
                    1,
                    legacy.itemsPerStoragePage),
                skinInventory = legacy.inventory ??
                    new List<InventoryItemSaveData>(),
                caseInventory = legacy.caseInventory ??
                    new List<CaseInventoryEntrySaveData>()
            },
            upgrades = new UpgradeStateSaveData
            {
                openingSlotsOwned = 1
            },
            museum = new MuseumStateSaveData(),
            tradeups = new TradeupStateSaveData(),
            containerProgress = ContainerProgressManager.Instance != null
                ? ContainerProgressManager.Instance.ExportSaveData()
                : ContainerProgressManager.ReadLegacySaveForMigration()
        };

        EnsureSaveData(data);
        return data;
    }

    private void ApplySaveData(SaveData data)
    {
        EnsureSaveData(data);

        metadata = Clone(data.metadata);
        upgrades = Clone(data.upgrades);
        museum = Clone(data.museum);
        tradeups = Clone(data.tradeups);
        containerProgressState = Clone(data.containerProgress);

        gold = Mathf.Max(0f, data.player.gold);
        diamonds = Mathf.Max(0, data.player.diamonds);
        xp = Mathf.Max(0, data.player.xp);

        InventoryManager.Instance.SetStorageData(
            data.inventories.unlockedStoragePages,
            data.inventories.itemsPerStoragePage);

        InventoryManager.Instance.ReplaceInventory(
            BuildLoadedSkinInventory(data.inventories.skinInventory));

        if (CaseInventoryManager.Instance != null)
        {
            CaseInventoryManager.Instance.ReplaceCaseInventory(
                BuildLoadedCaseInventory(data.inventories.caseInventory));
        }
        else
        {
            Debug.LogWarning(
                "SaveManager: No CaseInventoryManager found while loading.");
        }

        if (ContainerProgressManager.Instance != null)
        {
            ContainerProgressManager.Instance.ReplaceProgress(
                containerProgressState);
        }
        else if (containerProgressState.progressEntries.Count > 0)
        {
            Debug.LogWarning(
                "SaveManager: Saved container progress could not be applied " +
                "because its manager is missing.");
        }

        OnCurrencyChanged?.Invoke();
        OnProgressChanged?.Invoke();
        OnTradeupStateChanged?.Invoke();
    }

    private List<InventoryItem> BuildLoadedSkinInventory(
        List<InventoryItemSaveData> savedItems)
    {
        List<InventoryItem> loaded = new List<InventoryItem>();

        if (savedItems == null)
            return loaded;

        foreach (InventoryItemSaveData saved in savedItems)
        {
            if (saved == null)
                continue;

            SkinData skin = database.GetSkinByApiId(saved.skinApiId);

            if (skin == null)
            {
                Debug.LogWarning(
                    $"Could not find skin with apiId: {saved.skinApiId}");
                continue;
            }

            bool souvenir = saved.souvenir && skin.canBeSouvenir;

            InventoryItem item = new InventoryItem
            {
                instanceId = string.IsNullOrWhiteSpace(saved.instanceId)
                    ? Guid.NewGuid().ToString()
                    : saved.instanceId,

                acquisitionSequence = saved.acquisitionSequence,

                skin = skin,
                floatValue = saved.floatValue,
                patternId = saved.patternId,
                patternTier = saved.patternTier,
                statTrak = !souvenir && saved.statTrak && skin.canBeStatTrak,
                souvenir = souvenir,
                isVanilla = saved.isVanilla || skin.isVanilla,
                favorite = saved.favorite,
                storageIndex = Mathf.Max(0, saved.storageIndex)
            };

            if (item.isVanilla)
            {
                item.floatValue = -1d;
                item.patternId = -1;
                item.patternTier = PatternTier.None;
            }

            item.marketValue = PriceCalculator.GetPrice(item);
            loaded.Add(item);
        }

        return loaded;
    }

    private List<CaseInventoryEntry> BuildLoadedCaseInventory(
        List<CaseInventoryEntrySaveData> savedCases)
    {
        List<CaseInventoryEntry> loaded =
            new List<CaseInventoryEntry>();

        if (savedCases == null)
            return loaded;

        foreach (CaseInventoryEntrySaveData saved in savedCases)
        {
            if (saved == null || saved.amount <= 0)
                continue;

            CaseData caseData = database.GetCaseByApiId(saved.caseApiId);

            if (caseData == null)
            {
                Debug.LogWarning(
                    $"Could not find case with apiId: {saved.caseApiId}");
                continue;
            }

            loaded.Add(new CaseInventoryEntry
            {
                caseData = caseData,
                amount = saved.amount
            });
        }

        return loaded;
    }

    private void EnsureRuntimeState()
    {
        if (upgrades == null)
            upgrades = new UpgradeStateSaveData();

        upgrades.openingSlotsOwned = Mathf.Clamp(
            upgrades.openingSlotsOwned,
            1,
            3);

        if (upgrades.upgradeLevels == null)
            upgrades.upgradeLevels = new List<UpgradeLevelSaveData>();

        if (museum == null)
            museum = new MuseumStateSaveData();

        EnsureMuseumState(museum);

        if (tradeups == null)
            tradeups = new TradeupStateSaveData();

        EnsureTradeupState(tradeups);

        if (metadata == null)
            metadata = new SaveMetadataSaveData();

        if (containerProgressState == null)
            containerProgressState = new ContainerProgressSaveData();

        if (containerProgressState.progressEntries == null)
        {
            containerProgressState.progressEntries =
                new List<ContainerProgressData>();
        }
    }

    private static void EnsureSaveData(SaveData data)
    {
        if (data == null)
            return;

        data.saveVersion = SaveData.CurrentVersion;

        if (data.metadata == null)
            data.metadata = new SaveMetadataSaveData();

        if (string.IsNullOrWhiteSpace(data.metadata.saveId))
            data.metadata.saveId = Guid.NewGuid().ToString();

        long now = DateTime.UtcNow.Ticks;

        if (data.metadata.createdUtcTicks <= 0)
            data.metadata.createdUtcTicks = now;

        if (data.metadata.lastSavedUtcTicks <= 0)
            data.metadata.lastSavedUtcTicks = now;

        if (data.player == null)
            data.player = new PlayerProfileSaveData();

        if (data.inventories == null)
            data.inventories = new InventoryStateSaveData();

        data.inventories.unlockedStoragePages = Mathf.Max(
            1,
            data.inventories.unlockedStoragePages);

        data.inventories.itemsPerStoragePage = Mathf.Max(
            1,
            data.inventories.itemsPerStoragePage);

        if (data.inventories.skinInventory == null)
            data.inventories.skinInventory = new List<InventoryItemSaveData>();

        if (data.inventories.caseInventory == null)
            data.inventories.caseInventory = new List<CaseInventoryEntrySaveData>();

        if (data.upgrades == null)
            data.upgrades = new UpgradeStateSaveData();

        data.upgrades.openingSlotsOwned = Mathf.Clamp(
            data.upgrades.openingSlotsOwned,
            1,
            3);

        if (data.upgrades.upgradeLevels == null)
            data.upgrades.upgradeLevels = new List<UpgradeLevelSaveData>();

        if (data.museum == null)
            data.museum = new MuseumStateSaveData();

        EnsureMuseumState(data.museum);

        if (data.tradeups == null)
            data.tradeups = new TradeupStateSaveData();

        EnsureTradeupState(data.tradeups);

        if (data.containerProgress == null)
            data.containerProgress = new ContainerProgressSaveData();

        if (data.containerProgress.progressEntries == null)
        {
            data.containerProgress.progressEntries =
                new List<ContainerProgressData>();
        }
    }

    private static void EnsureMuseumState(MuseumStateSaveData state)
    {
        if (state.donations == null)
            state.donations = new List<MuseumDonationRecordSaveData>();

        if (state.claimedMilestoneIds == null)
            state.claimedMilestoneIds = new List<string>();

        if (state.unlockedPlaqueIds == null)
            state.unlockedPlaqueIds = new List<string>();

        if (state.giftDesk == null)
            state.giftDesk = new GiftDeskSaveData();

        if (state.giftDesk.shardBalances == null)
        {
            state.giftDesk.shardBalances =
                new List<GiftShardBalanceSaveData>();
        }

        if (state.giftDesk.claimedGiftIds == null)
            state.giftDesk.claimedGiftIds = new List<string>();

        if (state.trophyRoom == null)
            state.trophyRoom = new TrophyRoomSaveData();

        if (state.trophyRoom.displayedItems == null)
        {
            state.trophyRoom.displayedItems =
                new List<TrophyDisplaySlotSaveData>();
        }
    }

    private static void EnsureTradeupState(TradeupStateSaveData state)
    {
        state.completedTradeups = Mathf.Max(0, state.completedTradeups);
        state.completedStandardTradeups =
            Mathf.Max(0, state.completedStandardTradeups);
        state.completedCovertTradeups =
            Mathf.Max(0, state.completedCovertTradeups);
        state.totalInputsConsumed = Mathf.Max(0, state.totalInputsConsumed);

        state.totalInputMarketValue =
            Mathf.Max(0f, state.totalInputMarketValue);
        state.totalOutputMarketValue =
            Mathf.Max(0f, state.totalOutputMarketValue);
        state.bestOutputMarketValue =
            Mathf.Max(0f, state.bestOutputMarketValue);
        state.floatTuningLevel = Mathf.Max(0, state.floatTuningLevel);

        if (state.lowestOutputFloat < 0d)
            state.lowestOutputFloat = -1d;

        if (state.recentHistory == null)
            state.recentHistory = new List<TradeupHistorySaveData>();

        for (int i = state.recentHistory.Count - 1; i >= 0; i--)
        {
            TradeupHistorySaveData record = state.recentHistory[i];

            if (record == null)
            {
                state.recentHistory.RemoveAt(i);
                continue;
            }

            NormalizeTradeupHistoryRecord(record);
        }
    }

    private static void NormalizeTradeupHistoryRecord(
        TradeupHistorySaveData record)
    {
        if (string.IsNullOrWhiteSpace(record.tradeupId))
            record.tradeupId = Guid.NewGuid().ToString();

        if (record.completedUtcTicks <= 0)
            record.completedUtcTicks = DateTime.UtcNow.Ticks;

        record.inputCount = Mathf.Max(0, record.inputCount);
        record.totalInputMarketValue =
            Mathf.Max(0f, record.totalInputMarketValue);
        record.outputMarketValue = Mathf.Max(0f, record.outputMarketValue);

        if (record.inputInstanceIds == null)
            record.inputInstanceIds = new List<string>();

        if (record.inputSkinApiIds == null)
            record.inputSkinApiIds = new List<string>();

        if (record.inputSourceApiIds == null)
            record.inputSourceApiIds = new List<string>();
    }

    private static void TrimTradeupHistory(
        TradeupStateSaveData state,
        int maximumEntries)
    {
        int limit = Mathf.Max(0, maximumEntries);

        while (state.recentHistory.Count > limit)
            state.recentHistory.RemoveAt(0);
    }

    private static T Clone<T>(T source) where T : class, new()
    {
        if (source == null)
            return new T();

        T copy = JsonUtility.FromJson<T>(JsonUtility.ToJson(source));
        return copy ?? new T();
    }

    private void WriteSaveAtomically(string json)
    {
        string directory = Path.GetDirectoryName(SavePath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(TempPath, json);

        if (File.Exists(SavePath))
            File.Copy(SavePath, BackupPath, true);

        if (File.Exists(SavePath))
            File.Delete(SavePath);

        File.Move(TempPath, SavePath);
    }

    private void ScheduleNextAutosave()
    {
        nextAutosaveTime =
            Time.unscaledTime + Mathf.Max(5f, autosaveIntervalSeconds);
    }

    private static bool DeleteFileIfPresent(string path)
    {
        if (!File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }
}
