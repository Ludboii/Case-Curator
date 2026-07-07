using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSkin", menuName = "Case Catcher/Skin Data")]
public class SkinData : ScriptableObject
{
    public string skinName;
    public string weaponName;
    public string collection;
    public string apiId;
    public string paintIndex;
    
    public CollectionData collectionData;
    public Rarity rarity;

    public float minFloat = 0f;
    public float maxFloat = 1f;

    public Sprite icon;
    public bool canBeStatTrak;
    public bool canBeSouvenir;

public PatternType patternType = PatternType.None;

// X axis = fade %, Y axis = price multiplier. Default: 80%→1.10x, 100%→1.60x
public AnimationCurve fadeBonusCurve = AnimationCurve.Linear(80f, 1.10f, 100f, 1.60f);

// Blue-gem style: many pattern IDs grouped per tier, each tier its own multiplier
public List<PatternTierGroup> patternTierGroups = new List<PatternTierGroup>();

// Single ultra-rare IDs with a fixed multiplier (AK #661, Karambit #387)
public List<SpecialPattern> uniquePatterns = new List<SpecialPattern>();

// Formula-based fade — no manual table needed.
// Pattern 0 → fadeRangeStart, Pattern 1000 → fadeRangeEnd (or reversed).
public float fadeRangeStart = 0f;
public float fadeRangeEnd = 100f;
public bool reverseFadePattern = false; // some weapons roll fade in reverse seed order

// Optional manual overrides for specific known patterns (e.g. community-verified exact values).
// Leave empty to rely entirely on the formula above.
public List<FadeEntry> fadeOverrides = new List<FadeEntry>();
    public WearPrices exteriorPrices;
public WearPrices statTrakExteriorPrices;
public WearPrices souvenirExteriorPrices;

public bool isVanilla;
public float vanillaPrice;
public float vanillaStatTrakPrice;


    public enum Wear { FactoryNew, MinimalWear, FieldTested, WellWorn, BattleScarred }
}