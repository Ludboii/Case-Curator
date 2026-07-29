using System;
using System.Collections.Generic;
using UnityEngine;

public enum StickerActionStatus
{
    Success,
    Invalid,
    NotOwned,
    Favorited,
    SlotOccupied,
    SlotEmpty,
    InventoryFull,
    InsufficientGold,
    UnsupportedItem,
    ServiceUnavailable
}

public sealed class StickerActionResult
{
    public bool success;
    public StickerActionStatus status;
    public string message;
    public float goldAmount;
    public float valueAmount;

    public static StickerActionResult Completed(
        string message,
        float goldAmount = 0f,
        float valueAmount = 0f)
    {
        return new StickerActionResult
        {
            success = true,
            status = StickerActionStatus.Success,
            message = message,
            goldAmount = goldAmount,
            valueAmount = valueAmount
        };
    }

    public static StickerActionResult Failed(
        StickerActionStatus status,
        string message)
    {
        return new StickerActionResult
        {
            success = false,
            status = status,
            message = message
        };
    }
}

/// <summary>
/// Gameplay authority for applying, moving, swapping and removing stickers.
/// Unapplied stickers are ordinary non-stackable InventoryItems. Applied
/// stickers move into a SaveData sidecar keyed by the owning skin instance ID.
/// </summary>
public sealed class StickerApplicationService : MonoBehaviour
{
    public const int StickerSlotCount = 4;
    public const float AppliedValuePercent = 0.20f;
    public const float FourIdenticalCraftMultiplier = 1.05f;
    public const float ExpensiveConfirmationThreshold = 500f;

    public static StickerApplicationService Instance { get; private set; }

    public event Action OnStickerStateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GetOrCreate();
    }

    public static StickerApplicationService GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        StickerApplicationService existing =
            FindFirstObjectByType<StickerApplicationService>();

        if (existing != null)
            return existing;

        GameObject go = new GameObject("StickerApplicationService");
        DontDestroyOnLoad(go);
        return go.AddComponent<StickerApplicationService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public IReadOnlyList<AppliedStickerSaveData> GetAppliedStickers(
        InventoryItem skinItem)
    {
        StickerCraftSaveData craft = GetCraft(skinItem, false);
        return craft != null && craft.appliedStickers != null
            ? craft.appliedStickers
            : Array.Empty<AppliedStickerSaveData>();
    }

    public AppliedStickerSaveData GetAppliedSticker(
        InventoryItem skinItem,
        int slotIndex)
    {
        StickerCraftSaveData craft = GetCraft(skinItem, false);

        if (craft == null || craft.appliedStickers == null)
            return null;

        int safeSlot = Mathf.Clamp(slotIndex, 0, StickerSlotCount - 1);

        for (int i = 0; i < craft.appliedStickers.Count; i++)
        {
            AppliedStickerSaveData sticker = craft.appliedStickers[i];

            if (sticker != null && sticker.slotIndex == safeSlot)
                return sticker;
        }

        return null;
    }

    public StickerData ResolveSticker(AppliedStickerSaveData applied)
    {
        if (applied == null || string.IsNullOrWhiteSpace(applied.stickerApiId))
            return null;

        GameDatabase database = GetDatabase();
        return database != null
            ? database.GetStickerByApiId(applied.stickerApiId)
            : null;
    }

    public StickerActionResult ApplySticker(
        InventoryItem skinItem,
        InventoryItem stickerItem,
        int slotIndex)
    {
        EnsureState();

        if (!SupportsStickers(skinItem))
        {
            return StickerActionResult.Failed(
                StickerActionStatus.UnsupportedItem,
                "This item cannot accept stickers.");
        }

        StickerData sticker = StickerItemUtility.GetSticker(stickerItem);

        if (sticker == null || string.IsNullOrWhiteSpace(sticker.apiId))
        {
            return StickerActionResult.Failed(
                StickerActionStatus.Invalid,
                "The selected item is not a configured sticker.");
        }

        if (stickerItem.favorite)
        {
            return StickerActionResult.Failed(
                StickerActionStatus.Favorited,
                "Favorited stickers cannot be applied. Unfavorite it first.");
        }

        if (InventoryManager.Instance == null)
        {
            return StickerActionResult.Failed(
                StickerActionStatus.ServiceUnavailable,
                "InventoryManager is unavailable.");
        }

        InventoryItem ownedSkin = InventoryManager.Instance.GetItemByInstanceId(
            skinItem.instanceId);
        InventoryItem ownedSticker = InventoryManager.Instance.GetItemByInstanceId(
            stickerItem.instanceId);

        if (!ReferenceEquals(ownedSkin, skinItem) ||
            !ReferenceEquals(ownedSticker, stickerItem))
        {
            return StickerActionResult.Failed(
                StickerActionStatus.NotOwned,
                "The skin or sticker is no longer in the inventory.");
        }

        int safeSlot = Mathf.Clamp(slotIndex, 0, StickerSlotCount - 1);

        if (GetAppliedSticker(skinItem, safeSlot) != null)
        {
            return StickerActionResult.Failed(
                StickerActionStatus.SlotOccupied,
                "Remove the current sticker before replacing it.");
        }

        if (!InventoryManager.Instance.TryExecuteTransaction(
                new[] { stickerItem.instanceId },
                Array.Empty<InventoryItem>(),
                out InventoryTransactionResult transaction))
        {
            return StickerActionResult.Failed(
                StickerActionStatus.NotOwned,
                transaction != null
                    ? transaction.Message
                    : "The sticker could not be removed from inventory.");
        }

        StickerCraftSaveData craft = GetCraft(skinItem, true);
        craft.appliedStickers.Add(new AppliedStickerSaveData
        {
            slotIndex = safeSlot,
            stickerApiId = sticker.apiId,
            stickerInstanceId = stickerItem.instanceId,
            acquisitionSequence = stickerItem.acquisitionSequence,
            favorite = stickerItem.favorite,
            originalStorageIndex = stickerItem.storageIndex,
            condition = 1f
        });

        NormalizeCraft(craft);
        float contribution = sticker.marketValue * AppliedValuePercent;
        MarkChanged();

        return StickerActionResult.Completed(
            $"Applied {sticker.DisplayName} to slot {safeSlot + 1}.",
            valueAmount: contribution);
    }

    public StickerActionResult RemoveSticker(
        InventoryItem skinItem,
        int slotIndex)
    {
        EnsureState();

        AppliedStickerSaveData applied = GetAppliedSticker(skinItem, slotIndex);

        if (applied == null)
        {
            return StickerActionResult.Failed(
                StickerActionStatus.SlotEmpty,
                "This sticker slot is empty.");
        }

        StickerData sticker = ResolveSticker(applied);

        if (sticker == null)
        {
            return StickerActionResult.Failed(
                StickerActionStatus.Invalid,
                "The applied sticker data could not be resolved.");
        }

        if (InventoryManager.Instance == null || SaveManager.Instance == null)
        {
            return StickerActionResult.Failed(
                StickerActionStatus.ServiceUnavailable,
                "Inventory or save services are unavailable.");
        }

        if (!InventoryManager.Instance.HasSpace())
        {
            return StickerActionResult.Failed(
                StickerActionStatus.InventoryFull,
                "Inventory is full. Free one slot before removing this sticker.");
        }

        float removalCost = GetRemovalCost(sticker.marketValue);

        if (SaveManager.Instance.Gold + 0.0001f < removalCost)
        {
            return StickerActionResult.Failed(
                StickerActionStatus.InsufficientGold,
                $"Removing this sticker costs {removalCost:N0} Gold.");
        }

        InventoryItem restored = new InventoryItem
        {
            instanceId = string.IsNullOrWhiteSpace(applied.stickerInstanceId)
                ? Guid.NewGuid().ToString()
                : applied.stickerInstanceId,
            skin = sticker,
            floatValue = -1d,
            patternId = -1,
            patternTier = PatternTier.None,
            acquisitionSequence = applied.acquisitionSequence,
            statTrak = false,
            souvenir = false,
            isVanilla = true,
            favorite = applied.favorite,
            marketValue = Mathf.Max(0f, sticker.marketValue),
            storageIndex = Mathf.Max(0, applied.originalStorageIndex)
        };

        if (!SaveManager.Instance.SpendGold(removalCost))
        {
            return StickerActionResult.Failed(
                StickerActionStatus.InsufficientGold,
                $"Removing this sticker costs {removalCost:N0} Gold.");
        }

        if (!InventoryManager.Instance.TryExecuteTransaction(
                Array.Empty<string>(),
                new[] { restored },
                out InventoryTransactionResult transaction))
        {
            SaveManager.Instance.AddGold(removalCost);

            return StickerActionResult.Failed(
                StickerActionStatus.InventoryFull,
                transaction != null
                    ? transaction.Message
                    : "The sticker could not be returned to inventory.");
        }

        StickerCraftSaveData craft = GetCraft(skinItem, false);

        if (craft != null)
        {
            craft.appliedStickers.Remove(applied);
            RemoveCraftWhenEmpty(craft);
        }

        MarkChanged();

        return StickerActionResult.Completed(
            $"Removed {sticker.DisplayName} for {removalCost:N0} Gold.",
            removalCost);
    }

    public StickerActionResult MoveOrSwapSticker(
        InventoryItem skinItem,
        int fromSlot,
        int toSlot)
    {
        int safeFrom = Mathf.Clamp(fromSlot, 0, StickerSlotCount - 1);
        int safeTo = Mathf.Clamp(toSlot, 0, StickerSlotCount - 1);

        if (safeFrom == safeTo)
        {
            return StickerActionResult.Failed(
                StickerActionStatus.Invalid,
                "Choose a different sticker slot.");
        }

        AppliedStickerSaveData source = GetAppliedSticker(skinItem, safeFrom);

        if (source == null)
        {
            return StickerActionResult.Failed(
                StickerActionStatus.SlotEmpty,
                "The source sticker slot is empty.");
        }

        AppliedStickerSaveData destination = GetAppliedSticker(skinItem, safeTo);
        source.slotIndex = safeTo;

        if (destination != null)
            destination.slotIndex = safeFrom;

        StickerCraftSaveData craft = GetCraft(skinItem, false);
        NormalizeCraft(craft);
        MarkChanged();

        return StickerActionResult.Completed(
            destination == null
                ? $"Moved sticker to slot {safeTo + 1}."
                : $"Swapped slots {safeFrom + 1} and {safeTo + 1}.");
    }

    public bool DestroyCraft(string skinInventoryItemInstanceId)
    {
        StickerSystemSaveData state = GetState();

        if (state == null || state.crafts == null ||
            string.IsNullOrWhiteSpace(skinInventoryItemInstanceId))
        {
            return false;
        }

        for (int i = state.crafts.Count - 1; i >= 0; i--)
        {
            StickerCraftSaveData craft = state.crafts[i];

            if (craft != null &&
                string.Equals(
                    craft.skinInventoryItemInstanceId,
                    skinInventoryItemInstanceId,
                    StringComparison.Ordinal))
            {
                state.crafts.RemoveAt(i);
                MarkChanged();
                return true;
            }
        }

        return false;
    }

    public float GetAppliedStickerValue(InventoryItem skinItem)
    {
        return StickerValueUtility.GetAppliedStickerContribution(skinItem);
    }

    public static float GetRemovalCost(float stickerMarketValue)
    {
        if (stickerMarketValue >= 200000f)
            return 10000f;

        return Mathf.Clamp(stickerMarketValue * 0.02f, 100f, 10000f);
    }

    public static bool SupportsStickers(InventoryItem item)
    {
        return item != null && SupportsStickers(item.skin);
    }

    public static bool SupportsStickers(SkinData skin)
    {
        if (skin == null || skin is StickerData)
            return false;

        string weaponName = (skin.weaponName ?? "").ToLowerInvariant();
        string[] excludedTerms =
        {
            "knife", "bayonet", "karambit", "dagger", "glove",
            "hand wrap", "handwrap", "falchion", "bowie", "huntsman",
            "butterfly", "navaja", "stiletto", "talon", "ursus",
            "nomad", "paracord", "survival", "skeleton", "kukri",
            "gut knife", "flip knife"
        };

        for (int i = 0; i < excludedTerms.Length; i++)
        {
            if (weaponName.Contains(excludedTerms[i]))
                return false;
        }

        return true;
    }

    private StickerCraftSaveData GetCraft(
        InventoryItem skinItem,
        bool create)
    {
        if (skinItem == null || string.IsNullOrWhiteSpace(skinItem.instanceId))
            return null;

        StickerSystemSaveData state = GetState();

        if (state == null)
            return null;

        for (int i = 0; i < state.crafts.Count; i++)
        {
            StickerCraftSaveData craft = state.crafts[i];

            if (craft != null &&
                string.Equals(
                    craft.skinInventoryItemInstanceId,
                    skinItem.instanceId,
                    StringComparison.Ordinal))
            {
                NormalizeCraft(craft);
                return craft;
            }
        }

        if (!create)
            return null;

        StickerCraftSaveData created = new StickerCraftSaveData
        {
            skinInventoryItemInstanceId = skinItem.instanceId,
            appliedStickers = new List<AppliedStickerSaveData>()
        };
        state.crafts.Add(created);
        return created;
    }

    private StickerSystemSaveData GetState()
    {
        EnsureState();
        return SaveManager.Instance != null && SaveManager.Instance.Museum != null
            ? SaveManager.Instance.Museum.stickerSystem
            : null;
    }

    private void EnsureState()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.Museum == null)
            return;

        MuseumStateSaveData museum = SaveManager.Instance.Museum;

        if (museum.stickerSystem == null)
            museum.stickerSystem = new StickerSystemSaveData();

        if (museum.stickerSystem.crafts == null)
        {
            museum.stickerSystem.crafts =
                new List<StickerCraftSaveData>();
        }

        for (int i = museum.stickerSystem.crafts.Count - 1; i >= 0; i--)
        {
            StickerCraftSaveData craft = museum.stickerSystem.crafts[i];

            if (craft == null ||
                string.IsNullOrWhiteSpace(craft.skinInventoryItemInstanceId))
            {
                museum.stickerSystem.crafts.RemoveAt(i);
                continue;
            }

            NormalizeCraft(craft);
        }
    }

    private static void NormalizeCraft(StickerCraftSaveData craft)
    {
        if (craft == null)
            return;

        if (craft.appliedStickers == null)
            craft.appliedStickers = new List<AppliedStickerSaveData>();

        HashSet<int> usedSlots = new HashSet<int>();

        for (int i = craft.appliedStickers.Count - 1; i >= 0; i--)
        {
            AppliedStickerSaveData sticker = craft.appliedStickers[i];

            if (sticker == null || string.IsNullOrWhiteSpace(sticker.stickerApiId))
            {
                craft.appliedStickers.RemoveAt(i);
                continue;
            }

            sticker.slotIndex = Mathf.Clamp(
                sticker.slotIndex,
                0,
                StickerSlotCount - 1);
            sticker.condition = 1f;
            sticker.originalStorageIndex = Mathf.Max(0, sticker.originalStorageIndex);

            if (!usedSlots.Add(sticker.slotIndex))
                craft.appliedStickers.RemoveAt(i);
        }

        craft.appliedStickers.Sort((a, b) =>
            a.slotIndex.CompareTo(b.slotIndex));
    }

    private void RemoveCraftWhenEmpty(StickerCraftSaveData craft)
    {
        if (craft == null ||
            (craft.appliedStickers != null && craft.appliedStickers.Count > 0))
        {
            return;
        }

        StickerSystemSaveData state = GetState();

        if (state != null && state.crafts != null)
            state.crafts.Remove(craft);
    }

    private GameDatabase GetDatabase()
    {
        return SaveManager.Instance != null
            ? SaveManager.Instance.database
            : null;
    }

    private void MarkChanged()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.RecalculateCachedTotalMarketValue();

        if (SaveManager.Instance != null)
            SaveManager.Instance.MarkDirty();

        OnStickerStateChanged?.Invoke();
    }
}

public static class StickerValueUtility
{
    public static float GetAppliedStickerContribution(InventoryItem skinItem)
    {
        if (skinItem == null ||
            string.IsNullOrWhiteSpace(skinItem.instanceId) ||
            StickerItemUtility.IsSticker(skinItem) ||
            SaveManager.Instance == null ||
            SaveManager.Instance.Museum == null ||
            SaveManager.Instance.Museum.stickerSystem == null)
        {
            return 0f;
        }

        StickerSystemSaveData state =
            SaveManager.Instance.Museum.stickerSystem;

        if (state.crafts == null)
            return 0f;

        StickerCraftSaveData craft = null;

        for (int i = 0; i < state.crafts.Count; i++)
        {
            StickerCraftSaveData candidate = state.crafts[i];

            if (candidate != null &&
                string.Equals(
                    candidate.skinInventoryItemInstanceId,
                    skinItem.instanceId,
                    StringComparison.Ordinal))
            {
                craft = candidate;
                break;
            }
        }

        if (craft == null || craft.appliedStickers == null ||
            craft.appliedStickers.Count == 0)
        {
            return 0f;
        }

        GameDatabase database = SaveManager.Instance.database;

        if (database == null)
            return 0f;

        float total = 0f;
        string identicalId = null;
        bool allIdentical = craft.appliedStickers.Count ==
                            StickerApplicationService.StickerSlotCount;

        for (int i = 0; i < craft.appliedStickers.Count; i++)
        {
            AppliedStickerSaveData applied = craft.appliedStickers[i];
            StickerData sticker = applied != null
                ? database.GetStickerByApiId(applied.stickerApiId)
                : null;

            if (sticker == null)
            {
                allIdentical = false;
                continue;
            }

            total += Mathf.Max(0f, sticker.marketValue) *
                     StickerApplicationService.AppliedValuePercent;

            if (identicalId == null)
                identicalId = sticker.apiId;
            else if (!string.Equals(
                         identicalId,
                         sticker.apiId,
                         StringComparison.Ordinal))
                allIdentical = false;
        }

        if (allIdentical)
        {
            total *= StickerApplicationService.FourIdenticalCraftMultiplier;
        }

        return Mathf.Max(0f, total);
    }
}
