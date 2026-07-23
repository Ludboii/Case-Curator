using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single runtime authority for Museum donations, Museum Points, donation
/// summaries and generated catalog progress. UI must call this service instead
/// of mutating SaveManager.Museum directly.
/// </summary>
public class MuseumService : MonoBehaviour
{
    public static MuseumService Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private GameDatabase database;

    [Header("Lifetime")]
    [SerializeField] private bool persistBetweenScenes = true;

    [Header("Diagnostics")]
    [SerializeField] private bool verboseLogging;

    public event Action OnMuseumChanged;

    private readonly Dictionary<string, MuseumDonationRecordSaveData>
        donationByKey =
            new Dictionary<string, MuseumDonationRecordSaveData>(
                StringComparer.Ordinal);

    private MuseumPointCalculator pointCalculator;
    private MuseumCatalogService catalogService;
    private MuseumCatalogSnapshot cachedCatalog;
    private MuseumStateSaveData indexedMuseumState;
    private bool subscribedToSaveManager;

    public GameDatabase Database => database;

    public MuseumBalanceData Balance =>
        database != null ? database.museumBalance : null;

    public MuseumCatalogConfig CatalogConfig =>
        database != null ? database.museumCatalog : null;

    public double MuseumPoints =>
        SaveManager.Instance != null &&
        SaveManager.Instance.Museum != null
            ? Math.Max(0d, SaveManager.Instance.Museum.museumPoints)
            : 0d;

    public int DonatedSlotCount
    {
        get
        {
            EnsureDonationIndexCurrent();
            int count = 0;

            foreach (MuseumDonationRecordSaveData record
                     in donationByKey.Values)
            {
                if (record != null && record.donatedCount > 0)
                    count++;
            }

            return count;
        }
    }

    public bool IsReady =>
        ResolveDependencies(false) &&
        InventoryManager.Instance != null &&
        Balance != null;

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

        ResolveDependencies(true);
        EnsureDonationIndexCurrent();
    }

    private void Start()
    {
        SubscribeToSaveManager();
        EnsureDonationIndexCurrent();
    }

    private void OnDestroy()
    {
        UnsubscribeFromSaveManager();

        if (Instance == this)
            Instance = null;
    }

    public bool CanDonate(
        InventoryItem item,
        out MuseumDonationPreview preview)
    {
        preview = PreviewDonation(item);
        return preview != null && preview.canDonate;
    }

    public MuseumDonationPreview PreviewDonation(InventoryItem item)
    {
        if (!ResolveDependencies(true) ||
            SaveManager.Instance == null ||
            InventoryManager.Instance == null ||
            Balance == null)
        {
            return MuseumDonationPreview.Rejected(
                MuseumDonationFailureReason.ServiceUnavailable,
                "Museum services or Museum balance data are unavailable.",
                item);
        }

        if (item == null)
        {
            return MuseumDonationPreview.Rejected(
                MuseumDonationFailureReason.MissingInventoryItem,
                "No inventory item was selected.");
        }

        if (item.skin == null)
        {
            return MuseumDonationPreview.Rejected(
                MuseumDonationFailureReason.MissingSkinData,
                "The selected inventory item has no SkinData.",
                item);
        }

        if (string.IsNullOrWhiteSpace(item.skin.apiId))
        {
            return MuseumDonationPreview.Rejected(
                MuseumDonationFailureReason.MissingStableSkinId,
                "This skin has no stable API ID and cannot be saved in the Museum.",
                item);
        }

        if (string.IsNullOrWhiteSpace(item.instanceId) ||
            InventoryManager.Instance.GetItemByInstanceId(item.instanceId) != item)
        {
            return MuseumDonationPreview.Rejected(
                MuseumDonationFailureReason.ItemNotOwned,
                "The selected item is no longer present in the inventory.",
                item);
        }

        if (item.favorite)
        {
            return MuseumDonationPreview.Rejected(
                MuseumDonationFailureReason.FavoriteItem,
                "Favorited items must be unfavorited before donation.",
                item);
        }

        if (IsDisplayedInTrophyRoom(item.instanceId))
        {
            return MuseumDonationPreview.Rejected(
                MuseumDonationFailureReason.TrophyRoomItem,
                "Remove this item from the Trophy Room before donation.",
                item);
        }

        MuseumDonationVariant variant =
            MuseumDonationKeyUtility.GetVariant(item);

        bool vanilla = item.isVanilla || item.skin.isVanilla;

        if (!IsSlotEnabled(item.skin, variant, vanilla))
        {
            return MuseumDonationPreview.Rejected(
                MuseumDonationFailureReason.VariantDisabled,
                "This Museum slot type is disabled by Museum balance settings.",
                item);
        }

        string donationKey = MuseumDonationKeyUtility.Build(item);

        if (string.IsNullOrWhiteSpace(donationKey))
        {
            return MuseumDonationPreview.Rejected(
                MuseumDonationFailureReason.MissingStableSkinId,
                "A stable Museum slot key could not be generated.",
                item);
        }

        EnsureDonationIndexCurrent();

        bool alreadyDonated =
            donationByKey.TryGetValue(
                donationKey,
                out MuseumDonationRecordSaveData existing) &&
            existing != null &&
            existing.donatedCount > 0;

        if (Balance.oneDonationPerSlot && alreadyDonated)
        {
            return MuseumDonationPreview.Rejected(
                MuseumDonationFailureReason.SlotAlreadyFilled,
                "This exact skin, wear and variant Museum slot is already filled.",
                item);
        }

        pointCalculator = pointCalculator ??
            new MuseumPointCalculator(Balance);

        MuseumPointBreakdown breakdown =
            pointCalculator.Calculate(item);

        int wearIndex = vanilla
            ? -1
            : MuseumDonationKeyUtility.GetWearIndex(item);

        return new MuseumDonationPreview
        {
            canDonate = true,
            failureReason = MuseumDonationFailureReason.None,
            message = $"Donate for {breakdown.totalPoints:0.##} Museum Points.",
            item = item,
            skin = item.skin,
            donationKey = donationKey,
            wearIndex = wearIndex,
            wearTier = MuseumDonationKeyUtility.GetWearTier(wearIndex),
            variant = variant,
            isVanilla = vanilla,
            isFirstDonationForSlot = !alreadyDonated,
            points = breakdown
        };
    }

    public MuseumDonationResult Donate(InventoryItem item)
    {
        MuseumDonationPreview preview = PreviewDonation(item);

        if (preview == null || !preview.canDonate)
            return MuseumDonationResult.Failed(preview);

        List<string> removals = new List<string>
        {
            item.instanceId
        };

        bool removed = InventoryManager.Instance.TryExecuteTransaction(
            removals,
            new List<InventoryItem>(),
            out InventoryTransactionResult inventoryResult);

        if (!removed || inventoryResult == null || !inventoryResult.success)
        {
            string transactionError =
                inventoryResult != null &&
                !string.IsNullOrWhiteSpace(inventoryResult.errorMessage)
                    ? inventoryResult.errorMessage
                    : "The inventory transaction failed.";

            preview.failureReason =
                MuseumDonationFailureReason.InventoryTransactionFailed;

            return MuseumDonationResult.Failed(
                preview,
                transactionError);
        }

        MuseumStateSaveData museumState = SaveManager.Instance.Museum;

        MuseumDonationRecordSaveData record =
            GetOrCreateDonationRecord(museumState, preview);

        record.donatedCount = Math.Max(0, record.donatedCount) + 1;
        record.totalMarketValueDonated +=
            Mathf.Max(0f, item.marketValue);

        record.totalMuseumPointsAwarded +=
            Math.Max(0d, preview.MuseumPoints);

        record.highestMarketValue = Mathf.Max(
            record.highestMarketValue,
            Mathf.Max(0f, item.marketValue));

        if (!preview.isVanilla && item.floatValue >= 0d)
        {
            if (record.bestFloat < 0d || item.floatValue < record.bestFloat)
                record.bestFloat = item.floatValue;
        }

        museumState.museumPoints = Math.Max(
            0d,
            museumState.museumPoints + preview.MuseumPoints);

        donationByKey[preview.donationKey] = record;
        indexedMuseumState = museumState;
        cachedCatalog = null;

        SaveManager.Instance.MarkDirty();
        OnMuseumChanged?.Invoke();

        if (verboseLogging)
        {
            Debug.Log(
                $"Museum donation completed: " +
                $"{SkinDisplayUtility.GetDisplayName(item.skin)} | " +
                $"{preview.variant} | Wear {preview.wearIndex} | " +
                $"+{preview.MuseumPoints:0.##} MP | " +
                $"Total {museumState.museumPoints:0.##} MP",
                this);
        }

        return new MuseumDonationResult
        {
            success = true,
            failureReason = MuseumDonationFailureReason.None,
            message =
                $"Donated for {preview.MuseumPoints:0.##} Museum Points.",
            donatedItem = item,
            donationRecord = record,
            museumPointsAwarded = preview.MuseumPoints,
            totalMuseumPoints = museumState.museumPoints
        };
    }

    public bool HasDonatedSlot(string donationKey)
    {
        if (string.IsNullOrWhiteSpace(donationKey))
            return false;

        EnsureDonationIndexCurrent();

        return donationByKey.TryGetValue(
                   donationKey,
                   out MuseumDonationRecordSaveData record) &&
               record != null &&
               record.donatedCount > 0;
    }

    public MuseumDonationRecordSaveData GetDonationRecord(
        string donationKey)
    {
        if (string.IsNullOrWhiteSpace(donationKey))
            return null;

        EnsureDonationIndexCurrent();
        donationByKey.TryGetValue(
            donationKey,
            out MuseumDonationRecordSaveData record);

        return record;
    }

    public MuseumDonationSummary GetDonationSummary(SkinData skin)
    {
        MuseumDonationSummary summary = new MuseumDonationSummary
        {
            skin = skin
        };

        if (skin == null || string.IsNullOrWhiteSpace(skin.apiId))
            return summary;

        EnsureDonationIndexCurrent();

        foreach (MuseumDonationRecordSaveData record
                 in donationByKey.Values)
        {
            if (record == null ||
                record.donatedCount <= 0 ||
                !string.Equals(
                    record.skinApiId,
                    skin.apiId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            summary.donatedSlots++;
            summary.totalDonations += record.donatedCount;
            summary.totalMarketValueDonated +=
                Mathf.Max(0f, record.totalMarketValueDonated);

            summary.highestMarketValueDonated = Mathf.Max(
                summary.highestMarketValueDonated,
                Mathf.Max(0f, record.highestMarketValue));

            summary.totalMuseumPointsAwarded +=
                Math.Max(0d, record.totalMuseumPointsAwarded);

            if (record.bestFloat >= 0d &&
                (summary.bestFloat < 0d ||
                 record.bestFloat < summary.bestFloat))
            {
                summary.bestFloat = record.bestFloat;
            }

            if (record.souvenir)
                summary.hasSouvenir = true;
            else if (record.statTrak)
                summary.hasStatTrak = true;
            else
                summary.hasNormal = true;
        }

        return summary;
    }

    public MuseumCatalogSnapshot GetCatalogSnapshot(bool forceRebuild = false)
    {
        ResolveDependencies(true);
        EnsureDonationIndexCurrent();

        if (forceRebuild || cachedCatalog == null)
        {
            catalogService = new MuseumCatalogService(
                database,
                CatalogConfig,
                Balance);

            cachedCatalog = catalogService.BuildCatalog(HasDonatedSlot);
        }

        return cachedCatalog;
    }

    public void InvalidateCatalog()
    {
        cachedCatalog = null;
    }

    private MuseumDonationRecordSaveData GetOrCreateDonationRecord(
        MuseumStateSaveData museumState,
        MuseumDonationPreview preview)
    {
        if (donationByKey.TryGetValue(
                preview.donationKey,
                out MuseumDonationRecordSaveData existing) &&
            existing != null)
        {
            return existing;
        }

        MuseumDonationRecordSaveData record =
            new MuseumDonationRecordSaveData
            {
                donationKey = preview.donationKey,
                skinApiId = preview.skin.apiId,
                wearIndex = preview.wearIndex,
                statTrak =
                    preview.variant == MuseumDonationVariant.StatTrak,
                souvenir =
                    preview.variant == MuseumDonationVariant.Souvenir,
                isVanilla = preview.isVanilla,
                bestFloat = -1d
            };

        if (museumState.donations == null)
        {
            museumState.donations =
                new List<MuseumDonationRecordSaveData>();
        }

        museumState.donations.Add(record);
        donationByKey.Add(preview.donationKey, record);
        return record;
    }

    private bool IsSlotEnabled(
        SkinData skin,
        MuseumDonationVariant variant,
        bool vanilla)
    {
        if (skin == null || Balance == null)
            return false;

        if (vanilla && !Balance.includeVanillaSlots)
            return false;

        switch (variant)
        {
            case MuseumDonationVariant.Normal:
                return Balance.includeNormalSlots;

            case MuseumDonationVariant.StatTrak:
                return Balance.includeStatTrakSlots &&
                       skin.canBeStatTrak;

            case MuseumDonationVariant.Souvenir:
                return !vanilla &&
                       Balance.includeSouvenirSlots &&
                       skin.canBeSouvenir;

            default:
                return false;
        }
    }

    private bool IsDisplayedInTrophyRoom(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId) ||
            SaveManager.Instance == null ||
            SaveManager.Instance.Museum == null ||
            SaveManager.Instance.Museum.trophyRoom == null ||
            SaveManager.Instance.Museum.trophyRoom.displayedItems == null)
        {
            return false;
        }

        List<TrophyDisplaySlotSaveData> displayed =
            SaveManager.Instance.Museum.trophyRoom.displayedItems;

        for (int i = 0; i < displayed.Count; i++)
        {
            TrophyDisplaySlotSaveData slot = displayed[i];

            if (slot != null &&
                string.Equals(
                    slot.inventoryItemInstanceId,
                    instanceId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private bool ResolveDependencies(bool logProblems)
    {
        if (database == null && SaveManager.Instance != null)
            database = SaveManager.Instance.database;

        if (database == null)
        {
            if (logProblems)
            {
                Debug.LogWarning(
                    "MuseumService: GameDatabase is not assigned and could not " +
                    "be obtained from SaveManager.",
                    this);
            }

            return false;
        }

        if (database.museumBalance == null)
        {
            if (logProblems)
            {
                Debug.LogWarning(
                    "MuseumService: GameDatabase.museumBalance is not assigned.",
                    database);
            }

            return false;
        }

        if (pointCalculator == null)
            pointCalculator = new MuseumPointCalculator(database.museumBalance);

        return true;
    }

    private void SubscribeToSaveManager()
    {
        if (subscribedToSaveManager || SaveManager.Instance == null)
            return;

        SaveManager.Instance.OnProgressChanged +=
            HandleSaveProgressChanged;

        subscribedToSaveManager = true;
    }

    private void UnsubscribeFromSaveManager()
    {
        if (!subscribedToSaveManager || SaveManager.Instance == null)
            return;

        SaveManager.Instance.OnProgressChanged -=
            HandleSaveProgressChanged;

        subscribedToSaveManager = false;
    }

    private void HandleSaveProgressChanged()
    {
        EnsureDonationIndexCurrent(true);
        cachedCatalog = null;
        OnMuseumChanged?.Invoke();
    }

    private void EnsureDonationIndexCurrent(bool force = false)
    {
        if (SaveManager.Instance == null ||
            SaveManager.Instance.Museum == null)
        {
            donationByKey.Clear();
            indexedMuseumState = null;
            cachedCatalog = null;
            return;
        }

        MuseumStateSaveData current = SaveManager.Instance.Museum;

        if (!force && indexedMuseumState == current)
            return;

        donationByKey.Clear();
        indexedMuseumState = current;
        cachedCatalog = null;

        if (current.donations == null)
        {
            current.donations =
                new List<MuseumDonationRecordSaveData>();
            SaveManager.Instance.MarkDirty();
            return;
        }

        bool repaired = false;

        for (int i = current.donations.Count - 1; i >= 0; i--)
        {
            MuseumDonationRecordSaveData record = current.donations[i];

            if (record == null ||
                string.IsNullOrWhiteSpace(record.skinApiId))
            {
                current.donations.RemoveAt(i);
                repaired = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.donationKey))
            {
                record.donationKey =
                    MuseumDonationKeyUtility.Build(record);
                repaired = true;
            }

            if (string.IsNullOrWhiteSpace(record.donationKey))
            {
                current.donations.RemoveAt(i);
                repaired = true;
                continue;
            }

            if (donationByKey.TryGetValue(
                    record.donationKey,
                    out MuseumDonationRecordSaveData existing) &&
                existing != null &&
                existing != record)
            {
                MergeRecord(existing, record);
                current.donations.RemoveAt(i);
                repaired = true;
                continue;
            }

            NormalizeRecord(record);
            donationByKey[record.donationKey] = record;
        }

        current.museumPoints = Math.Max(0d, current.museumPoints);

        if (repaired)
            SaveManager.Instance.MarkDirty();
    }

    private static void NormalizeRecord(
        MuseumDonationRecordSaveData record)
    {
        if (record == null)
            return;

        record.wearIndex = record.isVanilla
            ? -1
            : Mathf.Clamp(record.wearIndex, 0, 4);

        if (record.souvenir)
            record.statTrak = false;

        record.donatedCount = Math.Max(0, record.donatedCount);
        record.totalMarketValueDonated =
            Mathf.Max(0f, record.totalMarketValueDonated);
        record.totalMuseumPointsAwarded =
            Math.Max(0d, record.totalMuseumPointsAwarded);
        record.highestMarketValue =
            Mathf.Max(0f, record.highestMarketValue);

        if (record.isVanilla)
            record.bestFloat = -1d;
    }

    private static void MergeRecord(
        MuseumDonationRecordSaveData target,
        MuseumDonationRecordSaveData duplicate)
    {
        if (target == null || duplicate == null)
            return;

        NormalizeRecord(target);
        NormalizeRecord(duplicate);

        target.donatedCount += duplicate.donatedCount;
        target.totalMarketValueDonated +=
            duplicate.totalMarketValueDonated;
        target.totalMuseumPointsAwarded +=
            duplicate.totalMuseumPointsAwarded;
        target.highestMarketValue = Mathf.Max(
            target.highestMarketValue,
            duplicate.highestMarketValue);

        if (duplicate.bestFloat >= 0d &&
            (target.bestFloat < 0d ||
             duplicate.bestFloat < target.bestFloat))
        {
            target.bestFloat = duplicate.bestFloat;
        }
    }
}
