using System;
using System.Collections.Generic;
using UnityEngine;

public enum MuseumCompletionRewardKind
{
    Skin = 0,
    Weapon = 1,
    Category = 2
}

public sealed class MuseumCompletionRewardPreview
{
    public MuseumCompletionRewardKind kind;
    public string rewardKey;
    public string displayName;
    public string wingId;
    public string categoryId;
    public int totalSlots;
    public int donatedSlots;
    public bool completed;
    public bool claimed;
    public double rewardMuseumPoints;

    public MuseumService service;
    public MuseumSkinEntry skin;
    public MuseumWeaponEntry weapon;
    public MuseumCategoryEntry category;

    public bool CanClaim =>
        completed && !claimed && rewardMuseumPoints > 0d;
}

public sealed class MuseumCompletionRewardClaimResult
{
    public bool success;
    public string message;
    public string rewardKey;
    public MuseumCompletionRewardKind kind;
    public double museumPointsAwarded;
    public double totalMuseumPoints;
}

/// <summary>
/// Authoritative manual-claim service for completed Museum skins, weapons and
/// categories. Reward keys include the wing ID so Arsenal, Souvenir and Rare
/// Special completions remain independent.
/// </summary>
public static class MuseumCompletionRewardService
{
    private const string RewardKeyVersion = "museum-completion-v1";

    private static MuseumCompletionRewardBalanceData cachedBalance;
    private static MuseumCompletionRewardBalanceData runtimeFallback;

    public static MuseumCompletionRewardBalanceData Balance
    {
        get
        {
            if (cachedBalance != null)
                return cachedBalance;

            cachedBalance = Resources.Load<MuseumCompletionRewardBalanceData>(
                MuseumCompletionRewardBalanceData.ResourcesPath);

            if (cachedBalance != null)
                return cachedBalance;

            if (runtimeFallback == null)
            {
                runtimeFallback = ScriptableObject.CreateInstance<
                    MuseumCompletionRewardBalanceData>();
                runtimeFallback.hideFlags = HideFlags.HideAndDontSave;
                runtimeFallback.ResetToDefaults();
            }

            return runtimeFallback;
        }
    }

    public static void InvalidateBalanceCache()
    {
        cachedBalance = null;
    }

    public static MuseumCompletionRewardPreview BuildSkinPreview(
        MuseumSkinEntry skin,
        MuseumService service)
    {
        MuseumCompletionContext context = FindSkinContext(skin, service);

        if (context == null || skin == null || skin.skin == null)
            return EmptyPreview(MuseumCompletionRewardKind.Skin, service);

        int total = Math.Max(0, skin.TotalSlots);
        int donated = Math.Max(0, skin.DonatedSlots);
        bool complete = total > 0 && donated >= total;
        double highestActualDonation = complete
            ? GetHighestActualDonationPoints(skin, service)
            : 0d;
        double reward = complete
            ? Balance.CalculateSkinReward(
                highestActualDonation,
                total,
                skin.skin.rarity)
            : 0d;
        string key = BuildSkinRewardKey(
            context.wing.WingId,
            skin.skin.apiId);

        return new MuseumCompletionRewardPreview
        {
            kind = MuseumCompletionRewardKind.Skin,
            rewardKey = key,
            displayName = SkinDisplayUtility.GetDisplayName(skin.skin),
            wingId = context.wing.WingId,
            categoryId = context.category.CategoryId,
            totalSlots = total,
            donatedSlots = donated,
            completed = complete,
            claimed = IsClaimed(key),
            rewardMuseumPoints = reward,
            service = service,
            skin = skin,
            weapon = context.weapon,
            category = context.category
        };
    }

    public static MuseumCompletionRewardPreview BuildWeaponPreview(
        MuseumWeaponEntry weapon,
        MuseumService service)
    {
        MuseumCompletionContext context = FindWeaponContext(weapon, service);

        if (context == null || weapon == null)
            return EmptyPreview(MuseumCompletionRewardKind.Weapon, service);

        int total = Math.Max(0, weapon.totalSlots);
        int donated = Math.Max(0, weapon.donatedSlots);
        bool complete = total > 0 && donated >= total;
        double reward = complete ? Balance.GetWeaponReward(total) : 0d;
        string key = BuildWeaponRewardKey(
            context.wing.WingId,
            context.category.CategoryId,
            weapon.weaponName);

        return new MuseumCompletionRewardPreview
        {
            kind = MuseumCompletionRewardKind.Weapon,
            rewardKey = key,
            displayName = weapon.weaponName,
            wingId = context.wing.WingId,
            categoryId = context.category.CategoryId,
            totalSlots = total,
            donatedSlots = donated,
            completed = complete,
            claimed = IsClaimed(key),
            rewardMuseumPoints = reward,
            service = service,
            weapon = weapon,
            category = context.category
        };
    }

    public static MuseumCompletionRewardPreview BuildCategoryPreview(
        MuseumCategoryEntry category,
        MuseumService service)
    {
        MuseumCompletionContext context = FindCategoryContext(category, service);

        if (context == null || category == null)
            return EmptyPreview(MuseumCompletionRewardKind.Category, service);

        int total = Math.Max(0, category.totalSlots);
        int donated = Math.Max(0, category.donatedSlots);
        bool complete = total > 0 && donated >= total;
        double reward = complete
            ? Balance.GetCategoryReward(
                context.wing.WingId,
                category.CategoryId)
            : 0d;
        string key = BuildCategoryRewardKey(
            context.wing.WingId,
            category.CategoryId);

        return new MuseumCompletionRewardPreview
        {
            kind = MuseumCompletionRewardKind.Category,
            rewardKey = key,
            displayName = category.DisplayName,
            wingId = context.wing.WingId,
            categoryId = category.CategoryId,
            totalSlots = total,
            donatedSlots = donated,
            completed = complete,
            claimed = IsClaimed(key),
            rewardMuseumPoints = reward,
            service = service,
            category = category
        };
    }

    public static bool TryClaim(
        MuseumCompletionRewardPreview preview,
        out MuseumCompletionRewardClaimResult result)
    {
        result = new MuseumCompletionRewardClaimResult
        {
            success = false,
            message = "This completion reward is unavailable."
        };

        if (preview == null ||
            preview.service == null ||
            SaveManager.Instance == null ||
            SaveManager.Instance.Museum == null)
        {
            return false;
        }

        MuseumCompletionRewardPreview current = RebuildPreview(preview);

        if (current == null || !current.completed)
        {
            result.message = "This Museum section is not complete yet.";
            return false;
        }

        if (current.claimed || IsClaimed(current.rewardKey))
        {
            result.message = "This completion reward has already been claimed.";
            return false;
        }

        if (current.rewardMuseumPoints <= 0d)
        {
            result.message =
                "No completion reward is configured for this Museum section.";
            return false;
        }

        MuseumStateSaveData state = SaveManager.Instance.Museum;

        if (state.claimedCompletionRewards == null)
        {
            state.claimedCompletionRewards =
                new List<MuseumCompletionRewardClaimSaveData>();
        }

        double reward = Math.Ceiling(current.rewardMuseumPoints);
        state.museumPoints = Math.Max(0d, state.museumPoints + reward);
        state.claimedCompletionRewards.Add(
            new MuseumCompletionRewardClaimSaveData
            {
                rewardKey = current.rewardKey,
                rewardKind = current.kind,
                wingId = current.wingId,
                categoryId = current.categoryId,
                displayName = current.displayName,
                museumPointsAwarded = reward,
                claimedUtcTicks = DateTime.UtcNow.Ticks
            });

        SaveManager.Instance.MarkDirty();
        SaveManager.Instance.SaveGame();

        result = new MuseumCompletionRewardClaimResult
        {
            success = true,
            rewardKey = current.rewardKey,
            kind = current.kind,
            museumPointsAwarded = reward,
            totalMuseumPoints = state.museumPoints,
            message =
                $"{reward:N0} Museum Points claimed from " +
                $"{current.displayName} completion."
        };

        return true;
    }

    public static bool IsClaimed(string rewardKey)
    {
        if (string.IsNullOrWhiteSpace(rewardKey) ||
            SaveManager.Instance == null ||
            SaveManager.Instance.Museum == null ||
            SaveManager.Instance.Museum.claimedCompletionRewards == null)
        {
            return false;
        }

        List<MuseumCompletionRewardClaimSaveData> claims =
            SaveManager.Instance.Museum.claimedCompletionRewards;

        for (int i = 0; i < claims.Count; i++)
        {
            MuseumCompletionRewardClaimSaveData claim = claims[i];

            if (claim != null &&
                string.Equals(
                    claim.rewardKey,
                    rewardKey,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static MuseumCompletionRewardPreview RebuildPreview(
        MuseumCompletionRewardPreview preview)
    {
        switch (preview.kind)
        {
            case MuseumCompletionRewardKind.Skin:
                return BuildSkinPreview(preview.skin, preview.service);
            case MuseumCompletionRewardKind.Weapon:
                return BuildWeaponPreview(preview.weapon, preview.service);
            case MuseumCompletionRewardKind.Category:
                return BuildCategoryPreview(preview.category, preview.service);
            default:
                return null;
        }
    }

    private static double GetHighestActualDonationPoints(
        MuseumSkinEntry skin,
        MuseumService service)
    {
        if (skin == null || skin.slots == null || service == null)
            return 0d;

        double highest = 0d;

        for (int i = 0; i < skin.slots.Count; i++)
        {
            MuseumSlotEntry slot = skin.slots[i];

            if (slot == null || string.IsNullOrWhiteSpace(slot.donationKey))
                continue;

            MuseumDonationRecordSaveData record =
                service.GetDonationRecord(slot.donationKey);

            if (record == null || record.donatedCount <= 0)
                continue;

            double actualPerDonation =
                Math.Max(0d, record.totalMuseumPointsAwarded) /
                Math.Max(1, record.donatedCount);
            highest = Math.Max(highest, actualPerDonation);
        }

        return highest;
    }

    private static MuseumCompletionContext FindSkinContext(
        MuseumSkinEntry target,
        MuseumService service)
    {
        if (target == null)
            return null;

        return FindContext(
            service,
            context => ReferenceEquals(context.skin, target));
    }

    private static MuseumCompletionContext FindWeaponContext(
        MuseumWeaponEntry target,
        MuseumService service)
    {
        if (target == null)
            return null;

        return FindContext(
            service,
            context => ReferenceEquals(context.weapon, target));
    }

    private static MuseumCompletionContext FindCategoryContext(
        MuseumCategoryEntry target,
        MuseumService service)
    {
        if (target == null)
            return null;

        return FindContext(
            service,
            context => ReferenceEquals(context.category, target));
    }

    private static MuseumCompletionContext FindContext(
        MuseumService service,
        Func<MuseumCompletionContext, bool> predicate)
    {
        if (service == null || predicate == null)
            return null;

        MuseumCatalogSnapshot catalog = service.GetCatalogSnapshot(false);

        if (catalog == null || catalog.wings == null)
            return null;

        for (int wingIndex = 0; wingIndex < catalog.wings.Count; wingIndex++)
        {
            MuseumWingEntry wing = catalog.wings[wingIndex];

            if (wing == null || wing.categories == null)
                continue;

            for (int categoryIndex = 0;
                 categoryIndex < wing.categories.Count;
                 categoryIndex++)
            {
                MuseumCategoryEntry category = wing.categories[categoryIndex];

                if (category == null)
                    continue;

                MuseumCompletionContext categoryContext =
                    new MuseumCompletionContext
                    {
                        wing = wing,
                        category = category
                    };

                if (predicate(categoryContext))
                    return categoryContext;

                if (category.weapons == null)
                    continue;

                for (int weaponIndex = 0;
                     weaponIndex < category.weapons.Count;
                     weaponIndex++)
                {
                    MuseumWeaponEntry weapon = category.weapons[weaponIndex];

                    if (weapon == null)
                        continue;

                    MuseumCompletionContext weaponContext =
                        new MuseumCompletionContext
                        {
                            wing = wing,
                            category = category,
                            weapon = weapon
                        };

                    if (predicate(weaponContext))
                        return weaponContext;

                    if (weapon.skins == null)
                        continue;

                    for (int skinIndex = 0;
                         skinIndex < weapon.skins.Count;
                         skinIndex++)
                    {
                        MuseumSkinEntry skin = weapon.skins[skinIndex];
                        MuseumCompletionContext skinContext =
                            new MuseumCompletionContext
                            {
                                wing = wing,
                                category = category,
                                weapon = weapon,
                                skin = skin
                            };

                        if (predicate(skinContext))
                            return skinContext;
                    }
                }
            }
        }

        return null;
    }

    private static MuseumCompletionRewardPreview EmptyPreview(
        MuseumCompletionRewardKind kind,
        MuseumService service)
    {
        return new MuseumCompletionRewardPreview
        {
            kind = kind,
            service = service
        };
    }

    private static string BuildSkinRewardKey(
        string wingId,
        string skinApiId)
    {
        return string.Concat(
            RewardKeyVersion,
            "|skin|wing:", Escape(wingId),
            "|skin:", Escape(skinApiId));
    }

    private static string BuildWeaponRewardKey(
        string wingId,
        string categoryId,
        string weaponName)
    {
        return string.Concat(
            RewardKeyVersion,
            "|weapon|wing:", Escape(wingId),
            "|category:", Escape(categoryId),
            "|weapon:", Escape(weaponName));
    }

    private static string BuildCategoryRewardKey(
        string wingId,
        string categoryId)
    {
        return string.Concat(
            RewardKeyVersion,
            "|category|wing:", Escape(wingId),
            "|category:", Escape(categoryId));
    }

    private static string Escape(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim()
                .Replace("%", "%25")
                .Replace("|", "%7C")
                .Replace(":", "%3A");
    }

    private sealed class MuseumCompletionContext
    {
        public MuseumWingEntry wing;
        public MuseumCategoryEntry category;
        public MuseumWeaponEntry weapon;
        public MuseumSkinEntry skin;
    }
}
