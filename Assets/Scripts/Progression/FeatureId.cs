/// <summary>
/// Stable identifiers for major unlockable game features.
///
/// IMPORTANT: these values can be serialized inside UnlockDefinition assets.
/// Do not renumber or reuse existing values. Append new values instead.
/// </summary>
public enum FeatureId
{
    None = 0,

    Tradeups = 20,

    MuseumLobby = 100,
    MuseumPistolWing = 110,
    MuseumSmgWing = 111,
    MuseumHeavyWing = 112,
    MuseumRifleWing = 113,
    MuseumSniperWing = 114,
    MuseumRareSpecialVault = 120,
    MuseumSouvenirHall = 121,
    MuseumMilestoneStaircase = 130,
    MuseumGiftDesk = 140,
    MuseumTrophyRoom = 150,

    AutomatedAcquisitionsWing = 200,
    WeaponCaseArchive = 210,
    CollectionPackageArchive = 211,
    StickerCapsuleArchive = 212,

    OpeningSlotII = 300,
    OpeningSlotIII = 301,

    DebugPanel = 900,

    // Use a stable UnlockDefinition.unlockId for features that do not yet
    // deserve their own enum entry.
    Custom = 10000
}
