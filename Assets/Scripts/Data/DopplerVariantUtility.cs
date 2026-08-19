using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime rules for the legacy generic "Doppler" / "Gamma Doppler" SkinData
/// assets used by CaseData drop pools. The generic asset remains the weighted
/// case-pool entry so existing case odds are unchanged, while an actual opening
/// resolves it into a concrete phase/gem SkinData asset.
/// </summary>
public static class DopplerVariantUtility
{
    private const string DopplerName = "Doppler";
    private const string GammaDopplerName = "Gamma Doppler";

    public static bool IsGenericParent(SkinData skin)
    {
        if (skin == null || skin.isVanilla)
            return false;

        string name = (skin.skinName ?? "").Trim();
        return string.Equals(name, DopplerName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, GammaDopplerName, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDopplerFamily(SkinData skin)
    {
        if (skin == null || skin.isVanilla)
            return false;

        string name = (skin.skinName ?? "").Trim();
        return name.StartsWith("Doppler", StringComparison.OrdinalIgnoreCase) &&
               !name.StartsWith("Gamma Doppler", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsGammaDopplerFamily(SkinData skin)
    {
        if (skin == null || skin.isVanilla)
            return false;

        string name = (skin.skinName ?? "").Trim();
        return name.StartsWith("Gamma Doppler", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsConcreteVariant(SkinData skin)
    {
        return skin != null &&
               !IsGenericParent(skin) &&
               (IsDopplerFamily(skin) || IsGammaDopplerFamily(skin));
    }

    public static GameDatabase GetDatabase()
    {
        if (SaveManager.Instance != null && SaveManager.Instance.database != null)
            return SaveManager.Instance.database;

        GameDatabase[] loaded = Resources.FindObjectsOfTypeAll<GameDatabase>();

        if (loaded != null && loaded.Length == 1)
            return loaded[0];

        return null;
    }

    public static List<SkinData> GetVariants(
        SkinData genericParent,
        GameDatabase database = null)
    {
        List<SkinData> result = new List<SkinData>();

        if (!IsGenericParent(genericParent))
            return result;

        if (database == null)
            database = GetDatabase();

        if (database == null || database.allSkins == null)
            return result;

        bool gamma = IsGammaDopplerFamily(genericParent);
        string weapon = (genericParent.weaponName ?? "").Trim();

        for (int i = 0; i < database.allSkins.Count; i++)
        {
            SkinData candidate = database.allSkins[i];

            if (candidate == null || candidate == genericParent)
                continue;

            if (!string.Equals(
                    (candidate.weaponName ?? "").Trim(),
                    weapon,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsConcreteVariant(candidate))
                continue;

            if (gamma != IsGammaDopplerFamily(candidate))
                continue;

            if (!IsAllowedConcreteVariant(candidate, gamma))
                continue;

            result.Add(candidate);
        }

        result.Sort((a, b) => GetVariantOrder(a).CompareTo(GetVariantOrder(b)));
        return result;
    }

    public static SkinData GetPhaseOneVariant(
        SkinData genericParent,
        GameDatabase database = null)
    {
        List<SkinData> variants = GetVariants(genericParent, database);

        for (int i = 0; i < variants.Count; i++)
        {
            if (GetVariantOrder(variants[i]) == 0)
                return variants[i];
        }

        return variants.Count > 0 ? variants[0] : null;
    }

    public static SkinData RollVariantIfNeeded(
        SkinData selectedSkin,
        GameDatabase database = null)
    {
        if (!IsGenericParent(selectedSkin))
            return selectedSkin;

        List<SkinData> variants = GetVariants(selectedSkin, database);

        if (variants.Count == 0)
        {
            Debug.LogWarning(
                $"DopplerVariantUtility: No concrete variants are registered for " +
                $"{selectedSkin.weaponName} | {selectedSkin.skinName}. Run " +
                "Case Curator > Skins > Setup Doppler Variants in the Editor.");
            return selectedSkin;
        }

        return ChooseWeighted(variants, UnityEngine.Random.value);
    }

    /// <summary>
    /// Converts an already-owned legacy generic Doppler into a deterministic
    /// concrete phase/gem using its existing pattern ID. The same old item maps
    /// to the same variant every time it is loaded.
    /// </summary>
    public static SkinData ResolveLegacyVariant(
        SkinData legacyParent,
        int patternId,
        GameDatabase database = null)
    {
        if (!IsGenericParent(legacyParent))
            return legacyParent;

        List<SkinData> variants = GetVariants(legacyParent, database);

        if (variants.Count == 0)
            return legacyParent;

        int seed = patternId >= 0
            ? patternId
            : StablePositiveHash(
                (legacyParent.apiId ?? "") + "|" +
                (legacyParent.weaponName ?? ""));

        float roll01 = (Math.Abs(seed) % 10001) / 10000f;
        return ChooseWeighted(variants, roll01);
    }

    public static int GetVariantOrder(SkinData skin)
    {
        if (skin == null)
            return int.MaxValue;

        string name = (skin.skinName ?? "").ToLowerInvariant();

        if (name.Contains("phase 1")) return 0;
        if (name.Contains("phase 2")) return 1;
        if (name.Contains("phase 3")) return 2;
        if (name.Contains("phase 4")) return 3;
        if (name.Contains("ruby")) return 4;
        if (name.Contains("sapphire")) return 5;
        if (name.Contains("black pearl")) return 6;
        if (name.Contains("emerald")) return 4;
        return 100;
    }

    private static SkinData ChooseWeighted(
        List<SkinData> variants,
        float roll01)
    {
        if (variants == null || variants.Count == 0)
            return null;

        float total = 0f;

        for (int i = 0; i < variants.Count; i++)
            total += GetDefaultWeight(variants[i]);

        if (total <= 0f)
            return variants[Mathf.Clamp((int)(roll01 * variants.Count), 0, variants.Count - 1)];

        float target = Mathf.Clamp01(roll01) * total;
        float running = 0f;
        SkinData last = variants[variants.Count - 1];

        for (int i = 0; i < variants.Count; i++)
        {
            SkinData candidate = variants[i];
            running += GetDefaultWeight(candidate);

            if (target <= running)
                return candidate;
        }

        return last;
    }

    /// <summary>
    /// Authored defaults, deliberately centralized so they can be tuned later.
    /// Standard Doppler: phases total 95%, Ruby 2%, Sapphire 2%, Black Pearl 1%.
    /// Gamma Doppler: phases total 95%, Emerald 5%.
    /// </summary>
    private static float GetDefaultWeight(SkinData skin)
    {
        if (skin == null)
            return 0f;

        string name = (skin.skinName ?? "").ToLowerInvariant();
        bool gamma = IsGammaDopplerFamily(skin);

        if (name.Contains("phase 1") ||
            name.Contains("phase 2") ||
            name.Contains("phase 3") ||
            name.Contains("phase 4"))
        {
            return 23.75f;
        }

        if (gamma && name.Contains("emerald"))
            return 5f;

        if (!gamma && name.Contains("ruby"))
            return 2f;

        if (!gamma && name.Contains("sapphire"))
            return 2f;

        if (!gamma && name.Contains("black pearl"))
            return 1f;

        return 0f;
    }

    private static bool IsAllowedConcreteVariant(SkinData skin, bool gamma)
    {
        if (skin == null)
            return false;

        string name = (skin.skinName ?? "").ToLowerInvariant();

        if (name.Contains("phase 1") ||
            name.Contains("phase 2") ||
            name.Contains("phase 3") ||
            name.Contains("phase 4"))
        {
            return true;
        }

        if (gamma)
            return name.Contains("emerald");

        return name.Contains("ruby") ||
               name.Contains("sapphire") ||
               name.Contains("black pearl");
    }

    private static int StablePositiveHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            string text = value ?? "";

            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }

            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
