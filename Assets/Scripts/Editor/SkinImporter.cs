using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// --- Minimal mirror of the relevant fields in ByMykel/CSGO-API's skins.json ---
// Source: https://raw.githubusercontent.com/ByMykel/CSGO-API/main/public/api/en/skins.json
// JsonUtility ignores any JSON fields not declared here, so this only needs
// what we actually use.

[Serializable]
public class SkinJsonWeapon { public string name; }

[Serializable]
public class SkinJsonPattern { public string name; }

[Serializable]
public class SkinJsonCategory { public string name; }

[Serializable]
public class SkinJsonRarity { public string id; public string name; }

[Serializable]
public class SkinJsonCollection { public string name; }

[Serializable]
public class SkinJsonCrate { public string name; }

[Serializable]
public class SkinJsonEntry
{
    public string id;
    public string name;
    public SkinJsonWeapon weapon;
    public SkinJsonPattern pattern;
    public SkinJsonCategory category;
    public SkinJsonRarity rarity;
    public float min_float;
    public float max_float;
    public bool stattrak;
    public bool souvenir;
    public string paint_index;
    public List<SkinJsonCollection> collections;
    public List<SkinJsonCrate> crates;
    public string image;
    
}

// JsonUtility can't parse a top-level JSON array directly, so the raw file
// gets wrapped as {"items": [...]} before parsing.
[Serializable]
public class SkinJsonRoot
{
    public List<SkinJsonEntry> items;
}

public static class SkinImporter
{
    // Adjust this to wherever you want the generated SkinData assets to live.
     const string OutputFolder = "Assets/Data/Skins";
static HashSet<string> LoadSouvenirCollections()
{
    HashSet<string> result = new HashSet<string>();

    string path = "Assets/Data/ImportData/CollectionTypes.txt";

    if (!File.Exists(path))
    {
        Debug.LogError($"CollectionTypes file not found: {path}");
        return result;
    }

    string[] lines = File.ReadAllLines(path);

    foreach (string line in lines)
    {
        if (string.IsNullOrWhiteSpace(line))
            continue;

        string[] parts = line.Split('=');

        if (parts.Length != 2)
            continue;

        string collectionName = parts[0].Trim();
        string type = parts[1].Trim();

        if (type == "SouvenirCollection")
        {
            result.Add(collectionName);
        }
    }

    Debug.Log($"Loaded {result.Count} souvenir collections.");

    return result;
}
[MenuItem("Case Catcher/Fix Souvenir Flags On Existing Skins")]
    public static void FixSouvenirFlags()
{
    string path = EditorUtility.OpenFilePanel("Select skins.json", "", "json");
    if (string.IsNullOrEmpty(path)) return;

    string rawJson = File.ReadAllText(path);
    string wrapped = "{\"items\":" + rawJson + "}";
    SkinJsonRoot root = JsonUtility.FromJson<SkinJsonRoot>(wrapped);
Debug.Log($"Parsed items: {root?.items?.Count}");

HashSet<string> souvenirCollections = LoadSouvenirCollections();

    int fixedCount = 0;
    int notFound = 0;
    int skipped = 0;
    foreach (var entry in root.items)
    {
        if (entry.weapon == null || entry.pattern == null)
{
    skipped++;
    continue;
}

        string safeName = SkinNameUtility.BuildSafeName(entry.weapon.name, entry.pattern.name);
        string assetPath = $"{OutputFolder}/{safeName}.asset";

        SkinData skin = AssetDatabase.LoadAssetAtPath<SkinData>(assetPath);
        if (skin == null)
{
    notFound++;

    if (notFound <= 5)
    {
        Debug.LogWarning($"Not found: {assetPath}");
    }

    continue;
}
if (notFound < 5)
{
    Debug.Log(assetPath);
}
        bool correctValue = false;

if (entry.collections != null)
{
    foreach (var collection in entry.collections)
    {
        if (souvenirCollections.Contains(collection.name))
        {
            correctValue = true;
            break;
        }
    }
    if (entry.weapon.name == "AK-47" &&
    entry.pattern.name == "Asiimov")
{
    Debug.Log(
        $"AK Asiimov collection = {entry.collections?[0].name}, souvenir = {correctValue}"
    );
}
}
        if (skin.canBeSouvenir != correctValue)
        {
            skin.canBeSouvenir = correctValue;
            EditorUtility.SetDirty(skin);
            fixedCount++;
        }
    }

    AssetDatabase.SaveAssets();
    Debug.Log($"Souvenir flag fix complete: {fixedCount} updated, {notFound} not found.");
}
[MenuItem("Case Catcher/Fix Skin API IDs On Existing Skins")]
public static void FixSkinApiIds()
{
    string path = EditorUtility.OpenFilePanel("Select skins.json", "", "json");

    if (string.IsNullOrEmpty(path))
        return;

    string rawJson = File.ReadAllText(path);
    string wrapped = "{\"items\":" + rawJson + "}";

    SkinJsonRoot root = JsonUtility.FromJson<SkinJsonRoot>(wrapped);

    if (root == null || root.items == null)
    {
        Debug.LogError("Failed to parse skins.json.");
        return;
    }

    int updated = 0;
    int notFound = 0;
    int skipped = 0;

    foreach (var entry in root.items)
    {
        if (entry.weapon == null || entry.pattern == null)
        {
            skipped++;
            continue;
        }

        string safeName = SkinNameUtility.BuildSafeName(
            entry.weapon.name,
            entry.pattern.name);

        string assetPath = $"{OutputFolder}/{safeName}.asset";

        SkinData skin = AssetDatabase.LoadAssetAtPath<SkinData>(assetPath);

        if (skin == null)
        {
            notFound++;
            continue;
        }

        bool changed = false;

        if (skin.apiId != entry.id)
        {
            skin.apiId = entry.id;
            changed = true;
        }

        if (skin.paintIndex != entry.paint_index)
        {
            skin.paintIndex = entry.paint_index;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(skin);
            updated++;
        }
    }

    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();

    Debug.Log(
        $"Skin API ID fix complete: {updated} updated, {notFound} not found, {skipped} skipped.");
}
[MenuItem("Case Catcher/Fix Vanilla Knife Data")]
public static void FixVanillaKnifeData()
{
    string[] guids = AssetDatabase.FindAssets(
        "t:SkinData",
        new[] { "Assets/Data/Skins" });

    int fixedCount = 0;
    int alreadyCorrect = 0;

    foreach (string guid in guids)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        SkinData skin = AssetDatabase.LoadAssetAtPath<SkinData>(path);

        if (skin == null)
            continue;

        bool isRareSpecial = skin.rarity == Rarity.RareSpecial;
        bool hasEmptySkinName = string.IsNullOrWhiteSpace(skin.skinName);

        if (!isRareSpecial || !hasEmptySkinName)
            continue;

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

        // Optional but recommended:
        // Keep the skinName empty, because your importer already made the asset this way.
        // Do NOT set skin.skinName = "Vanilla" unless you want to rename/rekey things later.

        if (changed)
        {
            EditorUtility.SetDirty(skin);
            fixedCount++;
        }
        else
        {
            alreadyCorrect++;
        }
    }

    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();

    Debug.Log(
        $"Vanilla knife data fix complete: {fixedCount} updated, {alreadyCorrect} already correct.");
}
    // CS2's rarity ids for normal weapon skins. Knives/Gloves use different,
    // inconsistent rarity ids (rarity_ancient, rarity_immortal, etc.) since
    // their in-game "rarity" is really just always Rare Special / gold —
    // so those are handled separately below by category instead.
    static readonly Dictionary<string, Rarity> RarityMap = new Dictionary<string, Rarity>
    {
        { "rarity_common_weapon",    Rarity.Consumer },
        { "rarity_uncommon_weapon",  Rarity.Industrial },
        { "rarity_rare_weapon",      Rarity.MilSpec },
        { "rarity_mythical_weapon",  Rarity.Restricted },
        { "rarity_legendary_weapon", Rarity.Classified },
        { "rarity_ancient_weapon",   Rarity.Covert },
    };

    [MenuItem("Case Catcher/Import Skins From JSON")]
    public static void ImportSkins()
    {
        string path = EditorUtility.OpenFilePanel("Select skins.json", "", "json");
        if (string.IsNullOrEmpty(path)) return;

        string rawJson = File.ReadAllText(path);
        string wrapped = "{\"items\":" + rawJson + "}";
        SkinJsonRoot root = JsonUtility.FromJson<SkinJsonRoot>(wrapped);

        if (root == null || root.items == null)
        {
            Debug.LogError("SkinImporter: failed to parse JSON — check the file is the raw skins.json array.");
            return;
        }

        if (!Directory.Exists(OutputFolder))
            Directory.CreateDirectory(OutputFolder);

        int created = 0, skipped = 0;

        foreach (var entry in root.items)
        {
            if (entry.weapon == null || entry.pattern == null)
            {
                skipped++;
                continue;
            }

            string safeName = $"{entry.weapon.name}_{entry.pattern.name}"
                .Replace(" ", "_").Replace("|", "").Replace("™", "").Replace("★", "");
            string assetPath = $"{OutputFolder}/{safeName}.asset";

            // Skip existing assets so re-running the import never overwrites
            // prices or pattern data you've already hand-entered.
            if (AssetDatabase.LoadAssetAtPath<SkinData>(assetPath) != null)
            {
                skipped++;
                continue;
            }

            SkinData skin = ScriptableObject.CreateInstance<SkinData>();

            skin.weaponName    = entry.weapon.name;
            skin.skinName      = entry.pattern.name;
            skin.minFloat      = entry.min_float;
            skin.maxFloat      = entry.max_float;
            skin.canBeStatTrak = entry.stattrak;
            skin.apiId = entry.id;
            skin.paintIndex = entry.paint_index;
            skin.canBeSouvenir = false;
            bool isKnifeOrGlove = entry.category != null &&
                (entry.category.name == "Knives" || entry.category.name == "Gloves");

            if (isKnifeOrGlove)
            {
                skin.rarity = Rarity.RareSpecial;
            }
            else if (entry.rarity != null && RarityMap.TryGetValue(entry.rarity.id, out Rarity mapped))
            {
                skin.rarity = mapped;
            }
            else
            {
                // Unmapped rarity id — most commonly knives/gloves that slipped
                // past the category check. Default to RareSpecial rather than
                // silently mis-tiering a high-value item as Consumer.
                skin.rarity = Rarity.RareSpecial;
            }

            if (entry.collections != null && entry.collections.Count > 0)
                skin.collection = entry.collections[0].name;
            else if (entry.crates != null && entry.crates.Count > 0)
                skin.collection = entry.crates[0].name;

            // Prices are deliberately left blank — fill these in by hand from
            // the Steam Market, per your pricing design. This import only
            // sets up structural data (names, rarity, float caps, source).

            AssetDatabase.CreateAsset(skin, assetPath);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Skipped: {skipped}");
        Debug.Log($"Skin import complete: {created} created, {skipped} skipped (already existed or missing data).");
    }

}