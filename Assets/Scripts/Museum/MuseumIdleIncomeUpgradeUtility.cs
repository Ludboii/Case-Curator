using System;

/// <summary>
/// Stable IDs and read-only effect lookup for M5.1 Museum idle-income upgrades.
/// Upgrade levels remain owned by UpgradeService and SaveData; the Museum service
/// only reads their current absolute effect values.
/// </summary>
public static class MuseumIdleIncomeUpgradeUtility
{
    public const string GoldIncomeMultiplierId =
        "museum-idle-gold-income-multiplier";

    public const string DiamondIncomeMultiplierId =
        "museum-idle-diamond-income-multiplier";

    public const string OfflineHoursId =
        "museum-idle-offline-hours";

    public const string GoldCapacityId =
        "museum-idle-gold-capacity";

    public const string DiamondCapacityId =
        "museum-idle-diamond-capacity";

    public const string LegacySharedIncomeMultiplierId =
        "museum-idle-income-multiplier";

    public static double GetGoldIncomeMultiplier(GameDatabase database)
    {
        return Math.Max(
            0d,
            GetEffect(database, GoldIncomeMultiplierId, 1d));
    }

    public static double GetDiamondIncomeMultiplier(GameDatabase database)
    {
        return Math.Max(
            0d,
            GetEffect(database, DiamondIncomeMultiplierId, 1d));
    }

    public static double GetOfflineHoursBonus(GameDatabase database)
    {
        return Math.Max(
            0d,
            GetEffect(database, OfflineHoursId, 0d));
    }

    public static double GetGoldCapacityMultiplier(GameDatabase database)
    {
        return Math.Max(
            0d,
            GetEffect(database, GoldCapacityId, 1d));
    }

    public static double GetDiamondCapacityMultiplier(GameDatabase database)
    {
        return Math.Max(
            0d,
            GetEffect(database, DiamondCapacityId, 1d));
    }

    private static double GetEffect(
        GameDatabase database,
        string upgradeId,
        double fallback)
    {
        if (database == null ||
            database.upgradeCatalog == null ||
            SaveManager.Instance == null ||
            SaveManager.Instance.Upgrades == null)
        {
            return fallback;
        }

        UpgradeData upgrade =
            database.upgradeCatalog.GetUpgradeById(upgradeId);

        if (upgrade == null)
            return fallback;

        int level = UpgradeSaveUtility.GetLevel(
            SaveManager.Instance.Upgrades,
            upgradeId);

        double value = upgrade.GetEffectValue(level);

        return double.IsNaN(value) || double.IsInfinity(value)
            ? fallback
            : value;
    }
}
