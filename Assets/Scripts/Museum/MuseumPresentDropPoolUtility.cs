using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared default weighting rules used by the runtime fallback and the editor
/// generator. Generated/configured pools remain fully editable per tier.
/// </summary>
public static class MuseumPresentDropPoolUtility
{
    public static float GetDefaultWeight(
        MuseumPresentTier presentTier,
        CaseQuality quality)
    {
        switch (presentTier)
        {
            case MuseumPresentTier.Dusty:
                return WeightForQuality(
                    quality,
                    CaseQuality.Consumer, 6f,
                    CaseQuality.Industrial, 3f,
                    CaseQuality.MilSpec, 1f);

            case MuseumPresentTier.Bronze:
                return WeightForQuality(
                    quality,
                    CaseQuality.Industrial, 5f,
                    CaseQuality.MilSpec, 3f,
                    CaseQuality.Restricted, 1f);

            case MuseumPresentTier.Silver:
                return WeightForQuality(
                    quality,
                    CaseQuality.MilSpec, 5f,
                    CaseQuality.Restricted, 3f,
                    CaseQuality.Classified, 1f);

            case MuseumPresentTier.Gold:
                return WeightForQuality(
                    quality,
                    CaseQuality.Restricted, 5f,
                    CaseQuality.Classified, 3f,
                    CaseQuality.Covert, 1f);

            case MuseumPresentTier.Diamond:
                return WeightForQuality(
                    quality,
                    CaseQuality.Classified, 5f,
                    CaseQuality.Covert, 3f,
                    CaseQuality.Gold, 1f);

            case MuseumPresentTier.GlobalElite:
                return WeightForQuality(
                    quality,
                    CaseQuality.Classified, 1f,
                    CaseQuality.Covert, 4f,
                    CaseQuality.Gold, 2f);

            default:
                return 0f;
        }
    }

    public static List<MuseumPresentContainerDrop> BuildDefaultPool(
        MuseumPresentTier tier,
        IList<CaseData> allContainers)
    {
        List<MuseumPresentContainerDrop> result =
            new List<MuseumPresentContainerDrop>();

        if (allContainers == null)
            return result;

        for (int i = 0; i < allContainers.Count; i++)
        {
            CaseData container = allContainers[i];

            if (container == null || string.IsNullOrWhiteSpace(container.apiId))
                continue;

            float weight = GetDefaultWeight(tier, container.quality);

            if (weight <= 0f)
                continue;

            result.Add(new MuseumPresentContainerDrop
            {
                container = container,
                weight = weight,
                minimumAmount = 1,
                maximumAmount = 1
            });
        }

        return result;
    }

    public static MuseumPresentContainerDrop Roll(
        IList<MuseumPresentContainerDrop> drops)
    {
        if (drops == null || drops.Count == 0)
            return null;

        float totalWeight = 0f;

        for (int i = 0; i < drops.Count; i++)
        {
            MuseumPresentContainerDrop drop = drops[i];

            if (drop != null && drop.IsValid)
                totalWeight += drop.weight;
        }

        if (totalWeight <= 0f)
            return null;

        float roll = Random.value * totalWeight;
        float running = 0f;
        MuseumPresentContainerDrop lastValid = null;

        for (int i = 0; i < drops.Count; i++)
        {
            MuseumPresentContainerDrop drop = drops[i];

            if (drop == null || !drop.IsValid)
                continue;

            lastValid = drop;
            running += drop.weight;

            if (roll <= running)
                return drop;
        }

        return lastValid;
    }

    private static float WeightForQuality(
        CaseQuality actual,
        CaseQuality first,
        float firstWeight,
        CaseQuality second,
        float secondWeight,
        CaseQuality third,
        float thirdWeight)
    {
        if (actual == first)
            return firstWeight;
        if (actual == second)
            return secondWeight;
        if (actual == third)
            return thirdWeight;

        return 0f;
    }
}
