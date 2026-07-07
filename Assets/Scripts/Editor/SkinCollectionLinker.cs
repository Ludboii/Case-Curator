using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SkinCollectionLinker
{
    const string SkinsFolder = "Assets/Data/Skins";
    const string CollectionsFolder = "Assets/Data/Collections";

    [MenuItem("Case Catcher/Link Skins To Collections")]
    public static void LinkSkinsToCollections()
    {
        Dictionary<string, CollectionData> collectionLookup =
            BuildCollectionLookup();

        string[] skinGuids = AssetDatabase.FindAssets(
            "t:SkinData",
            new[] { SkinsFolder });

        int linked = 0;
        int alreadyLinked = 0;
        int notFound = 0;

        foreach (string guid in skinGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SkinData skin = AssetDatabase.LoadAssetAtPath<SkinData>(path);

            if (skin == null)
                continue;

            if (string.IsNullOrWhiteSpace(skin.collection))
            {
                notFound++;
                continue;
            }

            if (!collectionLookup.TryGetValue(skin.collection, out CollectionData collection))
            {
                notFound++;
                continue;
            }

            if (skin.collectionData == collection)
            {
                alreadyLinked++;
                continue;
            }

            skin.collectionData = collection;
            EditorUtility.SetDirty(skin);
            linked++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Skin collection linking complete: {linked} linked, " +
            $"{alreadyLinked} already linked, {notFound} collection names not found.");
    }

    static Dictionary<string, CollectionData> BuildCollectionLookup()
    {
        Dictionary<string, CollectionData> result =
            new Dictionary<string, CollectionData>();

        string[] collectionGuids = AssetDatabase.FindAssets(
            "t:CollectionData",
            new[] { CollectionsFolder });

        foreach (string guid in collectionGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CollectionData collection =
                AssetDatabase.LoadAssetAtPath<CollectionData>(path);

            if (collection == null)
                continue;

            if (string.IsNullOrWhiteSpace(collection.collectionName))
                continue;

            if (!result.ContainsKey(collection.collectionName))
            {
                result.Add(collection.collectionName, collection);
            }
        }

        Debug.Log($"Loaded {result.Count} CollectionData assets for linking.");

        return result;
    }
}