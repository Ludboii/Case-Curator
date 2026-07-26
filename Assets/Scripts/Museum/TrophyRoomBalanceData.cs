using System;
using UnityEngine;

[Serializable]
public sealed class TrophyFocusBalance
{
    [Range(0f, 1f)]
    public float maximumBonusFraction = 0.25f;

    [Min(1f)]
    public float halfPowerValue = 500f;

    public double Evaluate(double totalTrophyPower)
    {
        double power = Math.Max(0d, totalTrophyPower);
        double halfPower = Math.Max(1d, halfPowerValue);
        double maximum = Math.Max(0d, maximumBonusFraction);

        return maximum * power / (power + halfPower);
    }
}

[CreateAssetMenu(
    fileName = "TrophyRoomBalance",
    menuName = "Case Curator/Museum/Trophy Room Balance")]
public sealed class TrophyRoomBalanceData : ScriptableObject
{
    [Header("Power Weights")]
    [Min(0f)] public float rarityWeight = 15f;
    [Min(0f)] public float marketValueWeight = 35f;
    [Min(0f)] public float variantWeight = 15f;
    [Min(0f)] public float floatWeight = 35f;

    [Header("Rarity Scores")]
    [Tooltip("Normalised score before the rarityWeight is applied.")]
    [Range(0f, 1f)] public float consumerRarityScore = 0f;
    [Range(0f, 1f)] public float industrialRarityScore = 0.15f;
    [Range(0f, 1f)] public float milSpecRarityScore = 0.35f;
    [Range(0f, 1f)] public float restrictedRarityScore = 0.55f;
    [Range(0f, 1f)] public float classifiedRarityScore = 0.75f;
    [Range(0f, 1f)] public float covertRarityScore = 0.90f;
    [Range(0f, 1f)] public float rareSpecialRarityScore = 1f;

    [Header("Market Value")]
    [Tooltip(
        "Market value that reaches a full market-value score. Values above this " +
        "remain capped so one extremely expensive item cannot dominate the room.")]
    [Min(1f)] public float marketValueAtFullScore = 10000f;

    [Header("Variant")]
    [Range(0f, 1f)] public float normalVariantScore;
    [Range(0f, 1f)] public float souvenirVariantScore = 0.8f;
    [Range(0f, 1f)] public float statTrakVariantScore = 1f;

    [Header("Float Composition")]
    [Range(0f, 1f)] public float floorGapWeight = 0.70f;
    [Range(0f, 1f)] public float rangePositionWeight = 0.20f;
    [Range(0f, 1f)] public float absoluteFloatWeight = 0.10f;
    [Range(0f, 1f)] public float highFloatStrength = 0.70f;

    [Header("Float Curves")]
    [Tooltip("X = distance above the skin minimum; Y = prestige score.")]
    public AnimationCurve floorGapCurve = new AnimationCurve();

    [Tooltip("X = normalised position above the skin minimum; Y = score.")]
    public AnimationCurve lowRangePositionCurve = new AnimationCurve();

    [Tooltip("X = absolute float value; Y = absolute low-float prestige.")]
    public AnimationCurve absoluteLowFloatCurve = new AnimationCurve();

    [Tooltip("X = distance below the skin maximum; Y = prestige score.")]
    public AnimationCurve ceilingGapCurve = new AnimationCurve();

    [Tooltip("X = normalised distance below the skin maximum; Y = score.")]
    public AnimationCurve highRangePositionCurve = new AnimationCurve();

    [Tooltip("X = absolute float value; Y = absolute high-float prestige.")]
    public AnimationCurve absoluteHighFloatCurve = new AnimationCurve();

    [Header("Pedestal Multipliers")]
    [Min(0f)] public float slotsOneToFiveMultiplier = 1f;
    [Min(0f)] public float slotsSixToTenMultiplier = 1.2f;
    [Min(0f)] public float slotElevenMultiplier = 1.5f;

    [Header("Focus Balance")]
    public TrophyFocusBalance museumGoldIncome = new TrophyFocusBalance();
    public TrophyFocusBalance museumDiamondIncome = new TrophyFocusBalance();
    public TrophyFocusBalance automatedAcquisitions = new TrophyFocusBalance();
    public TrophyFocusBalance giftRetrievals = new TrophyFocusBalance();

    public double GetRarityScore(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Industrial:
                return Math.Max(0d, Math.Min(1d, industrialRarityScore));
            case Rarity.MilSpec:
                return Math.Max(0d, Math.Min(1d, milSpecRarityScore));
            case Rarity.Restricted:
                return Math.Max(0d, Math.Min(1d, restrictedRarityScore));
            case Rarity.Classified:
                return Math.Max(0d, Math.Min(1d, classifiedRarityScore));
            case Rarity.Covert:
                return Math.Max(0d, Math.Min(1d, covertRarityScore));
            case Rarity.RareSpecial:
                return Math.Max(0d, Math.Min(1d, rareSpecialRarityScore));
            default:
                return Math.Max(0d, Math.Min(1d, consumerRarityScore));
        }
    }

    public double GetPedestalMultiplier(int zeroBasedSlotIndex)
    {
        if (zeroBasedSlotIndex < 5)
            return Math.Max(0d, slotsOneToFiveMultiplier);

        if (zeroBasedSlotIndex < 10)
            return Math.Max(0d, slotsSixToTenMultiplier);

        return Math.Max(0d, slotElevenMultiplier);
    }

    public double EvaluateFocusBonus(
        TrophyRoomFocus focus,
        double totalTrophyPower)
    {
        TrophyFocusBalance balance;

        switch (focus)
        {
            case TrophyRoomFocus.MuseumDiamondIncome:
                balance = museumDiamondIncome;
                break;
            case TrophyRoomFocus.AutomatedAcquisitions:
                balance = automatedAcquisitions;
                break;
            case TrophyRoomFocus.GiftRetrievals:
                balance = giftRetrievals;
                break;
            default:
                balance = museumGoldIncome;
                break;
        }

        return balance != null
            ? balance.Evaluate(totalTrophyPower)
            : 0d;
    }

    private void OnValidate()
    {
        rarityWeight = Mathf.Max(0f, rarityWeight);
        marketValueWeight = Mathf.Max(0f, marketValueWeight);
        variantWeight = Mathf.Max(0f, variantWeight);
        floatWeight = Mathf.Max(0f, floatWeight);
        marketValueAtFullScore = Mathf.Max(1f, marketValueAtFullScore);

        floorGapWeight = Mathf.Max(0f, floorGapWeight);
        rangePositionWeight = Mathf.Max(0f, rangePositionWeight);
        absoluteFloatWeight = Mathf.Max(0f, absoluteFloatWeight);

        float sum = floorGapWeight + rangePositionWeight + absoluteFloatWeight;

        if (sum <= 0.0001f)
        {
            floorGapWeight = 0.70f;
            rangePositionWeight = 0.20f;
            absoluteFloatWeight = 0.10f;
        }
    }
}