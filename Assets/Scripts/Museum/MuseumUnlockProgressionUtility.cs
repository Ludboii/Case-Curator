using System;
using System.Collections.Generic;

/// <summary>
/// Authoritative progression rules for Museum navigation and donation access.
/// Normal weapons unlock by player rank. Rare Special families additionally
/// require their corresponding staged Rare Vault upgrade.
/// </summary>
public static class MuseumUnlockProgressionUtility
{
    public const string ArsenalWingId = "museum-arsenal";
    public const string RareSpecialVaultWingId =
        "museum-rare-special-vault";

    public const int RareVaultStageCount = 6;

    private const string RareVaultUpgradePrefix =
        "museum-rare-vault-stage-";

    private static readonly Dictionary<string, string>
        requiredRankNameByWeapon = BuildWeaponRankLookup();

    private static readonly Dictionary<string, PlayerRank>
        rankByNormalizedName = BuildRankLookup();

    public static bool IsWingUnlocked(
        MuseumWingEntry wing,
        GameDatabase database,
        out string lockedReason)
    {
        lockedReason = "";

        if (wing == null)
        {
            lockedReason = "Museum wing data is unavailable.";
            return false;
        }

        MuseumWingConfig config = wing.config;

        if (config != null &&
            config.unlockDefinition != null &&
            !UnlockEvaluator.IsUnlocked(config.unlockDefinition))
        {
            lockedReason = IsRareSpecialVault(config)
                ? "Unlocks at Legendary Eagle Master."
                : "Complete this wing's unlock requirements.";
            return false;
        }

        if (config != null && IsRareSpecialVault(config))
        {
            return MeetsRank(
                "Legendary Eagle Master",
                out lockedReason);
        }

        return true;
    }

    public static bool IsCategoryUnlocked(
        MuseumCategoryEntry category,
        GameDatabase database,
        out string lockedReason)
    {
        lockedReason = "";

        if (category == null)
        {
            lockedReason = "Museum category data is unavailable.";
            return false;
        }

        MuseumCategoryConfig config = category.config;

        if (config != null &&
            config.unlockDefinition != null &&
            !UnlockEvaluator.IsUnlocked(config.unlockDefinition))
        {
            lockedReason = "Complete this category's unlock requirements.";
            return false;
        }

        if (category.weapons == null || category.weapons.Count == 0)
        {
            lockedReason = "No Museum exhibits are configured for this category.";
            return false;
        }

        string earliestReason = "";
        int earliestOrder = int.MaxValue;

        for (int i = 0; i < category.weapons.Count; i++)
        {
            MuseumWeaponEntry weapon = category.weapons[i];

            if (IsWeaponUnlocked(weapon, database, out string reason))
                return true;

            int order = GetWeaponUnlockOrder(weapon);

            if (order < earliestOrder)
            {
                earliestOrder = order;
                earliestReason = reason;
            }
        }

        lockedReason = !string.IsNullOrWhiteSpace(earliestReason)
            ? earliestReason
            : "This category has not been unlocked yet.";
        return false;
    }

    public static bool IsWeaponUnlocked(
        MuseumWeaponEntry weapon,
        GameDatabase database,
        out string lockedReason)
    {
        lockedReason = "";

        if (weapon == null)
        {
            lockedReason = "Museum weapon data is unavailable.";
            return false;
        }

        bool rareSpecial = IsRareSpecialWeapon(weapon);
        return IsWeaponNameUnlocked(
            weapon.weaponName,
            rareSpecial,
            database,
            out lockedReason);
    }

    public static bool IsSkinUnlocked(
        SkinData skin,
        GameDatabase database,
        out string lockedReason)
    {
        lockedReason = "";

        if (skin == null)
        {
            lockedReason = "Skin data is unavailable.";
            return false;
        }

        bool rareSpecial =
            skin.rarity == Rarity.RareSpecial ||
            GetRareVaultStage(skin.weaponName) > 0;

        return IsWeaponNameUnlocked(
            skin.weaponName,
            rareSpecial,
            database,
            out lockedReason);
    }

    public static bool IsSlotUnlocked(
        MuseumSlotEntry slot,
        GameDatabase database,
        out string lockedReason)
    {
        return IsSkinUnlocked(
            slot != null ? slot.skin : null,
            database,
            out lockedReason);
    }

    public static int CompareWeaponsForDisplay(
        MuseumWeaponEntry left,
        MuseumWeaponEntry right)
    {
        int orderCompare = GetWeaponUnlockOrder(left)
            .CompareTo(GetWeaponUnlockOrder(right));

        if (orderCompare != 0)
            return orderCompare;

        return string.Compare(
            left != null ? left.weaponName : "",
            right != null ? right.weaponName : "",
            StringComparison.OrdinalIgnoreCase);
    }

    public static int GetRareVaultStage(string weaponName)
    {
        string key = Normalize(weaponName);

        if (string.IsNullOrWhiteSpace(key))
            return 0;

        // Check the icon families before generic terms such as Bayonet.
        if (ContainsAny(key, "butterfly", "karambit", "m9bayonet"))
            return 6;

        if (ContainsAny(
                key,
                "specialistglove",
                "sportglove",
                "motoglove",
                "driverglove"))
        {
            return 5;
        }

        if (ContainsAny(
                key,
                "bayonet",
                "talon",
                "skeleton",
                "stiletto"))
        {
            return 4;
        }

        if (ContainsAny(
                key,
                "kukri",
                "ursus",
                "flipknife",
                "huntsman",
                "nomad",
                "bowie",
                "classicknife"))
        {
            return 3;
        }

        if (ContainsAny(
                key,
                "handwrap",
                "brokenfang",
                "bloodhound",
                "hydraglove"))
        {
            return 2;
        }

        if (ContainsAny(
                key,
                "navaja",
                "shadowdagger",
                "gutknife",
                "falchion",
                "survivalknife",
                "paracord"))
        {
            return 1;
        }

        return 0;
    }

    public static string GetRareVaultStageUpgradeId(int stage)
    {
        int safeStage = Math.Max(1, Math.Min(RareVaultStageCount, stage));
        return RareVaultUpgradePrefix + safeStage.ToString("00");
    }

    public static string GetRequiredRankDisplayName(string weaponName)
    {
        string normalized = Normalize(weaponName);

        if (requiredRankNameByWeapon.TryGetValue(
                normalized,
                out string rankName))
        {
            return rankName;
        }

        // Future normal weapon additions remain inaccessible until the final
        // normal Arsenal rank rather than becoming available accidentally.
        return "Legendary Eagle";
    }

    private static bool IsWeaponNameUnlocked(
        string weaponName,
        bool rareSpecial,
        GameDatabase database,
        out string lockedReason)
    {
        lockedReason = "";

        if (rareSpecial)
        {
            if (!MeetsRank(
                    "Legendary Eagle Master",
                    out lockedReason))
            {
                return false;
            }

            int stage = GetRareVaultStage(weaponName);

            if (stage <= 0)
            {
                lockedReason =
                    "This Rare Special family has not been assigned to a " +
                    "Rare Vault stage yet.";
                return false;
            }

            if (!IsRareVaultStagePurchased(database, stage))
            {
                lockedReason =
                    $"Purchase Rare Vault Stage {stage} in Upgrades.";
                return false;
            }

            return true;
        }

        string requiredRank = GetRequiredRankDisplayName(weaponName);
        return MeetsRank(requiredRank, out lockedReason);
    }

    private static bool MeetsRank(
        string requiredRankName,
        out string lockedReason)
    {
        lockedReason = "";

        if (!TryResolveRank(requiredRankName, out PlayerRank requiredRank))
        {
            lockedReason =
                $"Required rank '{requiredRankName}' is not configured.";
            return false;
        }

        PlayerRank currentRank = GetCurrentRank();

        if ((int)currentRank >= (int)requiredRank)
            return true;

        lockedReason = $"Unlocks at {requiredRankName}.";
        return false;
    }

    private static bool IsRareVaultStagePurchased(
        GameDatabase database,
        int stage)
    {
        if (database == null ||
            database.upgradeCatalog == null ||
            SaveManager.Instance == null ||
            SaveManager.Instance.Upgrades == null)
        {
            return false;
        }

        string upgradeId = GetRareVaultStageUpgradeId(stage);
        UpgradeData upgrade =
            database.upgradeCatalog.GetUpgradeById(upgradeId);

        if (upgrade == null)
            return false;

        return UpgradeSaveUtility.GetLevel(
            SaveManager.Instance.Upgrades,
            upgradeId) > 0;
    }

    private static bool IsRareSpecialWeapon(MuseumWeaponEntry weapon)
    {
        if (weapon == null)
            return false;

        if (GetRareVaultStage(weapon.weaponName) > 0)
            return true;

        if (weapon.skins == null)
            return false;

        for (int i = 0; i < weapon.skins.Count; i++)
        {
            SkinData skin = weapon.skins[i] != null
                ? weapon.skins[i].skin
                : null;

            if (skin != null && skin.rarity == Rarity.RareSpecial)
                return true;
        }

        return false;
    }

    private static bool IsRareSpecialVault(MuseumWingConfig config)
    {
        if (config == null)
            return false;

        string id = Normalize(config.wingId);
        string name = Normalize(config.DisplayName);

        return id == Normalize(RareSpecialVaultWingId) ||
               name.Contains("rarespecialvault") ||
               name.Contains("rarevault");
    }

    private static int GetWeaponUnlockOrder(MuseumWeaponEntry weapon)
    {
        if (weapon == null)
            return int.MaxValue;

        if (IsRareSpecialWeapon(weapon))
            return 100000 + Math.Max(1, GetRareVaultStage(weapon.weaponName));

        string rankName = GetRequiredRankDisplayName(weapon.weaponName);

        return TryResolveRank(rankName, out PlayerRank rank)
            ? (int)rank
            : int.MaxValue - 1;
    }

    private static PlayerRank GetCurrentRank()
    {
        if (SaveManager.Instance != null)
            return SaveManager.Instance.CurrentRank;

        Array values = Enum.GetValues(typeof(PlayerRank));
        PlayerRank result = default;
        int lowest = int.MaxValue;

        foreach (object value in values)
        {
            PlayerRank rank = (PlayerRank)value;

            if ((int)rank < lowest)
            {
                lowest = (int)rank;
                result = rank;
            }
        }

        return result;
    }

    private static bool TryResolveRank(
        string rankName,
        out PlayerRank rank)
    {
        return rankByNormalizedName.TryGetValue(
            Normalize(rankName),
            out rank);
    }

    private static Dictionary<string, PlayerRank> BuildRankLookup()
    {
        Dictionary<string, PlayerRank> result =
            new Dictionary<string, PlayerRank>(StringComparer.OrdinalIgnoreCase);

        Array values = Enum.GetValues(typeof(PlayerRank));

        foreach (object value in values)
        {
            PlayerRank rank = (PlayerRank)value;
            AddRankAlias(result, rank, rank.ToString());
            AddRankAlias(
                result,
                rank,
                PlayerProgressUtility.GetRankDisplayName(rank));
        }

        return result;
    }

    private static void AddRankAlias(
        Dictionary<string, PlayerRank> lookup,
        PlayerRank rank,
        string alias)
    {
        string normalized = Normalize(alias);

        if (!string.IsNullOrWhiteSpace(normalized))
            lookup[normalized] = rank;
    }

    private static Dictionary<string, string> BuildWeaponRankLookup()
    {
        Dictionary<string, string> result =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        AddWeapons(result, "Silver I", "USP-S", "Glock-18");
        AddWeapons(result, "Silver II", "P2000", "P250", "CZ75-Auto");
        AddWeapons(
            result,
            "Silver III",
            "Dual Berettas",
            "Five-SeveN",
            "Tec-9");
        AddWeapons(
            result,
            "Silver Elite",
            "Desert Eagle",
            "R8 Revolver",
            "Zeus x27");
        AddWeapons(
            result,
            "Silver Elite Master",
            "MP9",
            "MAC-10",
            "UMP-45");
        AddWeapons(
            result,
            "Gold Nova I",
            "PP-Bizon",
            "MP7",
            "MP5-SD");
        AddWeapons(
            result,
            "Gold Nova II",
            "P90",
            "Negev",
            "M249");
        AddWeapons(
            result,
            "Gold Nova III",
            "XM1014",
            "Sawed-Off");
        AddWeapons(result, "Gold Nova Master", "MAG-7", "Nova");
        AddWeapons(result, "Master Guardian I", "Galil AR", "FAMAS");
        AddWeapons(result, "Master Guardian II", "SG 553", "AUG");
        AddWeapons(
            result,
            "Master Guardian Elite",
            "G3SG1",
            "SCAR-20");
        AddWeapons(
            result,
            "Distinguished Master Guardian",
            "SSG 08",
            "AWP");
        AddWeapons(
            result,
            "Legendary Eagle",
            "M4A4",
            "M4A1-S",
            "AK-47");

        return result;
    }

    private static void AddWeapons(
        Dictionary<string, string> lookup,
        string rankName,
        params string[] weaponNames)
    {
        if (weaponNames == null)
            return;

        for (int i = 0; i < weaponNames.Length; i++)
            lookup[Normalize(weaponNames[i])] = rankName;
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        if (string.IsNullOrWhiteSpace(value) || terms == null)
            return false;

        for (int i = 0; i < terms.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(terms[i]) &&
                value.Contains(terms[i]))
            {
                return true;
            }
        }

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
