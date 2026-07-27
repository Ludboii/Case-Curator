using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Counts open Museum donation slots that can be filled by the current
/// inventory. Multiple matching copies of the same exact skin/wear/variant only
/// count once because they all compete for the same permanent Museum slot.
/// The cache is rebuilt once per rendered frame and keeps full donation preview
/// creation reserved for the actual selection flow.
/// </summary>
public static class MuseumDonationAvailabilityUtility
{
    private struct AvailabilityCount
    {
        public int ready;
        public int protectedCount;
    }

    private static readonly Dictionary<string, AvailabilityCount>
        availabilityByDonationKey =
            new Dictionary<string, AvailabilityCount>(StringComparer.Ordinal);

    private static readonly HashSet<string> trophyRoomInstanceIds =
        new HashSet<string>(StringComparer.Ordinal);

    private static MuseumService cachedService;
    private static int cachedFrame = -1;

    public static void Count(
        MuseumSkinEntry skin,
        MuseumService service,
        out int readyCount,
        out int protectedCount)
    {
        readyCount = 0;
        protectedCount = 0;

        if (skin == null ||
            !MuseumUnlockProgressionUtility.IsSkinUnlocked(
                skin.skin,
                GetDatabase(service),
                out _))
        {
            return;
        }

        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        AddOpenKeys(skin, keys);
        CountKeys(keys, service, out readyCount, out protectedCount);
    }

    public static void Count(
        MuseumWeaponEntry weapon,
        MuseumService service,
        out int readyCount,
        out int protectedCount)
    {
        readyCount = 0;
        protectedCount = 0;

        if (!MuseumUnlockProgressionUtility.IsWeaponUnlocked(
                weapon,
                GetDatabase(service),
                out _))
        {
            return;
        }

        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

        if (weapon != null && weapon.skins != null)
        {
            for (int i = 0; i < weapon.skins.Count; i++)
                AddOpenKeys(weapon.skins[i], keys);
        }

        CountKeys(keys, service, out readyCount, out protectedCount);
    }

    public static void Count(
        MuseumCategoryEntry category,
        MuseumService service,
        out int readyCount,
        out int protectedCount)
    {
        readyCount = 0;
        protectedCount = 0;

        if (!MuseumUnlockProgressionUtility.IsCategoryUnlocked(
                category,
                GetDatabase(service),
                out _))
        {
            return;
        }

        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        GameDatabase database = GetDatabase(service);

        if (category != null && category.weapons != null)
        {
            for (int weaponIndex = 0;
                 weaponIndex < category.weapons.Count;
                 weaponIndex++)
            {
                MuseumWeaponEntry weapon = category.weapons[weaponIndex];

                if (weapon == null ||
                    weapon.skins == null ||
                    !MuseumUnlockProgressionUtility.IsWeaponUnlocked(
                        weapon,
                        database,
                        out _))
                {
                    continue;
                }

                for (int skinIndex = 0;
                     skinIndex < weapon.skins.Count;
                     skinIndex++)
                {
                    AddOpenKeys(weapon.skins[skinIndex], keys);
                }
            }
        }

        CountKeys(keys, service, out readyCount, out protectedCount);
    }

    public static string GetStatusText(
        int readyCount,
        int protectedCount)
    {
        if (readyCount > 0)
        {
            return readyCount == 1
                ? "1 ready to donate"
                : $"{readyCount:N0} ready to donate";
        }

        if (protectedCount > 0)
        {
            return protectedCount == 1
                ? "1 slot owned - protected"
                : $"{protectedCount:N0} slots owned - protected";
        }

        return "";
    }

    public static void InvalidateCache()
    {
        cachedFrame = -1;
        cachedService = null;
        availabilityByDonationKey.Clear();
        trophyRoomInstanceIds.Clear();
    }

    private static void AddOpenKeys(
        MuseumSkinEntry skin,
        HashSet<string> keys)
    {
        if (skin == null || skin.slots == null || keys == null)
            return;

        for (int i = 0; i < skin.slots.Count; i++)
        {
            MuseumSlotEntry slot = skin.slots[i];

            if (slot != null &&
                !slot.donated &&
                !string.IsNullOrWhiteSpace(slot.donationKey))
            {
                keys.Add(slot.donationKey);
            }
        }
    }

    private static void CountKeys(
        HashSet<string> openDonationKeys,
        MuseumService service,
        out int readyCount,
        out int protectedCount)
    {
        readyCount = 0;
        protectedCount = 0;

        if (openDonationKeys == null ||
            openDonationKeys.Count == 0 ||
            service == null ||
            InventoryManager.Instance == null)
        {
            return;
        }

        EnsureAvailabilityCache(service);

        foreach (string key in openDonationKeys)
        {
            if (!availabilityByDonationKey.TryGetValue(
                    key,
                    out AvailabilityCount count))
            {
                continue;
            }

            // Count the permanent slot, not every matching inventory instance.
            // A usable copy takes precedence over additional protected copies.
            if (count.ready > 0)
                readyCount++;
            else if (count.protectedCount > 0)
                protectedCount++;
        }
    }

    private static void EnsureAvailabilityCache(MuseumService service)
    {
        int frame = Time.frameCount;

        if (cachedFrame == frame && ReferenceEquals(cachedService, service))
            return;

        cachedFrame = frame;
        cachedService = service;
        availabilityByDonationKey.Clear();
        RebuildTrophyRoomIndex();

        if (InventoryManager.Instance == null || service == null)
            return;

        GameDatabase database = GetDatabase(service);
        List<InventoryItem> inventory =
            InventoryManager.Instance.GetItemsCopy();

        for (int i = 0; i < inventory.Count; i++)
        {
            InventoryItem item = inventory[i];

            if (item == null ||
                item.skin == null ||
                string.IsNullOrWhiteSpace(item.instanceId) ||
                !MuseumUnlockProgressionUtility.IsSkinUnlocked(
                    item.skin,
                    database,
                    out _))
            {
                continue;
            }

            string donationKey = MuseumDonationKeyUtility.Build(item);

            if (string.IsNullOrWhiteSpace(donationKey))
                continue;

            availabilityByDonationKey.TryGetValue(
                donationKey,
                out AvailabilityCount count);

            // Slot validity and already-donated state are handled by the open
            // donation-key set supplied by the generated Museum catalog.
            bool protectedItem =
                item.favorite || trophyRoomInstanceIds.Contains(item.instanceId);

            if (protectedItem)
                count.protectedCount++;
            else
                count.ready++;

            availabilityByDonationKey[donationKey] = count;
        }
    }

    private static GameDatabase GetDatabase(MuseumService service)
    {
        if (service != null && service.Database != null)
            return service.Database;

        return SaveManager.Instance != null
            ? SaveManager.Instance.database
            : null;
    }

    private static void RebuildTrophyRoomIndex()
    {
        trophyRoomInstanceIds.Clear();

        if (SaveManager.Instance == null ||
            SaveManager.Instance.Museum == null ||
            SaveManager.Instance.Museum.trophyRoom == null ||
            SaveManager.Instance.Museum.trophyRoom.displayedItems == null)
        {
            return;
        }

        List<TrophyDisplaySlotSaveData> displayed =
            SaveManager.Instance.Museum.trophyRoom.displayedItems;

        for (int i = 0; i < displayed.Count; i++)
        {
            TrophyDisplaySlotSaveData slot = displayed[i];

            if (slot != null &&
                !string.IsNullOrWhiteSpace(slot.inventoryItemInstanceId))
            {
                trophyRoomInstanceIds.Add(slot.inventoryItemInstanceId);
            }
        }
    }
}
