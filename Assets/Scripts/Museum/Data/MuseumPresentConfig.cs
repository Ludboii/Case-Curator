using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "MuseumPresentConfig",
    menuName = "Case Curator/Museum/Museum Present Config")]
public class MuseumPresentConfig : ScriptableObject
{
    public List<MuseumPresentTierConfig> tiers =
        new List<MuseumPresentTierConfig>();

    public MuseumPresentTierConfig GetTier(MuseumPresentTier tier)
    {
        if (tiers != null)
        {
            for (int i = 0; i < tiers.Count; i++)
            {
                MuseumPresentTierConfig entry = tiers[i];

                if (entry != null && entry.tier == tier)
                    return entry;
            }
        }

        return CreateFallbackTier(tier);
    }

    public static MuseumPresentTierConfig CreateFallbackTier(
        MuseumPresentTier tier)
    {
        MuseumPresentTierConfig config =
            new MuseumPresentTierConfig
            {
                tier = tier,
                displayName = MuseumPresentUtility.GetTierDisplayName(tier),
                fragmentsPerPresent = 100
            };

        switch (tier)
        {
            case MuseumPresentTier.Dusty:
                config.minimumGold = 25f;
                config.maximumGold = 60f;
                config.minimumXP = 5;
                config.maximumXP = 15;
                break;

            case MuseumPresentTier.Bronze:
                config.minimumGold = 60f;
                config.maximumGold = 140f;
                config.minimumXP = 15;
                config.maximumXP = 35;
                break;

            case MuseumPresentTier.Silver:
                config.minimumGold = 150f;
                config.maximumGold = 350f;
                config.minimumXP = 35;
                config.maximumXP = 80;
                break;

            case MuseumPresentTier.Gold:
                config.minimumGold = 400f;
                config.maximumGold = 900f;
                config.minimumXP = 90;
                config.maximumXP = 180;
                config.minimumDiamonds = 0;
                config.maximumDiamonds = 1;
                break;

            case MuseumPresentTier.Diamond:
                config.minimumGold = 1000f;
                config.maximumGold = 2200f;
                config.minimumXP = 200;
                config.maximumXP = 450;
                config.minimumDiamonds = 1;
                config.maximumDiamonds = 3;
                break;

            case MuseumPresentTier.GlobalElite:
                config.minimumGold = 2500f;
                config.maximumGold = 6000f;
                config.minimumXP = 500;
                config.maximumXP = 1100;
                config.minimumDiamonds = 3;
                config.maximumDiamonds = 7;
                break;
        }

        config.Normalize();
        return config;
    }

    private void OnValidate()
    {
        if (tiers == null)
            tiers = new List<MuseumPresentTierConfig>();

        for (int i = 0; i < tiers.Count; i++)
        {
            if (tiers[i] != null)
                tiers[i].Normalize();
        }
    }
}
