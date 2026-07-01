using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[Serializable]
public class CollectionJsonEntry
{
    public string id;
    public string name;
}

[Serializable]
public class CollectionJsonRoot
{
    public List<CollectionJsonEntry> items;
}

public static class CollectionImporter
{
    const string OutputFolder = "Assets/Data/Collections";
static Dictionary<string, CollectionType> LoadCollectionTypes()
{
    Dictionary<string, CollectionType> result =
        new Dictionary<string, CollectionType>();

    string path =
        "Assets/Data/ImportData/CollectionTypes.txt";

    if (!File.Exists(path))
    {
        Debug.LogError(
            $"CollectionTypes file not found: {path}");
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
        string typeText = parts[1].Trim();

        if (Enum.TryParse(
                typeText,
                out CollectionType collectionType))
        {
            result[collectionName] = collectionType;
        }
    }

    Debug.Log(
        $"Loaded {result.Count} collection types.");

    return result;
}
[MenuItem("Case Catcher/Fix Collection API IDs On Existing Collections")]
public static void FixCollectionApiIds()
{
    string jsonPath = EditorUtility.OpenFilePanel(
        "Select collections.json",
        "",
        "json");

    if (string.IsNullOrEmpty(jsonPath))
        return;

    string rawJson = File.ReadAllText(jsonPath);
    string wrapped = "{\"items\":" + rawJson + "}";

    CollectionJsonRoot root =
        JsonUtility.FromJson<CollectionJsonRoot>(wrapped);

    if (root == null || root.items == null)
    {
        Debug.LogError("Failed to parse collections.json.");
        return;
    }

    int updated = 0;
    int notFound = 0;
    int skipped = 0;

    foreach (var entry in root.items)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.name))
        {
            skipped++;
            continue;
        }

        if (entry.name.Contains("Graffiti") ||
            entry.name.Contains("Sticker") ||
            entry.name.Contains("Charm") ||
            entry.name.Contains("X-Ray"))
        {
            skipped++;
            continue;
        }

        string safeName = entry.name.Replace("/", "-");

        string assetPath = $"{OutputFolder}/{safeName}.asset";

        CollectionData collection =
            AssetDatabase.LoadAssetAtPath<CollectionData>(assetPath);

        if (collection == null)
        {
            notFound++;
            continue;
        }

        if (collection.apiId != entry.id)
        {
            collection.apiId = entry.id;
            EditorUtility.SetDirty(collection);
            updated++;
        }
    }

    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();

    Debug.Log(
        $"Collection API ID fix complete: {updated} updated, {notFound} not found, {skipped} skipped.");
}
    [MenuItem("Case Catcher/Import Collections From JSON")]
    public static void ImportCollections()
    {
        string jsonPath =
            EditorUtility.OpenFilePanel(
                "Select collections.json",
                "",
                "json");

        if (string.IsNullOrEmpty(jsonPath))
            return;

        string rawJson = File.ReadAllText(jsonPath);

        string wrapped =
            "{\"items\":" + rawJson + "}";

        CollectionJsonRoot root =
            JsonUtility.FromJson<CollectionJsonRoot>(wrapped);

        Dictionary<string, CollectionType> collectionTypes =
            LoadCollectionTypes();
        
        if (root == null || root.items == null)
        {
            Debug.LogError("Failed to parse collections.json");
            return;
        }

        if (!Directory.Exists(OutputFolder))
            Directory.CreateDirectory(OutputFolder);

        int created = 0;
        int skipped = 0;

        foreach (var entry in root.items)
{
    if (entry.name.Contains("Graffiti") ||
        entry.name.Contains("Sticker") ||
        entry.name.Contains("Charm") ||
        entry.name.Contains("X-Ray"))
    {
                skipped++;
                continue;
            }
            string safeName =
                entry.name.Replace("/", "-");

            string assetPath =
                $"{OutputFolder}/{safeName}.asset";

            if (AssetDatabase.LoadAssetAtPath<CollectionData>(assetPath) != null)
            {
                skipped++;
                continue;
            }

            CollectionData collection =
                ScriptableObject.CreateInstance<CollectionData>();

            collection.collectionName = entry.name;
            if (collectionTypes.TryGetValue(
                    entry.name,
                    out CollectionType type))
            {
                collection.type = type;
                collection.apiId = entry.id;
            }
            AssetDatabase.CreateAsset(
                collection,
                assetPath);

            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Collections imported: {created} created, {skipped} skipped.");
    }
}
