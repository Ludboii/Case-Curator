using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dedicated Sticker Capsule roll path. It preserves all five sticker-only
/// rarities instead of collapsing Extraordinary and Contraband into the weapon
/// skin rarity compatibility mapping used by generic inventory UI.
/// </summary>
public static class StickerCapsuleRollUtility
{
    public static StickerData Roll(CaseData capsule)
    {
        if (capsule == null ||
            capsule.containerType != CaseContainerType.StickerCapsule ||
            capsule.dropPool == null ||
            capsule.dropPool.Count == 0)
        {
            return null;
        }

        Dictionary<StickerRarity, List<WeightedDrop>> groups =
            BuildGroups(capsule);

        if (groups.Count == 0)
            return null;

        StickerRarity rarity = RollRarity(capsule, groups);

        if (!groups.TryGetValue(rarity, out List<WeightedDrop> drops) ||
            drops == null || drops.Count == 0)
        {
            foreach (List<WeightedDrop> fallback in groups.Values)
                return RollWeighted(fallback);

            return null;
        }

        return RollWeighted(drops);
    }

    public static void EnsureDefaultRarityTable(CaseData capsule)
    {
        if (capsule == null ||
            capsule.containerType != CaseContainerType.StickerCapsule)
        {
            return;
        }

        Dictionary<StickerRarity, List<WeightedDrop>> groups =
            BuildGroups(capsule);
        List<StickerRarity> present =
            new List<StickerRarity>(groups.Keys);
        present.Sort((a, b) => ((int)a).CompareTo((int)b));

        if (capsule.stickerRarityChances == null)
        {
            capsule.stickerRarityChances =
                new List<StickerRarityChance>();
        }

        HashSet<StickerRarity> configured = new HashSet<StickerRarity>();

        for (int i = capsule.stickerRarityChances.Count - 1; i >= 0; i--)
        {
            StickerRarityChance chance = capsule.stickerRarityChances[i];

            if (chance == null ||
                !groups.ContainsKey(chance.rarity) ||
                !configured.Add(chance.rarity))
            {
                capsule.stickerRarityChances.RemoveAt(i);
            }
        }

        if (capsule.stickerRarityChances.Count == present.Count &&
            HasPositiveConfiguredChance(capsule, groups))
        {
            capsule.stickerRarityChances.Sort((a, b) =>
                ((int)a.rarity).CompareTo((int)b.rarity));
            return;
        }

        float[] defaults = GetDefaultTierChances(present.Count);
        capsule.stickerRarityChances.Clear();

        for (int i = 0; i < present.Count; i++)
        {
            capsule.stickerRarityChances.Add(new StickerRarityChance
            {
                rarity = present[i],
                chance = defaults[i]
            });
        }
    }

    private static Dictionary<StickerRarity, List<WeightedDrop>> BuildGroups(
        CaseData capsule)
    {
        Dictionary<StickerRarity, List<WeightedDrop>> groups =
            new Dictionary<StickerRarity, List<WeightedDrop>>();

        if (capsule == null || capsule.dropPool == null)
            return groups;

        for (int i = 0; i < capsule.dropPool.Count; i++)
        {
            WeightedDrop drop = capsule.dropPool[i];
            StickerData sticker = drop != null
                ? drop.skin as StickerData
                : null;

            if (sticker == null)
                continue;

            if (!groups.TryGetValue(
                    sticker.stickerRarity,
                    out List<WeightedDrop> list))
            {
                list = new List<WeightedDrop>();
                groups.Add(sticker.stickerRarity, list);
            }

            list.Add(drop);
        }

        return groups;
    }

    private static StickerRarity RollRarity(
        CaseData capsule,
        Dictionary<StickerRarity, List<WeightedDrop>> groups)
    {
        if (!HasPositiveConfiguredChance(capsule, groups))
        {
            EnsureDefaultRarityTable(capsule);
        }

        float total = 0f;
        StickerRarity last = StickerRarity.HighGrade;

        for (int i = 0; i < capsule.stickerRarityChances.Count; i++)
        {
            StickerRarityChance chance = capsule.stickerRarityChances[i];

            if (chance == null || chance.chance <= 0f ||
                !groups.ContainsKey(chance.rarity))
            {
                continue;
            }

            total += chance.chance;
            last = chance.rarity;
        }

        if (total <= 0f)
        {
            foreach (StickerRarity rarity in groups.Keys)
                return rarity;

            return last;
        }

        float roll = Random.Range(0f, total);
        float current = 0f;

        for (int i = 0; i < capsule.stickerRarityChances.Count; i++)
        {
            StickerRarityChance chance = capsule.stickerRarityChances[i];

            if (chance == null || chance.chance <= 0f ||
                !groups.ContainsKey(chance.rarity))
            {
                continue;
            }

            current += chance.chance;

            if (roll <= current)
                return chance.rarity;
        }

        return last;
    }

    private static bool HasPositiveConfiguredChance(
        CaseData capsule,
        Dictionary<StickerRarity, List<WeightedDrop>> groups)
    {
        if (capsule == null || capsule.stickerRarityChances == null)
            return false;

        float total = 0f;
        HashSet<StickerRarity> seen = new HashSet<StickerRarity>();

        for (int i = 0; i < capsule.stickerRarityChances.Count; i++)
        {
            StickerRarityChance chance = capsule.stickerRarityChances[i];

            if (chance == null || chance.chance <= 0f ||
                !groups.ContainsKey(chance.rarity) ||
                !seen.Add(chance.rarity))
            {
                continue;
            }

            total += chance.chance;
        }

        return total > 0f && seen.Count == groups.Count;
    }

    private static StickerData RollWeighted(List<WeightedDrop> drops)
    {
        if (drops == null || drops.Count == 0)
            return null;

        float total = 0f;
        int valid = 0;

        for (int i = 0; i < drops.Count; i++)
        {
            WeightedDrop drop = drops[i];

            if (drop == null || !(drop.skin is StickerData))
                continue;

            valid++;
            total += Mathf.Max(0f, drop.weight);
        }

        if (valid == 0)
            return null;

        if (total <= 0f)
        {
            int target = Random.Range(0, valid);
            int current = 0;

            for (int i = 0; i < drops.Count; i++)
            {
                StickerData sticker = drops[i] != null
                    ? drops[i].skin as StickerData
                    : null;

                if (sticker == null)
                    continue;

                if (current == target)
                    return sticker;

                current++;
            }
        }

        float roll = Random.Range(0f, total);
        float accumulated = 0f;
        StickerData last = null;

        for (int i = 0; i < drops.Count; i++)
        {
            WeightedDrop drop = drops[i];
            StickerData sticker = drop != null
                ? drop.skin as StickerData
                : null;

            if (sticker == null)
                continue;

            last = sticker;
            accumulated += Mathf.Max(0f, drop.weight);

            if (roll <= accumulated)
                return sticker;
        }

        return last;
    }

    private static float[] GetDefaultTierChances(int count)
    {
        switch (count)
        {
            case 1: return new[] { 100f };
            case 2: return new[] { 80f, 20f };
            case 3: return new[] { 80f, 16f, 4f };
            case 4: return new[] { 80f, 16f, 3.2f, 0.8f };
            default: return new[] { 80f, 16f, 3.2f, 0.64f, 0.16f };
        }
    }
}
