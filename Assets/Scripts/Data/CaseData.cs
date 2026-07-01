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

[Header("Shop")]
public float priceInGold;
public bool isPremium;
public CaseQuality quality;
public PlayerRank requiredRank;
public int xpRewardOnOpen = 10; // will later be price-based or quality-based
public CaseShopCategory shopCategory = CaseShopCategory.Cases;

    [Header("Custom Case Settings")]
    public bool isCustomCase;
    public bool shouldHaveRareSpecial = true;

    [Header("Opening Data")]
    public List<RarityChance> rarityChances = new List<RarityChance>();
    public List<WeightedDrop> dropPool = new List<WeightedDrop>();
}