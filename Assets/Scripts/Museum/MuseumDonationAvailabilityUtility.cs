using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Counts inventory instances that match open Museum donation slots. Inventory
/// keys and donation previews are cached once per frame, so building a complete
/// category or weapon view does not rescan the full inventory for every card.
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

    private static MuseumService cachedService;
    private static int cachedFrame = -1;

    public static void Count(
        MuseumSkinEntry skin,
        MuseumService service,
        out int readyCount,
        out int protectedCount)
    {
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
        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

        if (category != null && category.weapons != null)
        {
            for (int weaponIndex = 0;
                 weaponIndex < category.weapons.Count;
                 weaponIndex++)
            {
                MuseumWeaponEntry weapon = category.weapons[weaponIndex];

                if (weapon == null || weapon.skins == null)
                    continue;

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
                ? "1 owned - protected"
                : $"{protectedCount:N0} owned - protected";
        }

        return "";
    }

    /// <summary>
    /// Explicit invalidation hook for callers that mutate inventory and rebuild
    /// Museum UI again within the same rendered frame.
    /// </summary>
    public static void InvalidateCache()
    {
        cachedFrame = -1;
        cachedService = null;
        availabilityByDonationKey.Clear();
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

            readyCount += count.ready;
            protectedCount += count.protectedCount;
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

        if (InventoryManager.Instance == null || service == null)
            return;

        List<InventoryItem> inventory =
            InventoryManager.Instance.GetItemsCopy();

        for (int i = 0; i < inventory.Count; i++)
        {
            InventoryItem item = inventory[i];

            if (item == null || string.IsNullOrWhiteSpace(item.instanceId))
                continue;

            string donationKey = MuseumDonationKeyUtility.Build(item);

            if (string.IsNullOrWhiteSpace(donationKey))
                continue;

            availabilityByDonationKey.TryGetValue(
                donationKey,
                out AvailabilityCount count);

            MuseumDonationPreview preview =
                service.PreviewDonation(item.instanceId);

            if (preview != null && preview.canDonate)
                count.ready++;
            else
                count.protectedCount++;

            availabilityByDonationKey[donationKey] = count;
        }
    }
}
