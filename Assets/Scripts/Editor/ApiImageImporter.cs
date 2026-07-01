using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class ImageSkinJsonWeapon
{
    public string name;
}

[Serializable]
public class ImageSkinJsonPattern
{
    public string name;
}

[Serializable]
public class ImageSkinJsonEntry
{
    public string id;
    public string name;
    public ImageSkinJsonWeapon weapon;
    public ImageSkinJsonPattern pattern;
    public string image;
}

[Serializable]
public class ImageSkinJsonRoot
{
    public List<ImageSkinJsonEntry> items;
}

[Serializable]
public class ImageCrateJsonEntry
{
    public string id;
    public string name;
    public string image;
}

[Serializable]
public class ImageCrateJsonRoot
{
    public List<ImageCrateJsonEntry> items;
}

[Serializable]
public class ImageCollectionJsonEntry
{
    public string id;
    public string name;
    public string image;
}

[Serializable]
public class ImageCollectionJsonRoot
{
    public List<ImageCollectionJsonEntry> items;
}

public static class ApiImageImporter
{
    const string SkinOutputFolder = "Assets/Art/Imported/Skins";
    const string CaseOutputFolder = "Assets/Art/Imported/Cases";
    const string CollectionOutputFolder = "Assets/Art/Imported/Collections";

    const string SkinDataFolder = "Assets/Data/Skins";
    const string CaseDataFolder = "Assets/Data/Cases";
    const string CollectionDataFolder = "Assets/Data/Collections";

    [MenuItem("Case Catcher/Images/Download Skin Images")]
    public static void DownloadSkinImages()
    {
        string jsonPath = EditorUtility.OpenFilePanel(
            "Select skins.json",
            "",
            "json");

        if (string.IsNullOrEmpty(jsonPath))
            return;

        EnsureFolder(SkinOutputFolder);

        string rawJson = File.ReadAllText(jsonPath);
        string wrapped = "{\"items\":" + rawJson + "}";

        ImageSkinJsonRoot root =
            JsonUtility.FromJson<ImageSkinJsonRoot>(wrapped);

        if (root == null || root.items == null)
        {
            Debug.LogError("Failed to parse skins.json.");
            return;
        }

        int assigned = 0;
        int downloaded = 0;
        int skipped = 0;
        int notFound = 0;

        try
        {
            for (int i = 0; i < root.items.Count; i++)
            {
                ImageSkinJsonEntry entry = root.items[i];

                EditorUtility.DisplayProgressBar(
                    "Downloading skin images",
                    $"{i + 1}/{root.items.Count}",
                    (float)i / root.items.Count);

                if (entry == null ||
                    entry.weapon == null ||
                    entry.pattern == null ||
                    string.IsNullOrWhiteSpace(entry.image))
                {
                    skipped++;
                    continue;
                }

                string safeName = SkinNameUtility.BuildSafeName(
                    entry.weapon.name,
                    entry.pattern.name);

                string skinAssetPath =
                    $"{SkinDataFolder}/{safeName}.asset";

                SkinData skin =
                    AssetDatabase.LoadAssetAtPath<SkinData>(skinAssetPath);

                if (skin == null)
                {
                    notFound++;
                    continue;
                }

                string imageAssetPath =
                    $"{SkinOutputFolder}/{safeName}.png";

                if (!File.Exists(imageAssetPath))
                {
                    if (!DownloadFile(entry.image, imageAssetPath))
                    {
                        skipped++;
                        continue;
                    }

                    downloaded++;
                }

                Sprite sprite = ImportAsSprite(imageAssetPath);

                if (sprite != null && skin.icon != sprite)
                {
                    skin.icon = sprite;
                    EditorUtility.SetDirty(skin);
                    assigned++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Skin image import complete: {downloaded} downloaded, " +
            $"{assigned} assigned, {notFound} skins not found, {skipped} skipped.");
    }

    [MenuItem("Case Catcher/Images/Download Case Images")]
    public static void DownloadCaseImages()
    {
        string jsonPath = EditorUtility.OpenFilePanel(
            "Select crates.json",
            "",
            "json");

        if (string.IsNullOrEmpty(jsonPath))
            return;

        EnsureFolder(CaseOutputFolder);

        string rawJson = File.ReadAllText(jsonPath);
        string wrapped = "{\"items\":" + rawJson + "}";

        ImageCrateJsonRoot root =
            JsonUtility.FromJson<ImageCrateJsonRoot>(wrapped);

        if (root == null || root.items == null)
        {
            Debug.LogError("Failed to parse crates.json.");
            return;
        }

        int assigned = 0;
        int downloaded = 0;
        int skipped = 0;
        int notFound = 0;

        try
        {
            for (int i = 0; i < root.items.Count; i++)
            {
                ImageCrateJsonEntry entry = root.items[i];

                EditorUtility.DisplayProgressBar(
                    "Downloading case images",
                    $"{i + 1}/{root.items.Count}",
                    (float)i / root.items.Count);

                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.name) ||
                    string.IsNullOrWhiteSpace(entry.image))
                {
                    skipped++;
                    continue;
                }

                string caseAssetPath =
                    $"{CaseDataFolder}/{BuildSafeFileName(entry.name)}.asset";

                CaseData caseData =
                    AssetDatabase.LoadAssetAtPath<CaseData>(caseAssetPath);

                if (caseData == null)
                {
                    notFound++;
                    continue;
                }

                string imageAssetPath =
                    $"{CaseOutputFolder}/{BuildSafeFileName(entry.name)}.png";

                if (!File.Exists(imageAssetPath))
                {
                    if (!DownloadFile(entry.image, imageAssetPath))
                    {
                        skipped++;
                        continue;
                    }

                    downloaded++;
                }

                Sprite sprite = ImportAsSprite(imageAssetPath);

                if (sprite != null && caseData.icon != sprite)
                {
                    caseData.icon = sprite;
                    EditorUtility.SetDirty(caseData);
                    assigned++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Case image import complete: {downloaded} downloaded, " +
            $"{assigned} assigned, {notFound} cases not found, {skipped} skipped.");
    }

    [MenuItem("Case Catcher/Images/Download Collection Images")]
    public static void DownloadCollectionImages()
    {
        string jsonPath = EditorUtility.OpenFilePanel(
            "Select collections.json",
            "",
            "json");

        if (string.IsNullOrEmpty(jsonPath))
            return;

        EnsureFolder(CollectionOutputFolder);

        string rawJson = File.ReadAllText(jsonPath);
        string wrapped = "{\"items\":" + rawJson + "}";

        ImageCollectionJsonRoot root =
            JsonUtility.FromJson<ImageCollectionJsonRoot>(wrapped);

        if (root == null || root.items == null)
        {
            Debug.LogError("Failed to parse collections.json.");
            return;
        }

        int assigned = 0;
        int downloaded = 0;
        int skipped = 0;
        int notFound = 0;

        try
        {
            for (int i = 0; i < root.items.Count; i++)
            {
                ImageCollectionJsonEntry entry = root.items[i];

                EditorUtility.DisplayProgressBar(
                    "Downloading collection images",
                    $"{i + 1}/{root.items.Count}",
                    (float)i / root.items.Count);

                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.name) ||
                    string.IsNullOrWhiteSpace(entry.image))
                {
                    skipped++;
                    continue;
                }

                if (ShouldSkipCollection(entry.name))
                {
                    skipped++;
                    continue;
                }

                string collectionAssetPath =
                    $"{CollectionDataFolder}/{BuildSafeFileName(entry.name)}.asset";

                CollectionData collection =
                    AssetDatabase.LoadAssetAtPath<CollectionData>(collectionAssetPath);

                if (collection == null)
                {
                    notFound++;
                    continue;
                }

                string imageAssetPath =
                    $"{CollectionOutputFolder}/{BuildSafeFileName(entry.name)}.png";

                if (!File.Exists(imageAssetPath))
                {
                    if (!DownloadFile(entry.image, imageAssetPath))
                    {
                        skipped++;
                        continue;
                    }

                    downloaded++;
                }

                Sprite sprite = ImportAsSprite(imageAssetPath);

                if (sprite != null && collection.icon != sprite)
                {
                    collection.icon = sprite;
                    EditorUtility.SetDirty(collection);
                    assigned++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Collection image import complete: {downloaded} downloaded, " +
            $"{assigned} assigned, {notFound} collections not found, {skipped} skipped.");
    }

    static bool DownloadFile(string url, string outputAssetPath)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                // Blocking in editor is okay for this importer.
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Image download failed: {url} / {request.error}");
                return false;
            }

            File.WriteAllBytes(outputAssetPath, request.downloadHandler.data);
            return true;
        }
    }

    static Sprite ImportAsSprite(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath);

        TextureImporter importer =
            AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer == null)
        {
            Debug.LogWarning($"Could not import as texture: {assetPath}");
            return null;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    static void EnsureFolder(string folder)
    {
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
    }

    static bool ShouldSkipCollection(string name)
    {
        return name.Contains("Graffiti") ||
               name.Contains("Sticker") ||
               name.Contains("Charm") ||
               name.Contains("X-Ray");
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