using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("Database")]
    public GameDatabase database;

    [Header("Temporary Player Values")]
    public float gold;
    public int diamonds;
    public int xp;

    [Header("SaveData v2 Runtime Sections")]
    [SerializeField] private ProgressionSaveData progressionData =
        new ProgressionSaveData();

    [SerializeField] private MuseumSaveData museumData =
        new MuseumSaveData();

    [SerializeField] private TradeupSaveData tradeupData =
        new TradeupSaveData();

    public event Action OnCurrencyChanged;
    public event Action OnProgressChanged;
    public event Action OnOpeningSlotsChanged;

    public float Gold => gold;
    public int Diamonds => diamonds;
    public int XP => xp;
    public int UnlockedOpeningSlots => progressionData.unlockedOpeningSlots;

    public ProgressionSaveData ProgressionData => progressionData;
    public MuseumSaveData MuseumData => museumData;
    public TradeupSaveData TradeupData => tradeupData;

    public PlayerRank CurrentRank => PlayerProgressUtility.GetRankFromXP(xp);

    private string SavePath =>
        Path.Combine(Application.persistentDataPath, "casecatcher_save.json");

    private string BackupSavePath => SavePath + ".bak";
    private string TemporarySavePath => SavePath + ".tmp";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Duplicate SaveManager found, destroying: " + gameObject.name);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        NormalizeRuntimeSections();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void AddGold(float amount)
    {
        if (amount <= 0f)
            return;

        gold += amount;
        OnCurrencyChanged?.Invoke();
    }

    public bool SpendGold(float amount)
    {
        if (amount <= 0f)
            return true;

        if (gold < amount)
            return false;

        gold -= amount;
        OnCurrencyChanged?.Invoke();
        return true;
    }

    public void AddDiamonds(int amount)
    {
        if (amount <= 0)
            return;

        diamonds += amount;
        OnCurrencyChanged?.Invoke();
    }

    public bool SpendDiamonds(int amount)
    {
        if (amount <= 0)
            return true;

        if (diamonds < amount)
            return false;

        diamonds -= amount;
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
        xp = Mathf.Max(0, amount);
        OnProgressChanged?.Invoke();
    }

    public bool SetUnlockedOpeningSlots(int amount)
    {
        NormalizeRuntimeSections();

        int clampedAmount = Mathf.Clamp(amount, 1, 3);

        if (progressionData.unlockedOpeningSlots == clampedAmount)
            return false;

        progressionData.unlockedOpeningSlots = clampedAmount;
        OnOpeningSlotsChanged?.Invoke();
        return true;
    }

    public bool UnlockNextOpeningSlot()
    {
        NormalizeRuntimeSections();

        int maximumAllowed = PlayerProgressUtility.GetMaxOpeningSlotsAllowedByRank(
            CurrentRank);

        if (progressionData.unlockedOpeningSlots >= maximumAllowed)
            return false;

        return SetUnlockedOpeningSlots(
            progressionData.unlockedOpeningSlots + 1);
    }

    public void SaveGame()
    {
        TrySaveCurrentState(false);
    }

    private bool TrySaveCurrentState(bool preserveExistingBackup)
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogError("Cannot save: no InventoryManager found.");
            return false;
        }

        NormalizeRuntimeSections();

        SaveData saveData = BuildSaveData();
        bool saved = WriteSaveData(saveData, preserveExistingBackup);

        if (saved && ContainerProgressManager.Instance != null)
            ContainerProgressManager.Instance.DeleteLegacyProgressAfterMigration();

        return saved;
    }

    private SaveData BuildSaveData()
    {
        SaveData saveData = new SaveData
        {
            saveVersion = SaveData.CurrentVersion,
            savedAtUtcTicks = DateTime.UtcNow.Ticks,
            player = new PlayerSaveData
            {
                gold = gold,
                diamonds = diamonds,
                xp = xp
            },
            progression = progressionData,
            museum = museumData,
            tradeups = tradeupData
        };

        saveData.inventory.unlockedStoragePages =
            InventoryManager.Instance.UnlockedStoragePages;

        saveData.inventory.itemsPerStoragePage =
            InventoryManager.Instance.ItemsPerStoragePage;

        foreach (InventoryItem item in InventoryManager.Instance.Items)
        {
            if (item == null || item.skin == null)
                continue;

            if (string.IsNullOrWhiteSpace(item.skin.apiId))
            {
                Debug.LogWarning(
                    $"Skipping item with missing skin apiId: " +
                    $"{SkinDisplayUtility.GetDisplayName(item.skin)}");
                continue;
            }

            saveData.inventory.items.Add(new InventoryItemSaveData
            {
                instanceId = item.instanceId,
                skinApiId = item.skin.apiId,
                floatValue = item.floatValue,
                patternId = item.patternId,
                patternTier = item.patternTier,
                statTrak = item.statTrak,
                souvenir = item.souvenir,
                isVanilla = item.isVanilla,
                marketValue = item.marketValue,
                favorite = item.favorite
            });
        }

        if (CaseInventoryManager.Instance != null)
        {
            foreach (CaseInventoryEntry entry in CaseInventoryManager.Instance.Cases)
            {
                if (entry == null || entry.caseData == null || entry.amount <= 0)
                    continue;

                if (string.IsNullOrWhiteSpace(entry.caseData.apiId))
                {
                    Debug.LogWarning(
                        $"Skipping case inventory entry with missing apiId: " +
                        $"{entry.caseData.caseName}");
                    continue;
                }

                saveData.inventory.cases.Add(new CaseInventoryEntrySaveData
                {
                    caseApiId = entry.caseData.apiId,
                    amount = entry.amount
                });
            }
        }

        if (ContainerProgressManager.Instance != null)
        {
            saveData.containerProgress =
                ContainerProgressManager.Instance.CreateSaveDataSnapshot();
        }

        NormalizeSaveData(saveData);
        return saveData;
    }

    private bool WriteSaveData(
        SaveData saveData,
        bool preserveExistingBackup)
    {
        try
        {
            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(TemporarySavePath, json);

            if (File.Exists(SavePath) && !preserveExistingBackup)
                File.Copy(SavePath, BackupSavePath, true);

            File.Copy(TemporarySavePath, SavePath, true);
            File.Delete(TemporarySavePath);

            Debug.Log(
                $"Game saved as SaveData v{saveData.saveVersion} to: {SavePath}");

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to save game. {exception.Message}");
            return false;
        }
    }

    public void LoadGame()
    {
        if (database == null)
        {
            Debug.LogError("Cannot load: GameDatabase is not assigned on SaveManager.");
            return;
        }

        if (InventoryManager.Instance == null)
        {
            Debug.LogError("Cannot load: no InventoryManager found.");
            return;
        }

        bool loadedFromBackup = false;
        bool migratedFromV1 = false;
        SaveData saveData;

        if (!TryLoadSaveFile(SavePath, out saveData, out migratedFromV1))
        {
            if (!TryLoadSaveFile(
                    BackupSavePath,
                    out saveData,
                    out migratedFromV1))
            {
                Debug.LogWarning(
                    $"No valid save file found at {SavePath} or {BackupSavePath}.");

                TryImportLegacyContainerProgressWithoutMainSave();
                return;
            }

            loadedFromBackup = true;
            Debug.LogWarning("Loaded the backup save because the main save was unavailable or invalid.");
        }

        ApplySaveData(saveData);

        bool importedLegacyContainerProgress =
            ContainerProgressManager.Instance != null &&
            ContainerProgressManager.Instance.TryImportLegacyProgress(true);

        if (migratedFromV1 ||
            loadedFromBackup ||
            importedLegacyContainerProgress)
        {
            TrySaveCurrentState(loadedFromBackup);
        }

        Debug.Log(
            $"Game loaded from SaveData v{saveData.saveVersion}. " +
            $"Inventory items loaded: {InventoryManager.Instance.Count}");
    }

    private bool TryLoadSaveFile(
        string path,
        out SaveData saveData,
        out bool migratedFromV1)
    {
        saveData = null;
        migratedFromV1 = false;

        if (!File.Exists(path))
            return false;

        try
        {
            string json = File.ReadAllText(path);
            return TryParseSaveJson(json, out saveData, out migratedFromV1);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to read save file at {path}. {exception.Message}");
            return false;
        }
    }

    private bool TryParseSaveJson(
        string json,
        out SaveData saveData,
        out bool migratedFromV1)
    {
        saveData = null;
        migratedFromV1 = false;

        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            SaveVersionHeader header =
                JsonUtility.FromJson<SaveVersionHeader>(json);

            int version = header != null ? header.saveVersion : 0;

            if (version <= 1)
            {
                LegacySaveDataV1 legacySave =
                    JsonUtility.FromJson<LegacySaveDataV1>(json);

                if (legacySave == null)
                    return false;

                saveData = MigrateV1ToV2(legacySave);
                migratedFromV1 = true;
                return true;
            }

            if (version > SaveData.CurrentVersion)
            {
                Debug.LogError(
                    $"SaveData version {version} is newer than supported version " +
                    $"{SaveData.CurrentVersion}.");
                return false;
            }

            saveData = JsonUtility.FromJson<SaveData>(json);

            if (saveData == null)
                return false;

            NormalizeSaveData(saveData);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to parse save JSON. {exception.Message}");
            return false;
        }
    }

    private SaveData MigrateV1ToV2(LegacySaveDataV1 legacySave)
    {
        SaveData migrated = new SaveData
        {
            saveVersion = SaveData.CurrentVersion,
            savedAtUtcTicks = DateTime.UtcNow.Ticks,
            player = new PlayerSaveData
            {
                gold = legacySave.gold,
                diamonds = legacySave.diamonds,
                xp = legacySave.xp
            },
            inventory = new InventorySaveData
            {
                unlockedStoragePages = Mathf.Max(
                    1,
                    legacySave.unlockedStoragePages),
                itemsPerStoragePage = Mathf.Max(
                    1,
                    legacySave.itemsPerStoragePage),
                items = legacySave.inventory != null
                    ? new List<InventoryItemSaveData>(legacySave.inventory)
                    : new List<InventoryItemSaveData>(),
                cases = legacySave.caseInventory != null
                    ? new List<CaseInventoryEntrySaveData>(legacySave.caseInventory)
                    : new List<CaseInventoryEntrySaveData>()
            },
            progression = new ProgressionSaveData
            {
                unlockedOpeningSlots = 1
            },
            containerProgress = new ContainerProgressSaveData(),
            museum = new MuseumSaveData(),
            tradeups = new TradeupSaveData()
        };

        NormalizeSaveData(migrated);
        Debug.Log("Migrated SaveData v1 to SaveData v2 in memory.");
        return migrated;
    }

    private void ApplySaveData(SaveData saveData)
    {
        NormalizeSaveData(saveData);

        gold = Mathf.Max(0f, saveData.player.gold);
        diamonds = Mathf.Max(0, saveData.player.diamonds);
        xp = Mathf.Max(0, saveData.player.xp);

        progressionData = saveData.progression;
        museumData = saveData.museum;
        tradeupData = saveData.tradeups;
        NormalizeRuntimeSections();

        InventoryManager.Instance.SetStorageData(
            saveData.inventory.unlockedStoragePages,
            saveData.inventory.itemsPerStoragePage);

        List<InventoryItem> loadedItems = BuildLoadedInventory(
            saveData.inventory.items);

        InventoryManager.Instance.ReplaceInventory(loadedItems);

        if (CaseInventoryManager.Instance != null)
        {
            List<CaseInventoryEntry> loadedCases = BuildLoadedCaseInventory(
                saveData.inventory.cases);

            CaseInventoryManager.Instance.ReplaceCaseInventory(loadedCases);
        }
        else
        {
            Debug.LogWarning(
                "SaveManager: No CaseInventoryManager found while loading case inventory.");
        }

        if (ContainerProgressManager.Instance != null)
        {
            ContainerProgressManager.Instance.ReplaceProgress(
                saveData.containerProgress,
                false);
        }

        OnCurrencyChanged?.Invoke();
        OnProgressChanged?.Invoke();
        OnOpeningSlotsChanged?.Invoke();

        if (ContainerProgressManager.Instance != null)
            ContainerProgressManager.Instance.NotifyProgressChanged();
    }

    private List<InventoryItem> BuildLoadedInventory(
        List<InventoryItemSaveData> savedItems)
    {
        List<InventoryItem> loadedItems = new List<InventoryItem>();

        if (savedItems == null)
            return loadedItems;

        foreach (InventoryItemSaveData itemSave in savedItems)
        {
            if (itemSave == null || string.IsNullOrWhiteSpace(itemSave.skinApiId))
                continue;

            SkinData skin = database.GetSkinByApiId(itemSave.skinApiId);

            if (skin == null)
            {
                Debug.LogWarning(
                    $"Could not find skin with apiId: {itemSave.skinApiId}");
                continue;
            }

            InventoryItem item = new InventoryItem
            {
                instanceId = string.IsNullOrWhiteSpace(itemSave.instanceId)
                    ? Guid.NewGuid().ToString()
                    : itemSave.instanceId,
                skin = skin,
                floatValue = itemSave.floatValue,
                patternId = itemSave.patternId,
                patternTier = itemSave.patternTier,
                statTrak = itemSave.souvenir ? false : itemSave.statTrak,
                souvenir = itemSave.souvenir,
                isVanilla = itemSave.isVanilla,
                marketValue = itemSave.marketValue,
                favorite = itemSave.favorite
            };

            loadedItems.Add(item);
        }

        return loadedItems;
    }

    private List<CaseInventoryEntry> BuildLoadedCaseInventory(
        List<CaseInventoryEntrySaveData> savedCases)
    {
        List<CaseInventoryEntry> loadedCases =
            new List<CaseInventoryEntry>();

        if (savedCases == null)
            return loadedCases;

        foreach (CaseInventoryEntrySaveData caseSave in savedCases)
        {
            if (caseSave == null ||
                string.IsNullOrWhiteSpace(caseSave.caseApiId) ||
                caseSave.amount <= 0)
            {
                continue;
            }

            CaseData caseData = database.GetCaseByApiId(caseSave.caseApiId);

            if (caseData == null)
            {
                Debug.LogWarning(
                    $"Could not find case with apiId: {caseSave.caseApiId}");
                continue;
            }

            loadedCases.Add(new CaseInventoryEntry
            {
                caseData = caseData,
                amount = caseSave.amount
            });
        }

        return loadedCases;
    }

    private void TryImportLegacyContainerProgressWithoutMainSave()
    {
        if (ContainerProgressManager.Instance == null)
            return;

        if (!ContainerProgressManager.Instance.TryImportLegacyProgress(true))
            return;

        TrySaveCurrentState(false);
    }

    private void NormalizeRuntimeSections()
    {
        if (progressionData == null)
            progressionData = new ProgressionSaveData();

        if (progressionData.upgradeLevels == null)
            progressionData.upgradeLevels = new List<UpgradeLevelSaveData>();

        progressionData.unlockedOpeningSlots = Mathf.Clamp(
            progressionData.unlockedOpeningSlots,
            1,
            3);

        if (museumData == null)
            museumData = new MuseumSaveData();

        NormalizeMuseumData(museumData);

        if (tradeupData == null)
            tradeupData = new TradeupSaveData();

        if (tradeupData.history == null)
            tradeupData.history = new List<TradeupHistorySaveData>();
    }

    private static void NormalizeSaveData(SaveData saveData)
    {
        if (saveData == null)
            return;

        saveData.saveVersion = SaveData.CurrentVersion;

        if (saveData.player == null)
            saveData.player = new PlayerSaveData();

        if (saveData.inventory == null)
            saveData.inventory = new InventorySaveData();

        saveData.inventory.unlockedStoragePages = Mathf.Max(
            1,
            saveData.inventory.unlockedStoragePages);

        saveData.inventory.itemsPerStoragePage = Mathf.Max(
            1,
            saveData.inventory.itemsPerStoragePage);

        if (saveData.inventory.items == null)
        {
            saveData.inventory.items =
                new List<InventoryItemSaveData>();
        }

        if (saveData.inventory.cases == null)
        {
            saveData.inventory.cases =
                new List<CaseInventoryEntrySaveData>();
        }

        if (saveData.progression == null)
            saveData.progression = new ProgressionSaveData();

        if (saveData.progression.upgradeLevels == null)
        {
            saveData.progression.upgradeLevels =
                new List<UpgradeLevelSaveData>();
        }

        saveData.progression.unlockedOpeningSlots = Mathf.Clamp(
            saveData.progression.unlockedOpeningSlots,
            1,
            3);

        if (saveData.containerProgress == null)
            saveData.containerProgress = new ContainerProgressSaveData();

        if (saveData.containerProgress.progressEntries == null)
        {
            saveData.containerProgress.progressEntries =
                new List<ContainerProgressData>();
        }

        if (saveData.museum == null)
            saveData.museum = new MuseumSaveData();

        NormalizeMuseumData(saveData.museum);

        if (saveData.tradeups == null)
            saveData.tradeups = new TradeupSaveData();

        if (saveData.tradeups.history == null)
        {
            saveData.tradeups.history =
                new List<TradeupHistorySaveData>();
        }
    }

    private static void NormalizeMuseumData(MuseumSaveData museum)
    {
        if (museum == null)
            return;

        if (museum.donations == null)
            museum.donations = new List<MuseumDonationSaveData>();

        if (museum.claimedMilestoneIds == null)
            museum.claimedMilestoneIds = new List<string>();

        if (museum.unlockedWeaponWingIds == null)
            museum.unlockedWeaponWingIds = new List<string>();

        if (museum.giftDesk == null)
            museum.giftDesk = new GiftDeskSaveData();

        if (museum.giftDesk.claimedGiftIds == null)
            museum.giftDesk.claimedGiftIds = new List<string>();

        if (museum.trophyRoom == null)
            museum.trophyRoom = new TrophyRoomSaveData();

        if (museum.trophyRoom.displays == null)
        {
            museum.trophyRoom.displays =
                new List<TrophyDisplaySaveData>();
        }
    }

    public void DeleteSave()
    {
        bool deletedAnyFile = false;

        deletedAnyFile |= DeleteFileIfPresent(SavePath);
        deletedAnyFile |= DeleteFileIfPresent(BackupSavePath);
        deletedAnyFile |= DeleteFileIfPresent(TemporarySavePath);

        if (ContainerProgressManager.Instance != null)
            ContainerProgressManager.Instance.DeleteLegacyProgressAfterMigration(true);

        if (deletedAnyFile)
            Debug.Log("SaveData v2 files deleted.");
        else
            Debug.LogWarning("No save files exist to delete.");
    }

    private bool DeleteFileIfPresent(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not delete save file at {path}. {exception.Message}");
            return false;
        }
    }
}
