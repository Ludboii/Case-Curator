using System;
using System.Collections.Generic;

/// <summary>
/// Builds the Museum's Wing -> Category -> Weapon -> Skin -> Slot hierarchy
/// from GameDatabase.allSkins and MuseumCatalogConfig. No per-skin Museum asset
/// maintenance is required.
/// </summary>
public sealed class MuseumCatalogService
{
    private const float WearEpsilon = 0.000001f;

    private readonly GameDatabase database;
    private readonly MuseumCatalogConfig config;
    private readonly MuseumBalanceData balance;

    public MuseumCatalogService(
        GameDatabase gameDatabase,
        MuseumCatalogConfig catalogConfig,
        MuseumBalanceData balanceData)
    {
        database = gameDatabase;
        config = catalogConfig;
        balance = balanceData;
    }

    public MuseumCatalogSnapshot BuildCatalog(
        Func<string, bool> isSlotDonated)
    {
        MuseumCatalogSnapshot snapshot = new MuseumCatalogSnapshot();

        if (database == null || database.allSkins == null)
            return snapshot;

        if (config == null || config.wings == null || config.wings.Count == 0)
        {
            BuildFallbackCatalog(snapshot, isSlotDonated);
            return snapshot;
        }

        List<MuseumWingConfig> sortedWings =
            new List<MuseumWingConfig>();

        for (int i = 0; i < config.wings.Count; i++)
        {
            if (config.wings[i] != null)
                sortedWings.Add(config.wings[i]);
        }

        sortedWings.Sort((a, b) =>
        {
            int orderCompare = a.sortOrder.CompareTo(b.sortOrder);
            return orderCompare != 0
                ? orderCompare
                : string.Compare(a.DisplayName, b.DisplayName,
                    StringComparison.OrdinalIgnoreCase);
        });

        HashSet<string> assignedSkinIds =
            new HashSet<string>(StringComparer.Ordinal);

        for (int wingIndex = 0;
             wingIndex < sortedWings.Count;
             wingIndex++)
        {
            MuseumWingConfig wingConfig = sortedWings[wingIndex];
            MuseumWingEntry wingEntry = new MuseumWingEntry
            {
                config = wingConfig
            };

            List<MuseumCategoryConfig> sortedCategories =
                GetSortedCategories(wingConfig);

            for (int categoryIndex = 0;
                 categoryIndex < sortedCategories.Count;
                 categoryIndex++)
            {
                MuseumCategoryConfig categoryConfig =
                    sortedCategories[categoryIndex];

                MuseumCategoryEntry categoryEntry = BuildCategory(
                    categoryConfig,
                    assignedSkinIds,
                    isSlotDonated,
                    snapshot);

                wingEntry.categories.Add(categoryEntry);
                wingEntry.totalSlots += categoryEntry.totalSlots;
                wingEntry.donatedSlots += categoryEntry.donatedSlots;
            }

            snapshot.wings.Add(wingEntry);
            snapshot.totalSlots += wingEntry.totalSlots;
            snapshot.donatedSlots += wingEntry.donatedSlots;
        }

        return snapshot;
    }

    private MuseumCategoryEntry BuildCategory(
        MuseumCategoryConfig categoryConfig,
        HashSet<string> assignedSkinIds,
        Func<string, bool> isSlotDonated,
        MuseumCatalogSnapshot snapshot)
    {
        MuseumCategoryEntry categoryEntry = new MuseumCategoryEntry
        {
            config = categoryConfig
        };

        Dictionary<string, MuseumWeaponEntry> weapons =
            new Dictionary<string, MuseumWeaponEntry>(
                StringComparer.OrdinalIgnoreCase);

        for (int skinIndex = 0;
             skinIndex < database.allSkins.Count;
             skinIndex++)
        {
            SkinData skin = database.allSkins[skinIndex];

            if (!IsEligibleSkin(skin, categoryConfig))
                continue;

            string stableSkinId = GetStableSkinId(skin);

            // A skin can only contribute to one configured category. This
            // prevents accidental overlapping filters from double-counting
            // Museum completion.
            if (!assignedSkinIds.Add(stableSkinId))
                continue;

            MuseumSkinEntry skinEntry = BuildSkinEntry(
                skin,
                categoryConfig != null ? categoryConfig.filter : null,
                isSlotDonated);

            if (skinEntry.TotalSlots <= 0)
                continue;

            if (!weapons.TryGetValue(
                    skinEntry.weaponName,
                    out MuseumWeaponEntry weaponEntry))
            {
                weaponEntry = new MuseumWeaponEntry
                {
                    weaponName = skinEntry.weaponName
                };

                weapons.Add(skinEntry.weaponName, weaponEntry);
            }

            weaponEntry.skins.Add(skinEntry);
            weaponEntry.totalSlots += skinEntry.TotalSlots;
            weaponEntry.donatedSlots += skinEntry.DonatedSlots;
            snapshot.totalSkins++;
        }

        foreach (MuseumWeaponEntry weapon in weapons.Values)
        {
            weapon.skins.Sort((a, b) =>
                string.Compare(
                    GetSkinDisplayName(a != null ? a.skin : null),
                    GetSkinDisplayName(b != null ? b.skin : null),
                    StringComparison.OrdinalIgnoreCase));

            categoryEntry.weapons.Add(weapon);
            categoryEntry.totalSlots += weapon.totalSlots;
            categoryEntry.donatedSlots += weapon.donatedSlots;
        }

        categoryEntry.weapons.Sort((a, b) =>
            string.Compare(
                a != null ? a.weaponName : "",
                b != null ? b.weaponName : "",
                StringComparison.OrdinalIgnoreCase));

        return categoryEntry;
    }

    private MuseumSkinEntry BuildSkinEntry(
        SkinData skin,
        MuseumCatalogFilter filter,
        Func<string, bool> isSlotDonated)
    {
        MuseumSkinEntry entry = new MuseumSkinEntry
        {
            skin = skin,
            weaponName = !string.IsNullOrWhiteSpace(skin.weaponName)
                ? skin.weaponName
                : "Unknown Weapon"
        };

        bool vanilla = skin.isVanilla;

        if (vanilla)
        {
            if (ShouldIncludeVanilla(filter))
            {
                AddVariantSlots(
                    entry,
                    skin,
                    -1,
                    MuseumWearTier.FactoryNew,
                    true,
                    filter,
                    isSlotDonated);
            }

            return entry;
        }

        for (int wearIndex = 0; wearIndex < 5; wearIndex++)
        {
            if (!IsWearAvailable(skin, wearIndex))
                continue;

            AddVariantSlots(
                entry,
                skin,
                wearIndex,
                MuseumDonationKeyUtility.GetWearTier(wearIndex),
                false,
                filter,
                isSlotDonated);
        }

        return entry;
    }

    private void AddVariantSlots(
        MuseumSkinEntry entry,
        SkinData skin,
        int wearIndex,
        MuseumWearTier wearTier,
        bool isVanilla,
        MuseumCatalogFilter filter,
        Func<string, bool> isSlotDonated)
    {
        if (ShouldIncludeNormal(filter))
        {
            AddSlot(
                entry,
                skin,
                wearIndex,
                wearTier,
                MuseumDonationVariant.Normal,
                isVanilla,
                isSlotDonated);
        }

        if (skin.canBeStatTrak && ShouldIncludeStatTrak(filter))
        {
            AddSlot(
                entry,
                skin,
                wearIndex,
                wearTier,
                MuseumDonationVariant.StatTrak,
                isVanilla,
                isSlotDonated);
        }

        if (!isVanilla &&
            skin.canBeSouvenir &&
            ShouldIncludeSouvenir(filter))
        {
            AddSlot(
                entry,
                skin,
                wearIndex,
                wearTier,
                MuseumDonationVariant.Souvenir,
                false,
                isSlotDonated);
        }
    }

    private static void AddSlot(
        MuseumSkinEntry entry,
        SkinData skin,
        int wearIndex,
        MuseumWearTier wearTier,
        MuseumDonationVariant variant,
        bool isVanilla,
        Func<string, bool> isSlotDonated)
    {
        string key = MuseumDonationKeyUtility.Build(
            skin,
            wearIndex,
            variant,
            isVanilla);

        if (string.IsNullOrWhiteSpace(key))
            return;

        entry.slots.Add(new MuseumSlotEntry
        {
            donationKey = key,
            skin = skin,
            wearIndex = wearIndex,
            wearTier = wearTier,
            variant = variant,
            isVanilla = isVanilla,
            donated = isSlotDonated != null && isSlotDonated(key)
        });
    }

    private bool IsEligibleSkin(
        SkinData skin,
        MuseumCategoryConfig category)
    {
        if (skin == null || string.IsNullOrWhiteSpace(skin.apiId))
            return false;

        MuseumCatalogFilter filter =
            category != null ? category.filter : null;

        if (filter == null)
            return true;

        switch (filter.filterMode)
        {
            case MuseumCatalogFilterMode.AllSkins:
                return true;

            case MuseumCatalogFilterMode.RareSpecialOnly:
                return skin.rarity == Rarity.RareSpecial;

            case MuseumCatalogFilterMode.NonRareSpecialOnly:
                return skin.rarity != Rarity.RareSpecial;

            case MuseumCatalogFilterMode.ListedWeapons:
                return ContainsWeaponName(
                    filter.weaponNames,
                    skin.weaponName);

            default:
                return false;
        }
    }

    private bool ShouldIncludeNormal(MuseumCatalogFilter filter)
    {
        return (balance == null || balance.includeNormalSlots) &&
               (filter == null || filter.includeNormal);
    }

    private bool ShouldIncludeStatTrak(MuseumCatalogFilter filter)
    {
        return (balance == null || balance.includeStatTrakSlots) &&
               (filter == null || filter.includeStatTrak);
    }

    private bool ShouldIncludeSouvenir(MuseumCatalogFilter filter)
    {
        return (balance == null || balance.includeSouvenirSlots) &&
               (filter == null || filter.includeSouvenir);
    }

    private bool ShouldIncludeVanilla(MuseumCatalogFilter filter)
    {
        return (balance == null || balance.includeVanillaSlots) &&
               (filter == null || filter.includeVanilla);
    }

    private static bool IsWearAvailable(SkinData skin, int wearIndex)
    {
        if (skin == null)
            return false;

        float lower;
        float upper;

        switch (wearIndex)
        {
            case 0:
                lower = 0f;
                upper = 0.07f;
                break;
            case 1:
                lower = 0.07f;
                upper = 0.15f;
                break;
            case 2:
                lower = 0.15f;
                upper = 0.38f;
                break;
            case 3:
                lower = 0.38f;
                upper = 0.45f;
                break;
            default:
                lower = 0.45f;
                upper = 1.000001f;
                break;
        }

        return skin.maxFloat > lower + WearEpsilon &&
               skin.minFloat < upper - WearEpsilon;
    }

    private void BuildFallbackCatalog(
        MuseumCatalogSnapshot snapshot,
        Func<string, bool> isSlotDonated)
    {
        MuseumWingConfig fallbackWingConfig = new MuseumWingConfig
        {
            wingId = "museum-all",
            displayName = "Museum Collection",
            categories = new List<MuseumCategoryConfig>()
        };

        MuseumCategoryConfig fallbackCategoryConfig =
            new MuseumCategoryConfig
            {
                categoryId = "museum-all-skins",
                displayName = "All Skins",
                filter = new MuseumCatalogFilter
                {
                    filterMode = MuseumCatalogFilterMode.AllSkins
                }
            };

        fallbackWingConfig.categories.Add(fallbackCategoryConfig);

        MuseumWingEntry wing = new MuseumWingEntry
        {
            config = fallbackWingConfig
        };

        MuseumCategoryEntry category = BuildCategory(
            fallbackCategoryConfig,
            new HashSet<string>(StringComparer.Ordinal),
            isSlotDonated,
            snapshot);

        wing.categories.Add(category);
        wing.totalSlots = category.totalSlots;
        wing.donatedSlots = category.donatedSlots;

        snapshot.wings.Add(wing);
        snapshot.totalSlots = wing.totalSlots;
        snapshot.donatedSlots = wing.donatedSlots;
    }

    private static List<MuseumCategoryConfig> GetSortedCategories(
        MuseumWingConfig wing)
    {
        List<MuseumCategoryConfig> result =
            new List<MuseumCategoryConfig>();

        if (wing == null || wing.categories == null)
            return result;

        for (int i = 0; i < wing.categories.Count; i++)
        {
            if (wing.categories[i] != null)
                result.Add(wing.categories[i]);
        }

        result.Sort((a, b) =>
        {
            int orderCompare = a.sortOrder.CompareTo(b.sortOrder);
            return orderCompare != 0
                ? orderCompare
                : string.Compare(a.DisplayName, b.DisplayName,
                    StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    private static bool ContainsWeaponName(
        List<string> configuredNames,
        string weaponName)
    {
        if (configuredNames == null ||
            configuredNames.Count == 0 ||
            string.IsNullOrWhiteSpace(weaponName))
        {
            return false;
        }

        for (int i = 0; i < configuredNames.Count; i++)
        {
            if (string.Equals(
                    configuredNames[i],
                    weaponName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetStableSkinId(SkinData skin)
    {
        if (skin == null)
            return "";

        return !string.IsNullOrWhiteSpace(skin.apiId)
            ? skin.apiId
            : skin.name;
    }

    private static string GetSkinDisplayName(SkinData skin)
    {
        if (skin == null)
            return "";

        return skin.isVanilla || string.IsNullOrWhiteSpace(skin.skinName)
            ? "Vanilla"
            : skin.skinName;
    }
}
