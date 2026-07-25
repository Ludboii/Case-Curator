using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// M7 authority for pedestal availability, Trophy safe storage, weighted power
/// and the global Trophy focus. Displayed items are removed from InventoryManager
/// and persisted as complete item snapshots inside MuseumStateSaveData.
/// </summary>
[DisallowMultipleComponent]
public sealed class TrophyRoomService : MonoBehaviour
{
    public static TrophyRoomService Instance { get; private set; }

    [SerializeField] private GameDatabase database;
    [SerializeField] private bool persistBetweenScenes = true;
    [SerializeField] private bool verboseLogging;

    public event Action OnTrophyRoomChanged;

    private readonly Dictionary<int, InventoryItem> displayedBySlot =
        new Dictionary<int, InventoryItem>();

    private TrophyRoomSaveData observedState;
    private UpgradeService upgradeService;
    private bool subscribedToUpgrades;
    private bool initialized;
    private bool rebuilding;

    public TrophyRoomBalanceData Balance
    {
        get
        {
            GameDatabase active = ResolveDatabase();
            return active != null ? active.trophyRoomBalance : null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GetOrCreate();
    }

    public static TrophyRoomService GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        TrophyRoomService existing =
            FindFirstObjectByType<TrophyRoomService>();

        if (existing != null)
            return existing;

        GameObject go = new GameObject("TrophyRoomService");
        return go.AddComponent<TrophyRoomService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistBetweenScenes)
            DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        TryInitialize();
    }

    private void Update()
    {
        if (!initialized)
        {
            TryInitialize();
            return;
        }

        TrophyRoomSaveData current = GetState();

        if (!ReferenceEquals(observedState, current))
        {
            SettleTimeSensitiveSystems();
            BindState(current, true);
            RefreshTimeSensitiveSystems();
            OnTrophyRoomChanged?.Invoke();
        }

        ResolveUpgradeSubscription();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (Instance == this)
            Instance = null;
    }

    public TrophyRoomSnapshot GetSnapshot()
    {
        EnsureCurrentState();

        TrophyRoomSnapshot snapshot = new TrophyRoomSnapshot
        {
            focus = GetFocus(),
            unlockedSlotCount = GetUnlockedSlotCount(),
            totalWeightedPower = GetTotalWeightedPower()
        };

        snapshot.activeBonusFraction = EvaluateFocusBonusFraction(
            snapshot.focus,
            snapshot.totalWeightedPower);

        for (int slot = 0;
             slot < TrophyRoomUpgradeUtility.MaximumPedestalCount;
             slot++)
        {
            InventoryItem item = GetDisplayedItem(slot);
            TrophyPowerBreakdown power = item != null
                ? EvaluateItem(item, slot)
                : null;

            double multiplier = Balance != null
                ? Balance.GetPedestalMultiplier(slot)
                : slot < 5 ? 1d : slot < 10 ? 1.2d : 1.5d;

            snapshot.slots.Add(new TrophyRoomSlotSnapshot
            {
                slotIndex = slot,
                unlocked = slot < snapshot.unlockedSlotCount,
                occupied = item != null,
                pedestalMultiplier = multiplier,
                item = item,
                power = power
            });

            if (item != null)
                snapshot.occupiedSlotCount++;
        }

        return snapshot;
    }

    public int GetUnlockedSlotCount()
    {
        EnsureCurrentState();

        int unlocked = TrophyRoomUpgradeUtility.GetUnlockedSlotCount(
            ResolveDatabase());

        if (observedState != null && observedState.unlockedSlots != unlocked)
        {
            observedState.unlockedSlots = unlocked;

            if (SaveManager.Instance != null)
                SaveManager.Instance.MarkDirty();
        }

        return unlocked;
    }

    public TrophyRoomFocus GetFocus()
    {
        TrophyRoomSaveData state = GetState();
        return state != null
            ? state.focus
            : TrophyRoomFocus.MuseumGoldIncome;
    }

    public bool SetFocus(TrophyRoomFocus focus)
    {
        EnsureCurrentState();

        if (observedState == null || observedState.focus == focus)
            return false;

        SettleTimeSensitiveSystems();
        observedState.focus = focus;
        MarkChanged();
        RefreshTimeSensitiveSystems();
        return true;
    }

    public InventoryItem GetDisplayedItem(int zeroBasedSlotIndex)
    {
        EnsureCurrentState();
        displayedBySlot.TryGetValue(
            zeroBasedSlotIndex,
            out InventoryItem item);
        return item;
    }

    public bool IsDisplayedInstanceId(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        EnsureCurrentState();

        foreach (InventoryItem item in displayedBySlot.Values)
        {
            if (item != null &&
                string.Equals(
                    item.instanceId,
                    instanceId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public TrophyPowerBreakdown EvaluateItem(
        InventoryItem item,
        int zeroBasedSlotIndex)
    {
        return TrophyPowerCalculator.Evaluate(
            item,
            Balance,
            zeroBasedSlotIndex);
    }

    public int GetTotalWeightedPower()
    {
        EnsureCurrentState();

        int total = 0;

        foreach (KeyValuePair<int, InventoryItem> pair in displayedBySlot)
        {
            if (pair.Value != null)
                total += EvaluateItem(pair.Value, pair.Key).finalContribution;
        }

        return Math.Max(0, total);
    }

    public double GetFocusBonusFraction(TrophyRoomFocus focus)
    {
        return EvaluateFocusBonusFraction(focus, GetTotalWeightedPower());
    }

    public double EvaluateFocusBonusFraction(
        TrophyRoomFocus focus,
        double totalPower)
    {
        return Balance != null
            ? Balance.EvaluateFocusBonus(focus, totalPower)
            : 0d;
    }

    public double GetMuseumGoldIncomeMultiplier()
    {
        return GetFocus() == TrophyRoomFocus.MuseumGoldIncome
            ? 1d + GetFocusBonusFraction(TrophyRoomFocus.MuseumGoldIncome)
            : 1d;
    }

    public double GetMuseumDiamondIncomeMultiplier()
    {
        return GetFocus() == TrophyRoomFocus.MuseumDiamondIncome
            ? 1d + GetFocusBonusFraction(TrophyRoomFocus.MuseumDiamondIncome)
            : 1d;
    }

    public double GetAutomatedAcquisitionDurationMultiplier()
    {
        double reduction = GetFocus() ==
                           TrophyRoomFocus.AutomatedAcquisitions
            ? GetFocusBonusFraction(TrophyRoomFocus.AutomatedAcquisitions)
            : 0d;

        return Math.Max(0.05d, 1d - reduction);
    }

    public double GetGiftRetrievalCooldownMultiplier()
    {
        double reduction = GetFocus() == TrophyRoomFocus.GiftRetrievals
            ? GetFocusBonusFraction(TrophyRoomFocus.GiftRetrievals)
            : 0d;

        return Math.Max(0.05d, 1d - reduction);
    }

    public TrophyRoomOperationResult PlaceOrReplace(
        int zeroBasedSlotIndex,
        InventoryItem selectedItem)
    {
        EnsureCurrentState();

        if (observedState == null || InventoryManager.Instance == null)
        {
            return TrophyRoomOperationResult.Failed(
                "Trophy Room or inventory state is unavailable.");
        }

        if (zeroBasedSlotIndex < 0 ||
            zeroBasedSlotIndex >= TrophyRoomUpgradeUtility.MaximumPedestalCount)
        {
            return TrophyRoomOperationResult.Failed(
                "The selected pedestal does not exist.");
        }

        if (zeroBasedSlotIndex >= GetUnlockedSlotCount())
        {
            return TrophyRoomOperationResult.Failed(
                "This pedestal has not been unlocked.");
        }

        if (selectedItem == null || selectedItem.skin == null ||
            InventoryManager.Instance.GetItemByInstanceId(
                selectedItem.instanceId) != selectedItem)
        {
            return TrophyRoomOperationResult.Failed(
                "The selected item is no longer in normal inventory.");
        }

        InventoryItemSaveData selectedSave =
            TrophyItemSerializationUtility.CreateSave(selectedItem);

        if (selectedSave == null)
        {
            return TrophyRoomOperationResult.Failed(
                "The selected item cannot be saved as a trophy.");
        }

        TrophyDisplaySlotSaveData existingRecord =
            FindSlotRecord(zeroBasedSlotIndex);
        InventoryItem existingItem = GetDisplayedItem(zeroBasedSlotIndex);

        SettleTimeSensitiveSystems();

        if (existingRecord != null && existingItem != null)
        {
            bool completed = InventoryManager.Instance.TryExecuteTransaction(
                new[] { selectedItem.instanceId },
                new List<InventoryItem> { existingItem },
                out InventoryTransactionResult transaction);

            if (!completed)
            {
                return TrophyRoomOperationResult.Failed(
                    transaction != null &&
                    !string.IsNullOrWhiteSpace(transaction.errorMessage)
                        ? transaction.errorMessage
                        : "The Trophy Room replacement transaction failed.");
            }
        }
        else if (!InventoryManager.Instance.RemoveItem(selectedItem))
        {
            return TrophyRoomOperationResult.Failed(
                "The selected item could not be moved into Trophy storage.");
        }

        if (existingRecord == null)
        {
            existingRecord = new TrophyDisplaySlotSaveData
            {
                slotIndex = zeroBasedSlotIndex
            };

            observedState.displayedItems.Add(existingRecord);
        }

        existingRecord.inventoryItemInstanceId = selectedSave.instanceId;
        existingRecord.storedItem = selectedSave;

        RebuildDisplayedCache(false);
        MarkChanged();
        RefreshTimeSensitiveSystems();

        InventoryItem placed = GetDisplayedItem(zeroBasedSlotIndex);
        TrophyPowerBreakdown power = EvaluateItem(
            placed,
            zeroBasedSlotIndex);

        return TrophyRoomOperationResult.Completed(
            $"Placed {SkinDisplayUtility.GetDisplayName(selectedItem.skin)} on " +
            $"Pedestal {zeroBasedSlotIndex + 1}. " +
            $"Contribution: {power.finalContribution:N0} Trophy Power.",
            zeroBasedSlotIndex,
            placed);
    }

    public TrophyRoomOperationResult RemoveFromPedestal(
        int zeroBasedSlotIndex)
    {
        EnsureCurrentState();

        TrophyDisplaySlotSaveData record =
            FindSlotRecord(zeroBasedSlotIndex);
        InventoryItem item = GetDisplayedItem(zeroBasedSlotIndex);

        if (record == null || item == null)
        {
            return TrophyRoomOperationResult.Failed(
                "This pedestal does not contain a trophy.");
        }

        if (InventoryManager.Instance == null ||
            !InventoryManager.Instance.HasSpace())
        {
            return TrophyRoomOperationResult.Failed(
                "Normal inventory is full. Free one inventory slot before " +
                "retrieving this trophy.");
        }

        SettleTimeSensitiveSystems();
        InventoryManager.Instance.AddItem(item);

        InventoryItem returned = InventoryManager.Instance.GetItemByInstanceId(
            item.instanceId);

        if (returned == null)
        {
            return TrophyRoomOperationResult.Failed(
                "The trophy could not be returned to normal inventory.");
        }

        observedState.displayedItems.Remove(record);
        RebuildDisplayedCache(false);
        MarkChanged();
        RefreshTimeSensitiveSystems();

        return TrophyRoomOperationResult.Completed(
            $"Returned {SkinDisplayUtility.GetDisplayName(item.skin)} to inventory.",
            zeroBasedSlotIndex,
            returned);
    }

    public List<InventoryItem> GetSelectionItems(
        TrophyInventorySortMode sortMode)
    {
        List<InventoryItem> result = InventoryManager.Instance != null
            ? InventoryManager.Instance.GetItemsCopy()
            : new List<InventoryItem>();

        result.RemoveAll(item => item == null || item.skin == null);
        result.Sort((left, right) => CompareItems(left, right, sortMode));
        return result;
    }

    private void TryInitialize()
    {
        if (initialized ||
            SaveManager.Instance == null ||
            InventoryManager.Instance == null)
        {
            return;
        }

        database = ResolveDatabase();

        if (database == null)
            return;

        initialized = true;
        BindState(GetState(), true);
        ResolveUpgradeSubscription();
        RefreshTimeSensitiveSystems();
    }

    private void EnsureCurrentState()
    {
        if (!initialized)
            TryInitialize();

        TrophyRoomSaveData current = GetState();

        if (!ReferenceEquals(observedState, current))
            BindState(current, true);
    }

    private void BindState(
        TrophyRoomSaveData state,
        bool migrateLegacyItems)
    {
        observedState = state;

        if (observedState == null)
        {
            displayedBySlot.Clear();
            return;
        }

        if (observedState.displayedItems == null)
        {
            observedState.displayedItems =
                new List<TrophyDisplaySlotSaveData>();
        }

        if (migrateLegacyItems)
            MigrateLegacyDisplayedItems();

        RebuildDisplayedCache(true);
        observedState.unlockedSlots =
            TrophyRoomUpgradeUtility.GetUnlockedSlotCount(ResolveDatabase());
    }

    private void MigrateLegacyDisplayedItems()
    {
        if (observedState == null ||
            observedState.displayedItems == null ||
            InventoryManager.Instance == null)
        {
            return;
        }

        bool changed = false;

        for (int i = observedState.displayedItems.Count - 1; i >= 0; i--)
        {
            TrophyDisplaySlotSaveData record =
                observedState.displayedItems[i];

            if (record == null || record.slotIndex < 0 ||
                record.slotIndex >= TrophyRoomUpgradeUtility.MaximumPedestalCount)
            {
                observedState.displayedItems.RemoveAt(i);
                changed = true;
                continue;
            }

            if (record.storedItem != null &&
                !string.IsNullOrWhiteSpace(record.storedItem.skinApiId))
            {
                continue;
            }

            InventoryItem legacyItem =
                InventoryManager.Instance.GetItemByInstanceId(
                    record.inventoryItemInstanceId);

            if (legacyItem == null)
            {
                observedState.displayedItems.RemoveAt(i);
                changed = true;
                continue;
            }

            InventoryItemSaveData saved =
                TrophyItemSerializationUtility.CreateSave(legacyItem);

            if (saved == null ||
                !InventoryManager.Instance.RemoveItem(legacyItem))
            {
                observedState.displayedItems.RemoveAt(i);
                changed = true;
                continue;
            }

            record.storedItem = saved;
            record.inventoryItemInstanceId = saved.instanceId;
            changed = true;
        }

        if (changed && SaveManager.Instance != null)
            SaveManager.Instance.MarkDirty();
    }

    private void RebuildDisplayedCache(bool cleanInvalidRecords)
    {
        if (rebuilding)
            return;

        rebuilding = true;

        try
        {
            displayedBySlot.Clear();

            if (observedState == null ||
                observedState.displayedItems == null)
            {
                return;
            }

            HashSet<string> usedInstances =
                new HashSet<string>(StringComparer.Ordinal);
            bool changed = false;

            for (int i = observedState.displayedItems.Count - 1; i >= 0; i--)
            {
                TrophyDisplaySlotSaveData record =
                    observedState.displayedItems[i];

                bool invalid =
                    record == null ||
                    record.slotIndex < 0 ||
                    record.slotIndex >=
                    TrophyRoomUpgradeUtility.MaximumPedestalCount ||
                    displayedBySlot.ContainsKey(record.slotIndex) ||
                    record.storedItem == null;

                InventoryItem item = !invalid
                    ? TrophyItemSerializationUtility.CreateRuntimeItem(
                        record.storedItem,
                        ResolveDatabase())
                    : null;

                if (item == null ||
                    string.IsNullOrWhiteSpace(item.instanceId) ||
                    !usedInstances.Add(item.instanceId))
                {
                    invalid = true;
                }

                if (invalid)
                {
                    if (cleanInvalidRecords)
                    {
                        observedState.displayedItems.RemoveAt(i);
                        changed = true;
                    }

                    continue;
                }

                record.inventoryItemInstanceId = item.instanceId;
                displayedBySlot.Add(record.slotIndex, item);
            }

            if (changed && SaveManager.Instance != null)
                SaveManager.Instance.MarkDirty();
        }
        finally
        {
            rebuilding = false;
        }
    }

    private TrophyDisplaySlotSaveData FindSlotRecord(int slotIndex)
    {
        if (observedState == null ||
            observedState.displayedItems == null)
        {
            return null;
        }

        for (int i = 0; i < observedState.displayedItems.Count; i++)
        {
            TrophyDisplaySlotSaveData record =
                observedState.displayedItems[i];

            if (record != null && record.slotIndex == slotIndex)
                return record;
        }

        return null;
    }

    private GameDatabase ResolveDatabase()
    {
        if (database == null && SaveManager.Instance != null)
            database = SaveManager.Instance.database;

        return database;
    }

    private TrophyRoomSaveData GetState()
    {
        return SaveManager.Instance != null &&
               SaveManager.Instance.Museum != null
            ? SaveManager.Instance.Museum.trophyRoom
            : null;
    }

    private void ResolveUpgradeSubscription()
    {
        if (upgradeService == null)
        {
            upgradeService = UpgradeService.Instance != null
                ? UpgradeService.Instance
                : FindFirstObjectByType<UpgradeService>();
        }

        if (!subscribedToUpgrades && upgradeService != null)
        {
            upgradeService.OnUpgradeStateChanged += HandleUpgradesChanged;
            subscribedToUpgrades = true;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedToUpgrades && upgradeService != null)
        {
            upgradeService.OnUpgradeStateChanged -= HandleUpgradesChanged;
        }

        subscribedToUpgrades = false;
    }

    private void HandleUpgradesChanged()
    {
        int previous = observedState != null
            ? observedState.unlockedSlots
            : 0;
        int current = GetUnlockedSlotCount();

        if (previous != current)
            MarkChanged();
    }

    private void MarkChanged()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.MarkDirty();

        OnTrophyRoomChanged?.Invoke();

        if (verboseLogging)
        {
            Debug.Log(
                $"Trophy Room changed. Slots: {GetUnlockedSlotCount()}, " +
                $"Power: {GetTotalWeightedPower()}, Focus: {GetFocus()}.",
                this);
        }
    }

    private static void SettleTimeSensitiveSystems()
    {
        if (MuseumIdleIncomeService.Instance != null)
            MuseumIdleIncomeService.Instance.ProcessElapsedTimeNow(true);
    }

    private static void RefreshTimeSensitiveSystems()
    {
        if (MuseumIdleIncomeService.Instance != null)
            MuseumIdleIncomeService.Instance.ProcessElapsedTimeNow(true);
    }

    private int CompareItems(
        InventoryItem left,
        InventoryItem right,
        TrophyInventorySortMode mode)
    {
        int comparison;

        switch (mode)
        {
            case TrophyInventorySortMode.HighestValue:
                comparison = right.marketValue.CompareTo(left.marketValue);
                break;
            case TrophyInventorySortMode.HighestRarity:
                comparison = ((int)right.skin.rarity).CompareTo(
                    (int)left.skin.rarity);
                break;
            case TrophyInventorySortMode.LowestFloat:
                comparison = GetSortableFloat(left).CompareTo(
                    GetSortableFloat(right));
                break;
            case TrophyInventorySortMode.Newest:
                comparison = right.acquisitionSequence.CompareTo(
                    left.acquisitionSequence);
                break;
            case TrophyInventorySortMode.Weapon:
                comparison = string.Compare(
                    left.skin.weaponName,
                    right.skin.weaponName,
                    StringComparison.OrdinalIgnoreCase);
                break;
            default:
                comparison = EvaluateItem(right, 0).rawTrophyPower.CompareTo(
                    EvaluateItem(left, 0).rawTrophyPower);
                break;
        }

        if (comparison != 0)
            return comparison;

        return string.Compare(
            SkinDisplayUtility.GetDisplayName(left.skin),
            SkinDisplayUtility.GetDisplayName(right.skin),
            StringComparison.OrdinalIgnoreCase);
    }

    private static double GetSortableFloat(InventoryItem item)
    {
        return item == null || item.isVanilla || item.floatValue < 0d
            ? double.MaxValue
            : item.floatValue;
    }
}
