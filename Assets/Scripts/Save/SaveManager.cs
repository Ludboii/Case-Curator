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
    public event Action OnCurrencyChanged;
public event Action OnProgressChanged;

public float Gold => gold;
public int Diamonds => diamonds;
public int XP => xp;

public PlayerRank CurrentRank => PlayerProgressUtility.GetRankFromXP(xp);

    string SavePath =>
        Path.Combine(Application.persistentDataPath, "casecatcher_save.json");

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

    public void SaveGame()
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogError("Cannot save: no InventoryManager found.");
            return;
        }

        SaveData saveData = new SaveData();

        saveData.gold = gold;
        saveData.diamonds = diamonds;
        saveData.xp = xp;
        saveData.unlockedStoragePages = InventoryManager.Instance.UnlockedStoragePages;
        saveData.itemsPerStoragePage = InventoryManager.Instance.ItemsPerStoragePage;

     foreach (InventoryItem item in InventoryManager.Instance.Items)
        {
            if (item == null || item.skin == null)
                continue;

            if (string.IsNullOrWhiteSpace(item.skin.apiId))
            {
                Debug.LogWarning(
                    $"Skipping item with missing skin apiId: {SkinDisplayUtility.GetDisplayName(item.skin)}");
                continue;
            }

            InventoryItemSaveData itemSave = new InventoryItemSaveData();

            itemSave.instanceId = item.instanceId;
            itemSave.skinApiId = item.skin.apiId;
            itemSave.floatValue = item.floatValue;
            itemSave.patternId = item.patternId;
            itemSave.patternTier = item.patternTier;
            itemSave.statTrak = item.statTrak;
            itemSave.souvenir = item.souvenir;
            itemSave.isVanilla = item.isVanilla;
            itemSave.marketValue = item.marketValue;
            itemSave.favorite = item.favorite;

            saveData.inventory.Add(itemSave);
        }
        saveData.caseInventory.Clear();

if (CaseInventoryManager.Instance != null)
{
    foreach (CaseInventoryEntry entry in CaseInventoryManager.Instance.Cases)
    {
        if (entry == null || entry.caseData == null)
            continue;

        if (entry.amount <= 0)
            continue;

        CaseInventoryEntrySaveData caseSave =
            new CaseInventoryEntrySaveData();

        caseSave.caseApiId = entry.caseData.apiId;
        caseSave.amount = entry.amount;

        saveData.caseInventory.Add(caseSave);
    }
}

        string json = JsonUtility.ToJson(saveData, true);

        File.WriteAllText(SavePath, json);

        Debug.Log($"Game saved to: {SavePath}");
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

        if (!File.Exists(SavePath))
        {
            Debug.LogWarning($"No save file found at: {SavePath}");
            return;
        }

        string json = File.ReadAllText(SavePath);

        SaveData saveData = JsonUtility.FromJson<SaveData>(json);

        if (saveData == null)
        {
            Debug.LogError("Failed to parse save file.");
            return;
        }

       gold = saveData.gold;
       diamonds = saveData.diamonds;
       xp = saveData.xp;

            OnCurrencyChanged?.Invoke();
            OnProgressChanged?.Invoke();
            
        InventoryManager.Instance.SetStorageData(
            saveData.unlockedStoragePages,
            saveData.itemsPerStoragePage);

        List<InventoryItem> loadedItems = new List<InventoryItem>();

        foreach (InventoryItemSaveData itemSave in saveData.inventory)
        {
            SkinData skin = database.GetSkinByApiId(itemSave.skinApiId);

            if (skin == null)
            {
                Debug.LogWarning(
                    $"Could not find skin with apiId: {itemSave.skinApiId}");
                continue;
            }

            InventoryItem item = new InventoryItem();

            item.instanceId = itemSave.instanceId;
            item.skin = skin;
            item.floatValue = itemSave.floatValue;
            item.patternId = itemSave.patternId;
            item.patternTier = itemSave.patternTier;
            item.statTrak = itemSave.statTrak;
            item.souvenir = itemSave.souvenir;
            item.isVanilla = itemSave.isVanilla;
            item.marketValue = itemSave.marketValue;
            item.favorite = itemSave.favorite;

            loadedItems.Add(item);
        }
        if (CaseInventoryManager.Instance != null)
{
    List<CaseInventoryEntry> loadedCases =
        new List<CaseInventoryEntry>();

    if (saveData.caseInventory != null)
    {
        foreach (CaseInventoryEntrySaveData caseSave in saveData.caseInventory)
        {
            if (caseSave == null)
                continue;

            CaseData caseData =
                database.GetCaseByApiId(caseSave.caseApiId);

            if (caseData == null)
            {
                Debug.LogWarning(
                    $"Could not find case with apiId: {caseSave.caseApiId}");

                continue;
            }

            if (caseSave.amount <= 0)
                continue;

            CaseInventoryEntry entry = new CaseInventoryEntry();
            entry.caseData = caseData;
            entry.amount = caseSave.amount;

            loadedCases.Add(entry);
        }
    }

    CaseInventoryManager.Instance.ReplaceCaseInventory(loadedCases);
}
else
{
    Debug.LogWarning("SaveManager: No CaseInventoryManager found while loading case inventory.");
}

        InventoryManager.Instance.ReplaceInventory(loadedItems);

        Debug.Log(
            $"Game loaded. Inventory items loaded: {loadedItems.Count}");
    }

    public void DeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("Save file deleted.");
        }
        else
        {
            Debug.LogWarning("No save file exists to delete.");
        }
    }
}