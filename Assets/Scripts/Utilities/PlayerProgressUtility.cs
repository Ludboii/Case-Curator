using UnityEngine;

public static class PlayerProgressUtility
{
    // XP required to REACH each rank.
    // IMPORTANT:
    // This array must match the PlayerRank enum order exactly.
    // Values are from Case Catcher GDD 1.3.4, section 10.1 Ranks.
    private static readonly int[] rankXpRequirements =
    {
        0,       // 0  Silver I
        800,     // 1  Silver II
        1600,    // 2  Silver III
        2500,    // 3  Silver Elite
        3500,    // 4  Silver Elite Master

        5000,    // 5  Gold Nova I
        6500,    // 6  Gold Nova II
        8000,    // 7  Gold Nova III
        10000,   // 8  Gold Nova Master

        12500,   // 9  Master Guardian I
        15000,   // 10 Master Guardian II
        18000,   // 11 Master Guardian Elite
        21500,   // 12 Distinguished Master Guardian

        25000,   // 13 Legendary Eagle
        30000,   // 14 Legendary Eagle Master
        38000,   // 15 Supreme Master First Class

        50000,   // 16 Global Elite
        62000,   // 17 Global Elite II
        74000,   // 18 Global Elite III
        86000,   // 19 Global Elite IV
        100000,  // 20 Global Elite V
        120000,  // 21 Global Elite VI
        140000,  // 22 Global Elite VII
        160000,  // 23 Global Elite VIII
        180000,  // 24 Global Elite IX
        200000,  // 25 Global Elite X
        250000   // 26 The Global Elite
    };

    public static PlayerRank GetRankFromXP(int xp)
    {
        PlayerRank currentRank = PlayerRank.SilverI;

        for (int i = 0; i < rankXpRequirements.Length; i++)
        {
            if (xp >= rankXpRequirements[i])
                currentRank = (PlayerRank)i;
            else
                break;
        }

        return currentRank;
    }

    public static int GetCurrentRankXPRequirement(PlayerRank rank)
    {
        int index = Mathf.Clamp((int)rank, 0, rankXpRequirements.Length - 1);
        return rankXpRequirements[index];
    }

    public static int GetNextRankXPRequirement(PlayerRank rank)
    {
        int nextIndex = (int)rank + 1;

        if (nextIndex >= rankXpRequirements.Length)
            return rankXpRequirements[rankXpRequirements.Length - 1];

        return rankXpRequirements[nextIndex];
    }

    public static bool IsMaxRank(PlayerRank rank)
    {
        return (int)rank >= rankXpRequirements.Length - 1;
    }

    public static int GetXPIntoCurrentRank(int totalXP)
    {
        PlayerRank rank = GetRankFromXP(totalXP);
        int currentRankXP = GetCurrentRankXPRequirement(rank);

        return Mathf.Max(0, totalXP - currentRankXP);
    }

    public static int GetXPNeededForNextRank(int totalXP)
    {
        PlayerRank rank = GetRankFromXP(totalXP);

        if (IsMaxRank(rank))
            return 0;

        int currentRankXP = GetCurrentRankXPRequirement(rank);
        int nextRankXP = GetNextRankXPRequirement(rank);

        return nextRankXP - currentRankXP;
    }

    public static float GetRankProgress01(int totalXP)
    {
        PlayerRank rank = GetRankFromXP(totalXP);

        if (IsMaxRank(rank))
            return 1f;

        int xpIntoRank = GetXPIntoCurrentRank(totalXP);
        int xpNeeded = GetXPNeededForNextRank(totalXP);

        if (xpNeeded <= 0)
            return 1f;

        return Mathf.Clamp01(xpIntoRank / (float)xpNeeded);
    }

    public static int GetXPRemainingForNextRank(int totalXP)
    {
        PlayerRank rank = GetRankFromXP(totalXP);

        if (IsMaxRank(rank))
            return 0;

        int nextRankXP = GetNextRankXPRequirement(rank);

        return Mathf.Max(0, nextRankXP - totalXP);
    }

    public static int GetTotalXPRequiredForRank(PlayerRank rank)
    {
        return GetCurrentRankXPRequirement(rank);
    }

    public static int GetMaxRankIndex()
    {
        return rankXpRequirements.Length - 1;
    }

    public static int GetRankCount()
    {
        return rankXpRequirements.Length;
    }

    public static string GetRankDisplayName(PlayerRank rank)
    {
        switch (rank)
        {
            case PlayerRank.SilverI: return "Silver I";
            case PlayerRank.SilverII: return "Silver II";
            case PlayerRank.SilverIII: return "Silver III";
            case PlayerRank.SilverElite: return "Silver Elite";
            case PlayerRank.SilverEliteMaster: return "Silver Elite Master";

            case PlayerRank.GoldNovaI: return "Gold Nova I";
            case PlayerRank.GoldNovaII: return "Gold Nova II";
            case PlayerRank.GoldNovaIII: return "Gold Nova III";
            case PlayerRank.GoldNovaMaster: return "Gold Nova Master";

            case PlayerRank.MasterGuardianI: return "Master Guardian I";
            case PlayerRank.MasterGuardianII: return "Master Guardian II";
            case PlayerRank.MasterGuardianElite: return "Master Guardian Elite";
            case PlayerRank.DistinguishedMasterGuardian: return "Distinguished Master Guardian";

            case PlayerRank.LegendaryEagle: return "Legendary Eagle";
            case PlayerRank.LegendaryEagleMaster: return "Legendary Eagle Master";
            case PlayerRank.SupremeMasterFirstClass: return "Supreme Master First Class";

            case PlayerRank.GlobalElite: return "Global Elite";
            case PlayerRank.GlobalEliteII: return "Global Elite II";
            case PlayerRank.GlobalEliteIII: return "Global Elite III";
            case PlayerRank.GlobalEliteIV: return "Global Elite IV";
            case PlayerRank.GlobalEliteV: return "Global Elite V";
            case PlayerRank.GlobalEliteVI: return "Global Elite VI";
            case PlayerRank.GlobalEliteVII: return "Global Elite VII";
            case PlayerRank.GlobalEliteVIII: return "Global Elite VIII";
            case PlayerRank.GlobalEliteIX: return "Global Elite IX";
            case PlayerRank.GlobalEliteX: return "Global Elite X";

            case PlayerRank.TheGlobalElite: return "The Global Elite";

            default: return rank.ToString();
        }
    }

    // This is not the actual owned slot count.
    // This only answers: "How many opening slots is the player allowed to unlock by rank?"
    // Your GDD says:
    // - Start with 1 opening slot.
    // - Silver Elite Master makes the 2nd slot upgrade available.
    // - Gold Nova Master makes the 3rd slot upgrade available.
    public static int GetMaxOpeningSlotsAllowedByRank(PlayerRank rank)
    {
        if (rank >= PlayerRank.GoldNovaMaster)
            return 3;

        if (rank >= PlayerRank.SilverEliteMaster)
            return 2;

        return 1;
    }

    public static bool CanUnlockSecondOpeningSlot(PlayerRank rank)
    {
        return rank >= PlayerRank.SilverEliteMaster;
    }

    public static bool CanUnlockThirdOpeningSlot(PlayerRank rank)
    {
        return rank >= PlayerRank.GoldNovaMaster;
    }
}