using UnityEngine;

/// <summary>
/// Stable IDs and runtime effect readers for Automated Acquisitions upgrades.
/// Level-zero values are supplied by the generated UpgradeData definitions.
/// </summary>
public static class AutoAcquisitionUpgradeUtility
{
    public const string ProcessingSpeedId =
        "auto-acq-processing-speed";
    public const string CalibrationId =
        "auto-acq-machine-calibration";
    public const string FloatCalibrationId =
        "auto-acq-float-calibration";
    public const string IntakeCapacityId =
        "auto-acq-intake-vault";
    public const string ProcessingLinesId =
        "auto-acq-processing-lines";
    public const string ProcurementBudgetId =
        "auto-acq-procurement-budget";
    public const string OfflineShiftId =
        "auto-acq-offline-shift";
    public const string CuratorAlertId =
        "auto-acq-curator-alert";

    public static float GetBaseProcessingSeconds()
    {
        return Mathf.Max(1f, GetEffect(ProcessingSpeedId, 600f));
    }

    public static float GetCalibrationMultiplier()
    {
        return Mathf.Clamp(GetEffect(CalibrationId, 0.80f), 0.80f, 1f);
    }

    /// <summary>
    /// Exponent applied to a uniform 0-1 float roll. Values below 1 bias the
    /// result towards the high/worse end. Values above 1 bias towards better
    /// floats. An exponent of 1 matches manual opening.
    /// </summary>
    public static float GetFloatCalibrationExponent()
    {
        return Mathf.Clamp(GetEffect(FloatCalibrationId, 0.60f), 0.1f, 1.15f);
    }

    public static float GetExpectedNormalisedFloat()
    {
        float exponent = GetFloatCalibrationExponent();
        return 1f / (exponent + 1f);
    }

    public static int GetIntakeCapacity()
    {
        return Mathf.Max(1, Mathf.RoundToInt(GetEffect(IntakeCapacityId, 10f)));
    }

    public static int GetProcessingLineCount()
    {
        return Mathf.Clamp(
            Mathf.RoundToInt(GetEffect(ProcessingLinesId, 1f)),
            1,
            3);
    }

    public static float GetMaximumBudgetPerLine()
    {
        return Mathf.Max(0f, GetEffect(ProcurementBudgetId, 5000f));
    }

    public static double GetOfflineShiftHours()
    {
        return Mathf.Max(0f, GetEffect(OfflineShiftId, 1f));
    }

    public static int GetCuratorAlertLevel()
    {
        return Mathf.Clamp(
            Mathf.RoundToInt(GetEffect(CuratorAlertId, 0f)),
            0,
            4);
    }

    private static float GetEffect(string upgradeId, float fallback)
    {
        UpgradeService service = UpgradeService.Instance != null
            ? UpgradeService.Instance
            : Object.FindFirstObjectByType<UpgradeService>();

        UpgradeData upgrade = service != null
            ? service.GetUpgrade(upgradeId)
            : null;

        if (upgrade == null)
            return fallback;

        int level = service.GetLevel(upgrade);
        return upgrade.GetEffectValue(level);
    }
}
