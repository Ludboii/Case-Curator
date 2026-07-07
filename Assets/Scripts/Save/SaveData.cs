using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
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