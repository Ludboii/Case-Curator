using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StickerRarityChance
{
    public StickerRarity rarity = StickerRarity.HighGrade;
    [Min(0f)] public float chance;
}

[CreateAssetMenu(
    fileName = "NewCase",
    menuName = "Case Catcher/Case Data")]
public class CaseData : ScriptableObject
{
    [Header("Identity")]
    public string apiId;
    public string caseName;
    public Sprite icon;

    [Header("Container Type")]
    public CaseContainerType containerType = CaseContainerType.WeaponCase;

    [Header("Container Rules")]
    public bool allowRareSpecialItem = true;
    public bool allowStatTrak = true;
    public bool forceSouvenirDrops = false;

    [Header("Shop")]
    public float priceInGold;
    public bool isPremium;
    public CaseQuality quality;
    public PlayerRank requiredRank;
    public int xpRewardOnOpen = 10;
    public CaseShopCategory shopCategory = CaseShopCategory.Cases;

    [Header("Custom Case Settings")]
    public bool isCustomCase;
    public bool shouldHaveRareSpecial = true;

    [Header("Opening Data")]
    public List<RarityChance> rarityChances = new List<RarityChance>();

    [Tooltip(
        "Dedicated Sticker Capsule rarity table. This keeps High Grade, " +
        "Remarkable, Exotic, Extraordinary and Contraband distinct even though " +
        "StickerData uses compatibility weapon rarities elsewhere.")]
    public List<StickerRarityChance> stickerRarityChances =
        new List<StickerRarityChance>();

    public List<WeightedDrop> dropPool = new List<WeightedDrop>();
}
