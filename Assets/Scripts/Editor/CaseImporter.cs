using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[Serializable]
public class CrateJsonRarity
{
    public string id;
    public string name;
    public string color;
}

[Serializable]
public class CrateJsonItem
{
    public string id;
    public string name;
    public CrateJsonRarity rarity;
    public string paint_index;
    public string image;
}

[Serializable]
public class CrateJsonEntry
{
    public string id;
    public string name;
    public string description;
    public string type;
    public string first_sale_date;
    public string image;

    public List<CrateJsonItem> contains;
    public List<CrateJsonItem> contains_rare;
}

[Serializable]
public class CrateJsonRoot
{
    public List<CrateJsonEntry> items;
}

public static class CaseImporter
{
    const string OutputFolder = "Assets/Data/Cases";
    const string CollectionTypesPath = "Assets/Data/ImportData/CollectionTypes.txt";
    const string SkinsFolder = "Assets/Data/Skins";

    [MenuItem("Case Catcher/Import Cases From JSON")]
    public static void ImportCases()
    {
        string jsonPath = EditorUtility.OpenFilePanel(
            "Select crates.json",
            "",
            "json");

        if (string.IsNullOrEmpty(jsonPath))
            return;

        HashSet<string> allowedCases = LoadAllowedCases();

        if (allowedCases.Count == 0)
        {
            Debug.LogError("No cases loaded from CollectionTypes.txt. Check the file path and =Case entries.");
            return;
        }

        Dictionary<string, SkinData> skinLookup = BuildSkinLookup();

        string rawJson = File.ReadAllText(jsonPath);
        string wrapped = "{\"items\":" + rawJson + "}";

        CrateJsonRoot root = JsonUtility.FromJson<CrateJsonRoot>(wrapped);

        if (root == null || root.items == null)
        {
            Debug.LogError("Failed to parse crates.json.");
            return;
        }

        if (!Directory.Exists(OutputFolder))
            Directory.CreateDirectory(OutputFolder);

        int created = 0;
        int skippedNotAllowed = 0;
        int skippedExisting = 0;
        int missingSkins = 0;

        foreach (var crate in root.items)
        {
            if (crate == null || string.IsNullOrWhiteSpace(crate.name))
                continue;

            if (!allowedCases.Contains(crate.name))
            {
                skippedNotAllowed++;
                continue;
            }

            string assetPath = $"{OutputFolder}/{BuildSafeFileName(crate.name)}.asset";

            if (AssetDatabase.LoadAssetAtPath<CaseData>(assetPath) != null)
            {
                skippedExisting++;
                continue;
            }

            CaseData caseData = ScriptableObject.CreateInstance<CaseData>();

            caseData.apiId = crate.id;
            caseData.caseName = crate.name;
            caseData.priceInGold = 0f;
            caseData.isPremium = false;
            caseData.quality =
    CaseQualityUtility.GetQualityFromGoldPrice(caseData.priceInGold);

caseData.requiredRank = PlayerRank.SilverI;
caseData.isCustomCase = false;
caseData.shouldHaveRareSpecial =
    !caseData.caseName.ToLowerInvariant().Contains("terminal");

            SetupDefaultRarityChances(caseData);

            AddContainedItemsToCase(caseData, crate.contains, skinLookup, ref missingSkins);
            AddContainedItemsToCase(caseData, crate.contains_rare, skinLookup, ref missingSkins);

            AssetDatabase.CreateAsset(caseData, assetPath);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Case import complete: {created} created, " +
            $"{skippedExisting} skipped existing, " +
            $"{skippedNotAllowed} skipped not in CollectionTypes.txt, " +
            $"{missingSkins} contained skins not found.");
    }

    [MenuItem("Case Catcher/Fix Case API IDs On Existing Cases")]
    public static void FixCaseApiIds()
    {
        string jsonPath = EditorUtility.OpenFilePanel(
            "Select crates.json",
            "",
            "json");

        if (string.IsNullOrEmpty(jsonPath))
            return;

        HashSet<string> allowedCases = LoadAllowedCases();

        string rawJson = File.ReadAllText(jsonPath);
        string wrapped = "{\"items\":" + rawJson + "}";

        CrateJsonRoot root = JsonUtility.FromJson<CrateJsonRoot>(wrapped);

        if (root == null || root.items == null)
        {
            Debug.LogError("Failed to parse crates.json.");
            return;
        }

        int updated = 0;
        int notFound = 0;
        int skipped = 0;

        foreach (var crate in root.items)
        {
            if (crate == null || string.IsNullOrWhiteSpace(crate.name))
            {
                skipped++;
                continue;
            }

            if (!allowedCases.Contains(crate.name))
            {
                skipped++;
                continue;
            }

            string assetPath = $"{OutputFolder}/{BuildSafeFileName(crate.name)}.asset";

            CaseData caseData = AssetDatabase.LoadAssetAtPath<CaseData>(assetPath);

            if (caseData == null)
            {
                notFound++;
                continue;
            }

            if (caseData.apiId != crate.id)
            {
                caseData.apiId = crate.id;
                EditorUtility.SetDirty(caseData);
                updated++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Case API ID fix complete: {updated} updated, {notFound} not found, {skipped} skipped.");
    }

    [MenuItem("Case Catcher/Fix Duplicate Skins In Cases")]
    public static void FixDuplicateSkinsInCases()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:CaseData",
            new[] { OutputFolder });

        int casesChanged = 0;
        int duplicatesRemoved = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CaseData caseData = AssetDatabase.LoadAssetAtPath<CaseData>(path);

            if (caseData == null || caseData.dropPool == null)
                continue;

            HashSet<SkinData> seenSkins = new HashSet<SkinData>();
            List<WeightedDrop> cleanedDropPool = new List<WeightedDrop>();

            foreach (WeightedDrop drop in caseData.dropPool)
            {
                if (drop == null || drop.skin == null)
                    continue;

                if (seenSkins.Contains(drop.skin))
                {
                    duplicatesRemoved++;
                    continue;
                }

                seenSkins.Add(drop.skin);
                cleanedDropPool.Add(drop);
            }

            if (cleanedDropPool.Count != caseData.dropPool.Count)
            {
                caseData.dropPool = cleanedDropPool;
                EditorUtility.SetDirty(caseData);
                casesChanged++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Duplicate case skin fix complete: " +
            $"{duplicatesRemoved} duplicates removed from {casesChanged} cases.");
    }

    static HashSet<string> LoadAllowedCases()
    {
        HashSet<string> result = new HashSet<string>();

        if (!File.Exists(CollectionTypesPath))
        {
            Debug.LogError($"CollectionTypes file not found: {CollectionTypesPath}");
            return result;
        }

        string[] lines = File.ReadAllLines(CollectionTypesPath);

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string[] parts = line.Split('=');

            if (parts.Length != 2)
                continue;

            string name = parts[0].Trim();
            string type = parts[1].Trim();

            if (type == "Case")
                result.Add(name);
        }

        Debug.Log($"Loaded {result.Count} allowed cases.");

        return result;
    }

    static Dictionary<string, SkinData> BuildSkinLookup()
    {
        Dictionary<string, SkinData> result = new Dictionary<string, SkinData>();

        string[] guids = AssetDatabase.FindAssets(
            "t:SkinData",
            new[] { SkinsFolder });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkinData skin = AssetDatabase.LoadAssetAtPath<SkinData>(path);

            if (skin == null)
                continue;

            string normalKey = BuildSkinKey(skin.weaponName, skin.skinName);

            if (!result.ContainsKey(normalKey))
                result.Add(normalKey, skin);

            if (string.IsNullOrWhiteSpace(skin.skinName))
            {
                string vanillaKey = BuildVanillaKnifeKey(skin.weaponName);

                if (!result.ContainsKey(vanillaKey))
                    result.Add(vanillaKey, skin);

                bool changed = false;

                if (!skin.isVanilla)
                {
                    skin.isVanilla = true;
                    changed = true;
                }

                if (skin.minFloat != 0f)
                {
                    skin.minFloat = 0f;
                    changed = true;
                }

                if (skin.maxFloat != 0f)
                {
                    skin.maxFloat = 0f;
                    changed = true;
                }

                if (skin.patternType != PatternType.None)
                {
                    skin.patternType = PatternType.None;
                    changed = true;
                }

                if (changed)
                    EditorUtility.SetDirty(skin);
            }
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"Loaded {result.Count} SkinData lookup keys for case linking.");

        return result;
    }

    static void AddContainedItemsToCase(
        CaseData caseData,
        List<CrateJsonItem> items,
        Dictionary<string, SkinData> skinLookup,
        ref int missingSkins)
    {
        if (items == null)
            return;

        foreach (var item in items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.name))
                continue;

            string key = BuildSkinKeyFromFullName(item.name);

            if (!skinLookup.TryGetValue(key, out SkinData skin))
            {
                missingSkins++;

                if (missingSkins <= 20)
                {
                    Debug.LogWarning(
                        $"Skin not found for case '{caseData.caseName}': {item.name} | key: {key}");
                }

                continue;
            }

            if (CaseAlreadyContainsSkin(caseData, skin))
                continue;

            WeightedDrop drop = new WeightedDrop();
            drop.skin = skin;
            drop.weight = 1f;

            caseData.dropPool.Add(drop);
        }
    }

    static bool CaseAlreadyContainsSkin(CaseData caseData, SkinData skin)
    {
        foreach (WeightedDrop drop in caseData.dropPool)
        {
            if (drop != null && drop.skin == skin)
                return true;
        }

        return false;
    }

    static void SetupDefaultRarityChances(CaseData caseData)
    {
        caseData.rarityChances = new List<RarityChance>
        {
            new RarityChance { rarity = Rarity.MilSpec, chance = 79.92f },
            new RarityChance { rarity = Rarity.Restricted, chance = 15.98f },
            new RarityChance { rarity = Rarity.Classified, chance = 3.20f },
            new RarityChance { rarity = Rarity.Covert, chance = 0.64f },
            new RarityChance { rarity = Rarity.RareSpecial, chance = 0.26f },
        };
    }

    static string BuildSkinKeyFromFullName(string fullName)
    {
        string cleaned = fullName
            .Replace("★", "")
            .Replace("StatTrak™", "")
            .Replace("Souvenir", "")
            .Trim();

        string[] parts = cleaned.Split('|');

        if (parts.Length == 2)
        {
            string weaponName = parts[0].Trim();
            string skinName = parts[1].Trim();

            return BuildSkinKey(weaponName, skinName);
        }

        return BuildVanillaKnifeKey(cleaned);
    }

    static string BuildVanillaKnifeKey(string weaponName)
    {
        return weaponName
            .ToLowerInvariant()
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("_", "")
            .Replace("™", "")
            .Replace("★", "")
            .Trim();
    }

    static string BuildSkinKey(string weaponName, string skinName)
    {
        return $"{weaponName}|{skinName}"
            .ToLowerInvariant()
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("_", "")
            .Replace("™", "")
            .Replace("★", "")
            .Trim();
    }

    static string BuildSafeFileName(string name)
    {
        return name
            .Replace("/", "-")
            .Replace("\\", "-")
            .Replace(":", "")
            .Replace("*", "")
            .Replace("?", "")
            .Replace("\"", "")
            .Replace("<", "")
            .Replace(">", "")
            .Replace("|", "")
            .Replace("#", "");
    }
}