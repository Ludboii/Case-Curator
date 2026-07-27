using System;
using System.Collections.Generic;
using UnityEngine;

public enum AutoAcquisitionActionStatus
{
    Success,
    Locked,
    Invalid,
    InsufficientGold,
    NotBronzeComplete,
    PreviousResearchRequired,
    IntakeVaultFull,
    InventoryFull,
    ServiceUnavailable
}

public sealed class AutoAcquisitionActionResult
{
    public bool success;
    public AutoAcquisitionActionStatus status;
    public string message;
    public double goldAmount;
    public int itemCount;

    public static AutoAcquisitionActionResult Completed(
        string message,
        double goldAmount = 0d,
        int itemCount = 0)
    {
        return new AutoAcquisitionActionResult
        {
            success = true,
            status = AutoAcquisitionActionStatus.Success,
            message = message,
            goldAmount = goldAmount,
            itemCount = itemCount
        };
    }

    public static AutoAcquisitionActionResult Failed(
        AutoAcquisitionActionStatus status,
        string message)
    {
        return new AutoAcquisitionActionResult
        {
            success = false,
            status = status,
            message = message
        };
    }
}

public sealed class AutoAcquisitionWingSnapshot
{
    public bool unlocked;
    public string lockedReason;
    public int ownedCategoryCount;
    public int researchedContainerCount;
    public int intakeCount;
    public int intakeCapacity;
    public int lineCount;
    public float calibrationMultiplier;
    public float processingSeconds;
    public float maximumBudgetPerLine;
    public double offlineShiftHours;
    public int curatorAlertLevel;
    public int lifetimeItemsProcessed;
    public double lifetimeGoldSpent;
    public double lifetimeValueReceived;
    public float bestPullMarketValue;
    public string bestPullSkinApiId;
}

/// <summary>
/// Gameplay authority for the Automated Acquisitions Wing. The service owns
/// category licences, sequential Bronze-gated research, procurement budgets,
/// UTC processing, calibrated opening generation, Intake Vault claims, pause
/// reasons and lifetime reports. UI only reads snapshots and submits commands.
/// </summary>
public sealed class AutoAcquisitionService : MonoBehaviour
{
    public static AutoAcquisitionService Instance { get; private set; }

    [SerializeField] private GameDatabase database;
    [SerializeField] private bool persistBetweenScenes = true;
    [SerializeField] private bool verboseLogging;

    public event Action OnStateChanged;
    public event Action<AutoAcquisitionPendingItemSaveData> OnItemProcessed;
    public event Action<AutoAcquisitionActionResult> OnItemsClaimed;

    private AutoAcquisitionStateSaveData observedState;
    private bool initialized;
    private bool processing;
    private float nextRuntimeTick;

    public GameDatabase Database => database;
    public AutoAcquisitionCatalogData Catalog =>
        database != null ? database.autoAcquisitionCatalog : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GetOrCreate();
    }

    public static AutoAcquisitionService GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        AutoAcquisitionService existing =
            FindFirstObjectByType<AutoAcquisitionService>();

        if (existing != null)
            return existing;

        GameObject go = new GameObject("AutoAcquisitionService");
        return go.AddComponent<AutoAcquisitionService>();
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

        AutoAcquisitionStateSaveData current = GetState();

        if (!ReferenceEquals(current, observedState))
        {
            BindState(current);
            return;
        }

        if (Time.unscaledTime < nextRuntimeTick)
            return;

        ProcessNow();
        ScheduleNextTick();
    }

    private void OnApplicationPause(bool paused)
    {
        if (initialized && paused)
            ProcessNow();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (initialized && focused)
            ProcessNow();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool IsWingUnlocked(out string lockedReason)
    {
        lockedReason = "";

        if (SaveManager.Instance == null)
        {
            lockedReason = "Save data is unavailable.";
            return false;
        }

        if (!TryResolveRank("Global Elite V", out PlayerRank requiredRank))
        {
            lockedReason = "Global Elite V is not configured in PlayerRank.";
            return false;
        }

        if ((int)SaveManager.Instance.CurrentRank < (int)requiredRank)
        {
            lockedReason = "Reach Global Elite V.";
            return false;
        }

        MuseumMilestoneData step40 = FindStaircaseStep(40);

        if (step40 == null || string.IsNullOrWhiteSpace(step40.milestoneId))
        {
            lockedReason = "Museum Staircase step 40 is not configured.";
            return false;
        }

        MuseumMilestoneService milestones =
            MuseumMilestoneService.GetOrCreate();

        if (milestones == null || !milestones.HasClaimed(step40.milestoneId))
        {
            lockedReason = "Claim Museum Staircase step 40.";
            return false;
        }

        return true;
    }

    public AutoAcquisitionWingSnapshot GetSnapshot(bool processNow = true)
    {
        EnsureInitialized();

        if (processNow)
            ProcessNow();

        AutoAcquisitionStateSaveData state = GetState();
        bool unlocked = IsWingUnlocked(out string reason);

        return new AutoAcquisitionWingSnapshot
        {
            unlocked = unlocked,
            lockedReason = reason,
            ownedCategoryCount = state != null && state.ownedCategoryIds != null
                ? state.ownedCategoryIds.Count
                : 0,
            researchedContainerCount =
                state != null && state.researchedContainerIds != null
                    ? state.researchedContainerIds.Count
                    : 0,
            intakeCount = state != null && state.intakeItems != null
                ? state.intakeItems.Count
                : 0,
            intakeCapacity = AutoAcquisitionUpgradeUtility.GetIntakeCapacity(),
            lineCount = AutoAcquisitionUpgradeUtility.GetProcessingLineCount(),
            calibrationMultiplier =
                AutoAcquisitionUpgradeUtility.GetCalibrationMultiplier(),
            processingSeconds =
                AutoAcquisitionUpgradeUtility.GetBaseProcessingSeconds(),
            maximumBudgetPerLine =
                AutoAcquisitionUpgradeUtility.GetMaximumBudgetPerLine(),
            offlineShiftHours =
                AutoAcquisitionUpgradeUtility.GetOfflineShiftHours(),
            curatorAlertLevel =
                AutoAcquisitionUpgradeUtility.GetCuratorAlertLevel(),
            lifetimeItemsProcessed = state != null
                ? state.lifetimeItemsProcessed
                : 0,
            lifetimeGoldSpent = state != null
                ? state.lifetimeGoldSpent
                : 0d,
            lifetimeValueReceived = state != null
                ? state.lifetimeValueReceived
                : 0d,
            bestPullMarketValue = state != null
                ? state.bestPullMarketValue
                : 0f,
            bestPullSkinApiId = state != null
                ? state.bestPullSkinApiId
                : ""
        };
    }

    public IReadOnlyList<AutoAcquisitionCategoryData> GetCategories()
    {
        return Catalog != null && Catalog.categories != null
            ? Catalog.categories
            : Array.Empty<AutoAcquisitionCategoryData>();
    }

    public List<AutoAcquisitionContainerData> GetContainers(
        string categoryId)
    {
        return Catalog != null
            ? Catalog.GetContainersInCategory(categoryId)
            : new List<AutoAcquisitionContainerData>();
    }

    public IReadOnlyList<AutoAcquisitionPendingItemSaveData> GetIntakeItems(
        bool processNow = true)
    {
        EnsureInitialized();

        if (processNow)
            ProcessNow();

        AutoAcquisitionStateSaveData state = GetState();
        return state != null && state.intakeItems != null
            ? state.intakeItems
            : Array.Empty<AutoAcquisitionPendingItemSaveData>();
    }

    public AutoAcquisitionLineSaveData GetLine(int lineIndex)
    {
        EnsureInitialized();
        AutoAcquisitionStateSaveData state = GetState();

        if (state == null || state.lines == null)
            return null;

        for (int i = 0; i < state.lines.Count; i++)
        {
            AutoAcquisitionLineSaveData line = state.lines[i];

            if (line != null && line.lineIndex == lineIndex)
                return line;
        }

        return null;
    }

    public bool OwnsCategory(string categoryId)
    {
        AutoAcquisitionStateSaveData state = GetState();
        return Contains(state != null ? state.ownedCategoryIds : null, categoryId);
    }

    public bool IsContainerResearched(string containerId)
    {
        AutoAcquisitionStateSaveData state = GetState();
        return Contains(
            state != null ? state.researchedContainerIds : null,
            containerId);
    }

    public AutoAcquisitionActionResult BuyCategoryLicense(string categoryId)
    {
        EnsureInitialized();

        if (!IsWingUnlocked(out string wingReason))
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Locked,
                wingReason);
        }

        AutoAcquisitionCategoryData category =
            Catalog != null ? Catalog.GetCategory(categoryId) : null;

        if (category == null)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Invalid,
                "The requested archive category is not configured.");
        }

        if (OwnsCategory(category.categoryId))
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Invalid,
                $"{category.DisplayName} is already licensed.");
        }

        if (SaveManager.Instance == null ||
            !SaveManager.Instance.SpendGold(category.licenseCost))
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.InsufficientGold,
                $"Requires {category.licenseCost:N0} Gold.");
        }

        AddUnique(GetState().ownedCategoryIds, category.categoryId);
        MarkChanged();

        return AutoAcquisitionActionResult.Completed(
            $"Licensed {category.DisplayName} for {category.licenseCost:N0} Gold.",
            category.licenseCost);
    }

    public AutoAcquisitionActionResult ResearchContainer(string containerId)
    {
        EnsureInitialized();

        if (!IsWingUnlocked(out string wingReason))
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Locked,
                wingReason);
        }

        AutoAcquisitionContainerData entry =
            Catalog != null ? Catalog.GetContainer(containerId) : null;

        if (entry == null || entry.container == null)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Invalid,
                "The requested acquisition container is not configured.");
        }

        if (!OwnsCategory(entry.categoryId))
        {
            AutoAcquisitionCategoryData category =
                Catalog.GetCategory(entry.categoryId);

            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Locked,
                $"Purchase the {category?.DisplayName ?? "archive"} licence first.");
        }

        if (IsContainerResearched(entry.containerId))
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Invalid,
                $"{entry.ContainerName} is already researched.");
        }

        AutoAcquisitionContainerData previous = GetPreviousContainer(entry);

        if (previous != null && !IsContainerResearched(previous.containerId))
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.PreviousResearchRequired,
                $"Research {previous.ContainerName} first.");
        }

        if (ContainerProgressManager.Instance == null ||
            !ContainerProgressManager.Instance.IsBronzeComplete(entry.container))
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.NotBronzeComplete,
                $"Reach Bronze Completion on {entry.ContainerName} first.");
        }

        if (SaveManager.Instance == null ||
            !SaveManager.Instance.SpendGold(entry.permanentResearchCost))
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.InsufficientGold,
                $"Requires {entry.permanentResearchCost:N0} Gold.");
        }

        AddUnique(GetState().researchedContainerIds, entry.containerId);
        MarkChanged();

        return AutoAcquisitionActionResult.Completed(
            $"Researched {entry.ContainerName} for " +
            $"{entry.permanentResearchCost:N0} Gold.",
            entry.permanentResearchCost);
    }

    public AutoAcquisitionActionResult SelectLineTarget(
        int lineIndex,
        string containerId)
    {
        EnsureInitialized();

        AutoAcquisitionLineSaveData line = GetUnlockedLine(lineIndex);

        if (line == null)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Locked,
                "This processing line has not been unlocked.");
        }

        AutoAcquisitionContainerData entry =
            Catalog != null ? Catalog.GetContainer(containerId) : null;

        if (entry == null || entry.container == null)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Invalid,
                "The selected container is unavailable.");
        }

        if (!IsContainerResearched(entry.containerId))
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Locked,
                "Research this container before assigning it to a line.");
        }

        line.selectedContainerId = entry.containerId;
        line.active = false;
        line.nextCompletionUtcTicks = 0;
        line.pausedByCuratorAlert = false;
        line.pauseReason = "Ready to start.";
        MarkChanged();

        return AutoAcquisitionActionResult.Completed(
            $"Assigned {entry.ContainerName} to Processing Line {lineIndex + 1}.");
    }

    public AutoAcquisitionActionResult DepositBudget(
        int lineIndex,
        double amount)
    {
        EnsureInitialized();
        AutoAcquisitionLineSaveData line = GetUnlockedLine(lineIndex);

        if (line == null)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Locked,
                "This processing line has not been unlocked.");
        }

        double requested = Math.Max(0d, amount);
        double maximum =
            AutoAcquisitionUpgradeUtility.GetMaximumBudgetPerLine();
        double availableRoom = Math.Max(0d, maximum - line.depositedGold);
        double deposit = Math.Min(requested, availableRoom);

        if (deposit <= 0.0001d)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Invalid,
                $"This line's budget is capped at {maximum:N0} Gold.");
        }

        if (SaveManager.Instance == null ||
            !SaveManager.Instance.SpendGold((float)deposit))
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.InsufficientGold,
                $"Not enough Gold to deposit {deposit:N0}.");
        }

        line.depositedGold += deposit;
        line.pauseReason = line.active ? line.pauseReason : "Ready to start.";
        MarkChanged();

        return AutoAcquisitionActionResult.Completed(
            $"Deposited {deposit:N0} Gold into Processing Line {lineIndex + 1}.",
            deposit);
    }

    public AutoAcquisitionActionResult WithdrawBudget(int lineIndex)
    {
        EnsureInitialized();
        AutoAcquisitionLineSaveData line = GetUnlockedLine(lineIndex);

        if (line == null)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Locked,
                "This processing line has not been unlocked.");
        }

        double amount = Math.Max(0d, line.depositedGold);

        if (amount <= 0.0001d)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Invalid,
                "This processing line has no deposited Gold.");
        }

        line.active = false;
        line.nextCompletionUtcTicks = 0;
        line.depositedGold = 0d;
        line.pauseReason = "Budget withdrawn.";
        SaveManager.Instance.AddGold((float)Math.Min(float.MaxValue, amount));
        MarkChanged();

        return AutoAcquisitionActionResult.Completed(
            $"Returned {amount:N0} Gold from Processing Line {lineIndex + 1}.",
            amount);
    }

    public AutoAcquisitionActionResult SetLineActive(
        int lineIndex,
        bool active)
    {
        EnsureInitialized();
        AutoAcquisitionLineSaveData line = GetUnlockedLine(lineIndex);

        if (line == null)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Locked,
                "This processing line has not been unlocked.");
        }

        if (!active)
        {
            line.active = false;
            line.nextCompletionUtcTicks = 0;
            line.pauseReason = "Stopped by curator.";
            MarkChanged();
            return AutoAcquisitionActionResult.Completed(
                $"Stopped Processing Line {lineIndex + 1}.");
        }

        if (line.pausedByCuratorAlert)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Locked,
                "A Curator Alert must be acknowledged first.");
        }

        AutoAcquisitionContainerData entry =
            Catalog != null
                ? Catalog.GetContainer(line.selectedContainerId)
                : null;

        if (entry == null || entry.container == null ||
            !IsContainerResearched(entry.containerId))
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Invalid,
                "Assign a researched container first.");
        }

        if (line.depositedGold + 0.0001d < entry.container.priceInGold)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.InsufficientGold,
                "Deposit enough Gold for at least one opening.");
        }

        if (IsIntakeFull())
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.IntakeVaultFull,
                "The Uncatalogued Intake Vault is full.");
        }

        line.active = true;
        line.pauseReason = "Processing.";
        line.nextCompletionUtcTicks =
            DateTime.UtcNow.Ticks + GetProcessingDurationTicks(entry);
        MarkChanged();

        return AutoAcquisitionActionResult.Completed(
            $"Started Processing Line {lineIndex + 1}.");
    }

    public AutoAcquisitionActionResult AcknowledgeCuratorAlert(int lineIndex)
    {
        EnsureInitialized();
        AutoAcquisitionLineSaveData line = GetUnlockedLine(lineIndex);

        if (line == null || !line.pausedByCuratorAlert)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Invalid,
                "This line has no Curator Alert to acknowledge.");
        }

        line.pausedByCuratorAlert = false;
        line.active = false;
        line.nextCompletionUtcTicks = 0;
        line.pauseReason = "Alert acknowledged. Ready to restart.";
        MarkChanged();

        return AutoAcquisitionActionResult.Completed(
            $"Acknowledged the alert on Processing Line {lineIndex + 1}.");
    }

    public AutoAcquisitionActionResult ClaimItem(string rewardId)
    {
        EnsureInitialized();
        ProcessNow();

        AutoAcquisitionStateSaveData state = GetState();
        AutoAcquisitionPendingItemSaveData pending =
            FindPendingItem(rewardId);

        if (pending == null || pending.item == null)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Invalid,
                "This Intake Vault item is no longer available.");
        }

        if (InventoryManager.Instance == null)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.ServiceUnavailable,
                "InventoryManager is unavailable.");
        }

        InventoryItem item = AutoAcquisitionItemSerializationUtility.ToRuntimeItem(
            pending.item,
            database);

        if (item == null)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Invalid,
                "The saved Intake Vault item could not be reconstructed.");
        }

        if (!InventoryManager.Instance.TryExecuteTransaction(
                Array.Empty<string>(),
                new List<InventoryItem> { item },
                out InventoryTransactionResult transaction))
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.InventoryFull,
                transaction != null
                    ? transaction.Message
                    : "Inventory is full.");
        }

        state.intakeItems.Remove(pending);
        MarkChanged();

        AutoAcquisitionActionResult result =
            AutoAcquisitionActionResult.Completed(
                $"Claimed {SkinDisplayUtility.GetDisplayName(item.skin)} from " +
                "the Intake Vault.",
                itemCount: 1);

        OnItemsClaimed?.Invoke(result);
        return result;
    }

    public AutoAcquisitionActionResult ClaimAll()
    {
        EnsureInitialized();
        ProcessNow();

        AutoAcquisitionStateSaveData state = GetState();

        if (state == null || state.intakeItems.Count == 0)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Invalid,
                "The Intake Vault is empty.");
        }

        if (InventoryManager.Instance == null)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.ServiceUnavailable,
                "InventoryManager is unavailable.");
        }

        int availableSpace = Math.Max(
            0,
            InventoryManager.Instance.TotalCapacity -
            InventoryManager.Instance.Count);

        if (availableSpace <= 0)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.InventoryFull,
                "Inventory is full.");
        }

        int takeCount = Math.Min(availableSpace, state.intakeItems.Count);
        List<InventoryItem> additions = new List<InventoryItem>(takeCount);
        List<AutoAcquisitionPendingItemSaveData> claimed =
            new List<AutoAcquisitionPendingItemSaveData>(takeCount);

        for (int i = 0; i < state.intakeItems.Count && additions.Count < takeCount; i++)
        {
            AutoAcquisitionPendingItemSaveData pending = state.intakeItems[i];
            InventoryItem item = pending != null
                ? AutoAcquisitionItemSerializationUtility.ToRuntimeItem(
                    pending.item,
                    database)
                : null;

            if (item == null)
                continue;

            additions.Add(item);
            claimed.Add(pending);
        }

        if (additions.Count == 0)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Invalid,
                "No valid Intake Vault items could be reconstructed.");
        }

        if (!InventoryManager.Instance.TryExecuteTransaction(
                Array.Empty<string>(),
                additions,
                out InventoryTransactionResult transaction))
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.InventoryFull,
                transaction != null
                    ? transaction.Message
                    : "Inventory capacity is insufficient.");
        }

        for (int i = 0; i < claimed.Count; i++)
            state.intakeItems.Remove(claimed[i]);

        MarkChanged();

        AutoAcquisitionActionResult result =
            AutoAcquisitionActionResult.Completed(
                $"Claimed {claimed.Count:N0} Intake Vault item(s).",
                itemCount: claimed.Count);

        OnItemsClaimed?.Invoke(result);
        return result;
    }

    public bool ProcessNow()
    {
        if (processing)
            return false;

        EnsureInitialized();

        if (!initialized ||
            !IsWingUnlocked(out _) ||
            Catalog == null)
        {
            return false;
        }

        AutoAcquisitionStateSaveData state = GetState();

        if (state == null)
            return false;

        processing = true;
        bool changed = false;

        try
        {
            long now = DateTime.UtcNow.Ticks;
            long offlineTicks = GetOfflineShiftTicks();
            long earliestEligible = offlineTicks > 0
                ? Math.Max(DateTime.MinValue.Ticks, now - offlineTicks)
                : now;
            int unlockedLines =
                AutoAcquisitionUpgradeUtility.GetProcessingLineCount();

            for (int i = 0; i < state.lines.Count; i++)
            {
                AutoAcquisitionLineSaveData line = state.lines[i];

                if (line == null || line.lineIndex >= unlockedLines)
                    continue;

                if (!line.active || line.pausedByCuratorAlert)
                    continue;

                AutoAcquisitionContainerData entry =
                    Catalog.GetContainer(line.selectedContainerId);

                if (entry == null || entry.container == null ||
                    !IsContainerResearched(entry.containerId))
                {
                    line.active = false;
                    line.pauseReason = "Selected research is unavailable.";
                    line.nextCompletionUtcTicks = 0;
                    changed = true;
                    continue;
                }

                long durationTicks = GetProcessingDurationTicks(entry);

                if (line.nextCompletionUtcTicks <= 0)
                {
                    line.nextCompletionUtcTicks = now + durationTicks;
                    line.pauseReason = "Processing.";
                    changed = true;
                    continue;
                }

                if (line.nextCompletionUtcTicks < earliestEligible)
                {
                    line.nextCompletionUtcTicks = earliestEligible;
                    changed = true;
                }

                int processedThisPass = 0;
                int maximum = Mathf.Max(
                    1,
                    Catalog.maximumOfflineOpeningsPerLine);

                while (line.active &&
                       !line.pausedByCuratorAlert &&
                       line.nextCompletionUtcTicks <= now &&
                       processedThisPass < maximum)
                {
                    AutoAcquisitionActionResult processResult =
                        ProcessOne(line, entry, line.nextCompletionUtcTicks);

                    if (!processResult.success)
                    {
                        line.pauseReason = processResult.message;
                        changed = true;
                        break;
                    }

                    processedThisPass++;
                    changed = true;
                    line.nextCompletionUtcTicks += durationTicks;
                }

                if (processedThisPass >= maximum &&
                    line.nextCompletionUtcTicks <= now)
                {
                    line.nextCompletionUtcTicks = now + durationTicks;
                    line.pauseReason =
                        "Offline processing safety limit reached; continuing live.";
                    changed = true;
                }
            }

            state.lastProcessingUtcTicks = now;

            if (changed)
                MarkChanged();

            return changed;
        }
        finally
        {
            processing = false;
        }
    }

    private AutoAcquisitionActionResult ProcessOne(
        AutoAcquisitionLineSaveData line,
        AutoAcquisitionContainerData entry,
        long completionTicks)
    {
        if (IsIntakeFull())
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.IntakeVaultFull,
                "Paused: Intake Vault full.");
        }

        float cost = Mathf.Max(0f, entry.container.priceInGold);

        if (line.depositedGold + 0.0001d < cost)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.InsufficientGold,
                "Paused: procurement budget depleted.");
        }

        InventoryItem generated = GenerateCalibratedItem(entry.container);

        if (generated == null || generated.skin == null)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.ServiceUnavailable,
                "Paused: container generation failed.");
        }

        generated.marketValue = PriceCalculator.GetPrice(generated);
        InventoryItemSaveData saved =
            AutoAcquisitionItemSerializationUtility.ToSaveData(generated);

        if (saved == null)
        {
            return AutoAcquisitionActionResult.Failed(
                AutoAcquisitionActionStatus.Invalid,
                "Paused: generated item has no stable skin ID.");
        }

        bool newPlaque = IsNewMuseumPlaque(generated);
        string alertReason = EvaluateCuratorAlert(generated, newPlaque);
        bool exceptional = !string.IsNullOrWhiteSpace(alertReason);

        AutoAcquisitionPendingItemSaveData pending =
            new AutoAcquisitionPendingItemSaveData
            {
                rewardId = Guid.NewGuid().ToString(),
                sourceContainerId = entry.containerId,
                sourceContainerName = entry.ContainerName,
                lineIndex = line.lineIndex,
                createdUtcTicks = completionTicks > 0
                    ? completionTicks
                    : DateTime.UtcNow.Ticks,
                item = saved,
                exceptional = exceptional,
                alertReason = alertReason
            };

        AutoAcquisitionStateSaveData state = GetState();
        state.intakeItems.Add(pending);
        line.depositedGold = Math.Max(0d, line.depositedGold - cost);
        line.pauseReason = "Processing.";

        state.lifetimeItemsProcessed++;
        state.lifetimeGoldSpent += cost;
        state.lifetimeValueReceived += Math.Max(0f, generated.marketValue);

        if (generated.marketValue > state.bestPullMarketValue)
        {
            state.bestPullMarketValue = generated.marketValue;
            state.bestPullSkinApiId = generated.skin.apiId;
        }

        if (generated.skin.rarity == Rarity.RareSpecial)
            state.lifetimeRareSpecialPulls++;

        if (newPlaque)
            state.lifetimeNewMuseumPlaques++;

        if (ContainerProgressManager.Instance != null)
        {
            ContainerProgressManager.Instance.RecordContainerOpened(
                entry.container,
                generated,
                cost,
                false);
        }

        if (exceptional && ShouldPauseForAlert(generated, newPlaque))
        {
            line.pausedByCuratorAlert = true;
            line.active = false;
            line.pauseReason = "CURATOR ALERT: " + alertReason;
        }

        if (verboseLogging)
        {
            Debug.Log(
                $"Automated acquisition: {entry.ContainerName} -> " +
                $"{SkinDisplayUtility.GetDisplayName(generated.skin)} " +
                $"({generated.marketValue:N2} Gold).",
                this);
        }

        OnItemProcessed?.Invoke(pending);
        return AutoAcquisitionActionResult.Completed(
            $"Processed {entry.ContainerName}.",
            cost,
            1);
    }

    private InventoryItem GenerateCalibratedItem(CaseData container)
    {
        if (container == null)
            return null;

        float calibration =
            AutoAcquisitionUpgradeUtility.GetCalibrationMultiplier();

        if (calibration >= 0.9999f)
            return CaseOpener.OpenCase(container);

        Rarity lowestRarity = GetLowestConfiguredRarity(container);
        InventoryItem last = null;
        int attempts = Catalog != null
            ? Mathf.Max(1, Catalog.maximumCalibrationAttempts)
            : 32;

        for (int i = 0; i < attempts; i++)
        {
            InventoryItem candidate = CaseOpener.OpenCase(container);

            if (candidate == null || candidate.skin == null)
                continue;

            last = candidate;

            // Lowest-tier outputs are never rejected. Higher-tier manual rolls
            // survive with the calibration percentage, approximating 0.80-1.00
            // of manual rare-output odds without duplicating CaseOpener rules.
            if ((int)candidate.skin.rarity <= (int)lowestRarity ||
                UnityEngine.Random.value <= calibration)
            {
                return candidate;
            }
        }

        return last;
    }

    private static Rarity GetLowestConfiguredRarity(CaseData container)
    {
        Rarity lowest = Rarity.RareSpecial;
        bool found = false;

        if (container != null && container.dropPool != null)
        {
            for (int i = 0; i < container.dropPool.Count; i++)
            {
                WeightedDrop drop = container.dropPool[i];
                SkinData skin = drop != null ? drop.skin : null;

                if (skin == null)
                    continue;

                if (!found || (int)skin.rarity < (int)lowest)
                {
                    lowest = skin.rarity;
                    found = true;
                }
            }
        }

        return found ? lowest : Rarity.Consumer;
    }

    private bool IsNewMuseumPlaque(InventoryItem item)
    {
        if (item == null || item.skin == null)
            return false;

        string key = MuseumDonationKeyUtility.Build(item);

        if (string.IsNullOrWhiteSpace(key))
            return false;

        MuseumService museum = MuseumService.Instance != null
            ? MuseumService.Instance
            : FindFirstObjectByType<MuseumService>();

        MuseumDonationRecordSaveData record =
            museum != null ? museum.GetDonationRecord(key) : null;

        return record == null || record.donatedCount <= 0;
    }

    private string EvaluateCuratorAlert(
        InventoryItem item,
        bool newMuseumPlaque)
    {
        int level = AutoAcquisitionUpgradeUtility.GetCuratorAlertLevel();

        if (level <= 0 || item == null || item.skin == null)
            return "";

        if (item.skin.rarity == Rarity.RareSpecial)
            return "Rare Special item received.";

        if (level >= 4 &&
            (item.patternTier != PatternTier.None ||
             (!item.isVanilla &&
              (item.floatValue <= Catalog.pristineFloatThreshold ||
               item.floatValue >= Catalog.extremeHighFloatThreshold))))
        {
            return "Rare pattern or extreme float received.";
        }

        if (level >= 3 &&
            item.marketValue >= Catalog.exceptionalValueThreshold)
        {
            return $"High-value item received ({item.marketValue:N2} Gold).";
        }

        if (level >= 2 && newMuseumPlaque)
            return "New Museum plaque candidate received.";

        return "";
    }

    private static bool ShouldPauseForAlert(
        InventoryItem item,
        bool newMuseumPlaque)
    {
        int level = AutoAcquisitionUpgradeUtility.GetCuratorAlertLevel();

        if (level <= 0 || item == null || item.skin == null)
            return false;

        if (item.skin.rarity == Rarity.RareSpecial)
            return true;

        if (level >= 4 &&
            (item.patternTier != PatternTier.None ||
             (!item.isVanilla &&
              (item.floatValue <= 0.001d || item.floatValue >= 0.999d))))
        {
            return true;
        }

        // Levels 2 and 3 flag the Intake item but continue processing, matching
        // the balance sheet's optional-pause direction for these alert types.
        return false;
    }

    private bool IsIntakeFull()
    {
        AutoAcquisitionStateSaveData state = GetState();
        int capacity = AutoAcquisitionUpgradeUtility.GetIntakeCapacity();

        return state != null &&
               state.intakeItems != null &&
               state.intakeItems.Count >= capacity;
    }

    private long GetProcessingDurationTicks(
        AutoAcquisitionContainerData entry)
    {
        double seconds = Math.Max(
            1d,
            AutoAcquisitionUpgradeUtility.GetBaseProcessingSeconds() *
            Math.Max(0.01f, entry.processingDurationMultiplier));

        return Math.Max(1L, (long)(seconds * TimeSpan.TicksPerSecond));
    }

    private static long GetOfflineShiftTicks()
    {
        double hours = AutoAcquisitionUpgradeUtility.GetOfflineShiftHours();

        if (hours <= 0d)
            return 0L;

        double ticks = hours * TimeSpan.TicksPerHour;
        return (long)Math.Min(long.MaxValue, Math.Max(0d, ticks));
    }

    private AutoAcquisitionContainerData GetPreviousContainer(
        AutoAcquisitionContainerData target)
    {
        if (target == null || Catalog == null)
            return null;

        List<AutoAcquisitionContainerData> entries =
            Catalog.GetContainersInCategory(target.categoryId);

        for (int i = 0; i < entries.Count; i++)
        {
            if (ReferenceEquals(entries[i], target) ||
                string.Equals(
                    entries[i].containerId,
                    target.containerId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return i > 0 ? entries[i - 1] : null;
            }
        }

        return null;
    }

    private AutoAcquisitionPendingItemSaveData FindPendingItem(
        string rewardId)
    {
        AutoAcquisitionStateSaveData state = GetState();

        if (state == null ||
            state.intakeItems == null ||
            string.IsNullOrWhiteSpace(rewardId))
        {
            return null;
        }

        for (int i = 0; i < state.intakeItems.Count; i++)
        {
            AutoAcquisitionPendingItemSaveData pending = state.intakeItems[i];

            if (pending != null &&
                string.Equals(
                    pending.rewardId,
                    rewardId,
                    StringComparison.Ordinal))
            {
                return pending;
            }
        }

        return null;
    }

    private AutoAcquisitionLineSaveData GetUnlockedLine(int lineIndex)
    {
        int count = AutoAcquisitionUpgradeUtility.GetProcessingLineCount();

        return lineIndex >= 0 && lineIndex < count
            ? GetLine(lineIndex)
            : null;
    }

    private MuseumMilestoneData FindStaircaseStep(int stairNumber)
    {
        if (database == null || database.museumMilestones == null)
            return null;

        for (int i = 0; i < database.museumMilestones.Count; i++)
        {
            MuseumMilestoneData milestone = database.museumMilestones[i];

            if (milestone != null && milestone.stairNumber == stairNumber)
                return milestone;
        }

        return null;
    }

    private void EnsureInitialized()
    {
        if (!initialized)
            TryInitialize();
    }

    private void TryInitialize()
    {
        if (initialized ||
            SaveManager.Instance == null ||
            SaveManager.Instance.Museum == null)
        {
            return;
        }

        if (database == null)
            database = SaveManager.Instance.database;

        if (database == null || database.autoAcquisitionCatalog == null)
            return;

        BindState(GetState());
        initialized = observedState != null;
        ScheduleNextTick();

        if (initialized)
            ProcessNow();
    }

    private void BindState(AutoAcquisitionStateSaveData state)
    {
        if (state == null)
            return;

        observedState = state;
        NormalizeState(state);
        ScheduleNextTick();
    }

    private AutoAcquisitionStateSaveData GetState()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Museum == null)
            return null;

        if (SaveManager.Instance.Museum.automatedAcquisitions == null)
        {
            SaveManager.Instance.Museum.automatedAcquisitions =
                new AutoAcquisitionStateSaveData();
            SaveManager.Instance.MarkDirty();
        }

        return SaveManager.Instance.Museum.automatedAcquisitions;
    }

    private static void NormalizeState(AutoAcquisitionStateSaveData state)
    {
        if (state.ownedCategoryIds == null)
            state.ownedCategoryIds = new List<string>();

        if (state.researchedContainerIds == null)
            state.researchedContainerIds = new List<string>();

        if (state.lines == null)
            state.lines = new List<AutoAcquisitionLineSaveData>();

        if (state.intakeItems == null)
            state.intakeItems = new List<AutoAcquisitionPendingItemSaveData>();

        for (int lineIndex = 0; lineIndex < 3; lineIndex++)
        {
            bool exists = false;

            for (int i = 0; i < state.lines.Count; i++)
            {
                AutoAcquisitionLineSaveData line = state.lines[i];

                if (line != null && line.lineIndex == lineIndex)
                {
                    line.depositedGold = Math.Max(0d, line.depositedGold);
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                state.lines.Add(new AutoAcquisitionLineSaveData
                {
                    lineIndex = lineIndex,
                    pauseReason = lineIndex == 0
                        ? "Assign a researched container."
                        : "Processing line locked."
                });
            }
        }

        for (int i = state.intakeItems.Count - 1; i >= 0; i--)
        {
            AutoAcquisitionPendingItemSaveData pending = state.intakeItems[i];

            if (pending == null || pending.item == null)
            {
                state.intakeItems.RemoveAt(i);
                continue;
            }

            if (string.IsNullOrWhiteSpace(pending.rewardId))
                pending.rewardId = Guid.NewGuid().ToString();
        }

        state.lifetimeItemsProcessed =
            Math.Max(0, state.lifetimeItemsProcessed);
        state.lifetimeGoldSpent = Math.Max(0d, state.lifetimeGoldSpent);
        state.lifetimeValueReceived = Math.Max(0d, state.lifetimeValueReceived);
        state.bestPullMarketValue = Math.Max(0f, state.bestPullMarketValue);
        state.lifetimeRareSpecialPulls =
            Math.Max(0, state.lifetimeRareSpecialPulls);
        state.lifetimeNewMuseumPlaques =
            Math.Max(0, state.lifetimeNewMuseumPlaques);
    }

    private void MarkChanged()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.MarkDirty();

        OnStateChanged?.Invoke();
    }

    private void ScheduleNextTick()
    {
        float interval = Catalog != null
            ? Mathf.Max(0.1f, Catalog.runtimeTickSeconds)
            : 1f;

        nextRuntimeTick = Time.unscaledTime + interval;
    }

    private static bool Contains(List<string> values, string value)
    {
        if (values == null || string.IsNullOrWhiteSpace(value))
            return false;

        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(
                    values[i],
                    value,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (values == null ||
            string.IsNullOrWhiteSpace(value) ||
            Contains(values, value))
        {
            return;
        }

        values.Add(value.Trim());
    }

    private static bool TryResolveRank(
        string displayName,
        out PlayerRank rank)
    {
        string target = Normalize(displayName);
        Array values = Enum.GetValues(typeof(PlayerRank));

        foreach (object value in values)
        {
            PlayerRank candidate = (PlayerRank)value;

            if (Normalize(candidate.ToString()) == target ||
                Normalize(
                    PlayerProgressUtility.GetRankDisplayName(candidate)) == target)
            {
                rank = candidate;
                return true;
            }
        }

        rank = default;
        return false;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        char[] buffer = new char[value.Length];
        int length = 0;

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];

            if (!char.IsLetterOrDigit(character))
                continue;

            buffer[length++] = char.ToLowerInvariant(character);
        }

        return new string(buffer, 0, length);
    }
}
