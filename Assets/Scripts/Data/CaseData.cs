using System.Collections.Generic;
using UnityEngine;

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
    public List<WeightedDrop> dropPool = new List<WeightedDrop>();
}