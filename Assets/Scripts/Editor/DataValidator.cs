using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DataValidator
{
    [MenuItem("Case Catcher/Validate Data")]
    public static void ValidateData()
    {
        int errors = 0;
        int warnings = 0;

        ValidateSkins(ref errors, ref warnings);
        ValidateCases(ref errors, ref warnings);
        ValidateCollections(ref errors, ref warnings);

        if (errors == 0 && warnings == 0)
        {
            Debug.Log("Data validation complete: no problems found.");
        }
        else
        {
            Debug.LogWarning(
                $"Data validation complete: {errors} errors, {warnings} warnings.");
        }
    }

    static void ValidateSkins(ref int errors, ref int warnings)
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:SkinData",
            new[] { "Assets/Data/Skins" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkinData skin = AssetDatabase.LoadAssetAtPath<SkinData>(path);

            if (skin == null)
                continue;

            if (string.IsNullOrWhiteSpace(skin.weaponName))
            {
                Debug.LogError($"Skin missing weapon name: {path}");
                errors++;
            }

            if (!skin.isVanilla && string.IsNullOrWhiteSpace(skin.skinName))
            {
                Debug.LogWarning($"Non-vanilla skin has empty skin name: {path}");
                warnings++;
            }

            if (string.IsNullOrWhiteSpace(skin.apiId))
            {
                Debug.LogWarning($"Skin missing apiId: {path}");
                warnings++;
            }

            if (skin.minFloat > skin.maxFloat)
            {
                Debug.LogError($"Skin minFloat is higher than maxFloat: {path}");
                errors++;
            }

            if (skin.isVanilla)
            {
                if (skin.minFloat != 0f || skin.maxFloat != 0f)
                {
                    Debug.LogWarning($"Vanilla skin should have min/max float 0: {path}");
                    warnings++;
                }
            }

            if (skin.canBeSouvenir)
            {
                if (skin.collectionData == null)
                {
                    Debug.LogWarning(
                        $"Souvenir skin has no linked CollectionData: {path}");
                    warnings++;
                }
                else if (skin.collectionData.type != CollectionType.SouvenirCollection)
                {
                    Debug.LogError(
                        $"Skin canBeSouvenir is true but collection is not SouvenirCollection: {path}");
                    errors++;
                }
            }
        }
    }

    static void ValidateCases(ref int errors, ref int warnings)
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:CaseData",
            new[] { "Assets/Data/Cases" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CaseData caseData = AssetDatabase.LoadAssetAtPath<CaseData>(path);

            if (caseData == null)
                continue;

            if (string.IsNullOrWhiteSpace(caseData.caseName))
            {
                Debug.LogError($"Case missing caseName: {path}");
                errors++;
            }

            if (string.IsNullOrWhiteSpace(caseData.apiId) && !caseData.isCustomCase)
            {
                Debug.LogWarning($"Case missing apiId: {path}");
                warnings++;
            }

            CaseQuality expectedQuality =
                CaseQualityUtility.GetQualityFromGoldPrice(caseData.priceInGold);

            if (caseData.quality != expectedQuality)
            {
                Debug.LogWarning(
                    $"Case quality does not match price: {caseData.caseName}. " +
                    $"Current: {caseData.quality}, Expected: {expectedQuality}");
                warnings++;
            }

            if (caseData.rarityChances == null || caseData.rarityChances.Count == 0)
            {
                Debug.LogError($"Case has no rarity chances: {path}");
                errors++;
            }

            if (caseData.dropPool == null || caseData.dropPool.Count == 0)
            {
                Debug.LogError($"Case has empty dropPool: {path}");
                errors++;
                continue;
            }

            HashSet<SkinData> seenSkins = new HashSet<SkinData>();
            bool hasRareSpecial = false;

            foreach (WeightedDrop drop in caseData.dropPool)
            {
                if (drop == null || drop.skin == null)
                {
                    Debug.LogError($"Case has null drop entry: {path}");
                    errors++;
                    continue;
                }

                if (seenSkins.Contains(drop.skin))
                {
                    Debug.LogWarning(
                        $"Case has duplicate skin: {caseData.caseName} / " +
                        $"{drop.skin.weaponName} {drop.skin.skinName}");
                    warnings++;
                }
                else
                {
                    seenSkins.Add(drop.skin);
                }

                if (drop.skin.rarity == Rarity.RareSpecial)
                    hasRareSpecial = true;
            }

            if (!hasRareSpecial && CaseShouldHaveRareSpecial(caseData))
            {
                Debug.LogWarning($"Case has no RareSpecial items: {path}");
                warnings++;
            }
        }
    }

    static bool CaseShouldHaveRareSpecial(CaseData caseData)
    {
        if (caseData == null)
            return false;

        if (!caseData.shouldHaveRareSpecial)
            return false;

        if (string.IsNullOrWhiteSpace(caseData.caseName))
            return false;

        string name = caseData.caseName.ToLowerInvariant();

        // Terminals do not need knives/gloves/rare special items.
        if (name.Contains("terminal"))
            return false;

        return true;
    }

    static void ValidateCollections(ref int errors, ref int warnings)
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:CollectionData",
            new[] { "Assets/Data/Collections" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CollectionData collection =
                AssetDatabase.LoadAssetAtPath<CollectionData>(path);

            if (collection == null)
                continue;

            if (string.IsNullOrWhiteSpace(collection.collectionName))
            {
                Debug.LogError($"Collection missing name: {path}");
                errors++;
            }

            if (string.IsNullOrWhiteSpace(collection.apiId))
            {
                Debug.LogWarning($"Collection missing apiId: {path}");
                warnings++;
            }
        }
    }
}