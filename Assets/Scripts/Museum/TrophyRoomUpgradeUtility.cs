using System;

public static class TrophyRoomUpgradeUtility
{
    public const int MaximumPedestalCount = 11;

    public static string GetPedestalUpgradeId(int zeroBasedSlotIndex)
    {
        int number = Math.Max(0, zeroBasedSlotIndex) + 1;
        return $"trophy-room-pedestal-{number:00}";
    }

    public static int GetUnlockedSlotCount(GameDatabase database)
    {
        if (database == null ||
            database.upgradeCatalog == null ||
            SaveManager.Instance == null ||
            SaveManager.Instance.Upgrades == null)
        {
            return 0;
        }

        int unlocked = 0;

        for (int i = 0; i < MaximumPedestalCount; i++)
        {
            string upgradeId = GetPedestalUpgradeId(i);
            UpgradeData upgrade = database.upgradeCatalog.GetUpgradeById(upgradeId);

            if (upgrade == null)
                break;

            int level = UpgradeSaveUtility.GetLevel(
                SaveManager.Instance.Upgrades,
                upgradeId);

            if (level <= 0)
                break;

            unlocked = i + 1;
        }

        return unlocked;
    }
}
