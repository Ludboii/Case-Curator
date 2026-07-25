using System;
using System.Collections.Generic;

/// <summary>
/// Counts inventory instances that match open Museum donation slots. The real
/// MuseumService preview remains authoritative, so Favorite/Trophy Room copies
/// are reported as protected rather than ready.
/// </summary>
public static class MuseumDonationAvailabilityUtility
{
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

        List<InventoryItem> inventory =
            InventoryManager.Instance.GetItemsCopy();

        for (int i = 0; i < inventory.Count; i++)
        {
            InventoryItem item = inventory[i];

            if (item == null || string.IsNullOrWhiteSpace(item.instanceId))
                continue;

            string donationKey = MuseumDonationKeyUtility.Build(item);

            if (string.IsNullOrWhiteSpace(donationKey) ||
                !openDonationKeys.Contains(donationKey))
            {
                continue;
            }

            MuseumDonationPreview preview =
                service.PreviewDonation(item.instanceId);

            if (preview != null && preview.canDonate)
                readyCount++;
            else
                protectedCount++;
        }
    }
}
