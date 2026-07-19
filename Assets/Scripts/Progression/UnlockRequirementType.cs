/// <summary>
/// Supported predicates for data-driven feature unlocks.
/// </summary>
public enum UnlockRequirementType
{
    PlayerRankAtLeast = 0,
    PlayerXpAtLeast = 1,
    GoldAtLeast = 2,
    DiamondsAtLeast = 3,

    MuseumPointsAtLeast = 10,
    MuseumMilestoneClaimed = 11,
    ClaimedMuseumMilestonesAtLeast = 12,

    CompletedTradeupsAtLeast = 20,

    ContainerCompletionAtLeast = 30,
    OpenablesOpenedAtLeast = 31,

    UpgradeLevelAtLeast = 40,
    FeatureUnlocked = 41,

    OpeningSlotsAtLeast = 50,
    StoragePagesAtLeast = 51
}

/// <summary>
/// Controls whether every requirement or any one requirement must pass.
/// </summary>
public enum UnlockRequirementGroupMode
{
    All = 0,
    Any = 1
}
