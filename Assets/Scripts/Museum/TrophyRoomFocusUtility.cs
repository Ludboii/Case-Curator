/// <summary>
/// Read-only integration surface for systems outside the Museum. Automated
/// Acquisitions and Gift Retrievals can consume these duration multipliers when
/// their authoritative timers are implemented.
/// </summary>
public static class TrophyRoomFocusUtility
{
    public static double GetMuseumGoldIncomeMultiplier()
    {
        return TrophyRoomService.Instance != null
            ? TrophyRoomService.Instance.GetMuseumGoldIncomeMultiplier()
            : 1d;
    }

    public static double GetMuseumDiamondIncomeMultiplier()
    {
        return TrophyRoomService.Instance != null
            ? TrophyRoomService.Instance.GetMuseumDiamondIncomeMultiplier()
            : 1d;
    }

    public static double GetAutomatedAcquisitionDurationMultiplier()
    {
        return TrophyRoomService.Instance != null
            ? TrophyRoomService.Instance
                .GetAutomatedAcquisitionDurationMultiplier()
            : 1d;
    }

    public static double GetGiftRetrievalCooldownMultiplier()
    {
        return TrophyRoomService.Instance != null
            ? TrophyRoomService.Instance.GetGiftRetrievalCooldownMultiplier()
            : 1d;
    }
}
