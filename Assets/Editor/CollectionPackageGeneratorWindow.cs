#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class CollectionPackageGeneratorWindow : EditorWindow
{
    private const string DefaultCollectionOutputFolder =
        "Assets/Data/Cases/CollectionPackages";

    private const string DefaultSouvenirOutputFolder =
        "Assets/Data/Cases/SouvenirPackages";

    private const string CollectionPackagePrefix =
        "cc_collection_package_";

    private const string SouvenirPackagePrefix =
        "cc_souvenir_package_";

    [SerializeField] private GameDatabase database;

    [Header("Templates")]
    [SerializeField] private CaseData collectionPackageTemplate;
    [SerializeField] private CaseData souvenirPackageTemplate;

    [Header("Output")]
    [SerializeField] private string collectionOutputFolder =
        DefaultCollectionOutputFolder;

    [SerializeField] private string souvenirOutputFolder =
        DefaultSouvenirOutputFolder;

    [SerializeField]
    private bool overwriteExistingSettingsFromTemplates;

    [MenuItem("Case Curator/Containers/Generate Collection Packages")]
    public static void OpenWindow()
    {
        CollectionPackageGeneratorWindow window =
            GetWindow<CollectionPackageGeneratorWindow>();

        window.titleContent = new GUIContent("Collection Packages");
        window.minSize = new Vector2(590f, 500f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Collection and Souvenir Package Generator",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "CollectionData.type is authoritative. Collection and " +
            "SouvenirCollection entries both receive a normal openable " +
            "Collection Package on the Collections shop page. " +
            "SouvenirCollection entries also receive a separate Souvenir " +
            "Package on the Souvenir shop page. Case entries are skipped.",
            MessageType.Info);

        EditorGUILayout.Space(8f);

        database = (GameDatabase)EditorGUILayout.ObjectField(
            "Game Database",
            database,
            typeof(GameDatabase),
            false);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Templates", EditorStyles.boldLabel);

        collectionPackageTemplate =
            (CaseData)EditorGUILayout.ObjectField(
                "Collection Package Template",
                collectionPackageTemplate,
                typeof(CaseData),
                false);

        souvenirPackageTemplate =
            (CaseData)EditorGUILayout.ObjectField(
                "Souvenir Package Template",
                souvenirPackageTemplate,
                typeof(CaseData),
                false);

        EditorGUILayout.HelpBox(
            "Use a working normal collection package for the first template " +
            "and a working souvenir package for the second. The generator " +
            "overrides identity, package type, shop category and Souvenir / " +
            "StatTrak rules, while the templates provide balancing defaults.",
            MessageType.None);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Output Folders", EditorStyles.boldLabel);

        collectionOutputFolder = EditorGUILayout.TextField(
            "Collection Packages",
            collectionOutputFolder);

        souvenirOutputFolder = EditorGUILayout.TextField(
            "Souvenir Packages",
            souvenirOutputFolder);

        overwriteExistingSettingsFromTemplates =
            EditorGUILayout.ToggleLeft(
                "Overwrite existing prices, ranks, quality and rarity chances",
                overwriteExistingSettingsFromTemplates);

        EditorGUILayout.Space(12f);

        bool canGenerate = database != null &&
                           collectionPackageTemplate != null &&
                           souvenirPackageTemplate != null;

        using (new EditorGUI.DisabledScope(!canGenerate))
        {
            if (GUILayout.Button(
                    "Generate / Update All Collection Packages",
                    GUILayout.Height(42f)))
            {
                GeneratePackages();
            }
        }

        if (!canGenerate)
        {
            EditorGUILayout.HelpBox(
                "Assign the GameDatabase and both templates before generating.",
                MessageType.Warning);
        }

        EditorGUILayout.Space(10f);

        using (new EditorGUI.DisabledScope(database == null))
        {
            if (GUILayout.Button(
                    "Remove Generated Packages With Invalid Source Types",
                    GUILayout.Height(32f)))
            {
                RemoveInvalidGeneratedPackages();
            }
        }

        EditorGUILayout.HelpBox(
            "Cleanup only deletes generator-owned assets. A normal generated " +
            "package is valid for Collection or SouvenirCollection. A generated " +
            "souvenir package is valid only for SouvenirCollection. Assets " +
            "without the generator prefixes are never deleted.",
            MessageType.Warning);
    }

    private void GeneratePackages()
    {
        if (database == null ||
            collectionPackageTemplate == null ||
            souvenirPackageTemplate == null)
        {
            return;
        }

        if (database.allCollections == null || database.allSkins == null)
        {
            EditorUtility.DisplayDialog(
                "Collection Package Generator",
                "The GameDatabase collection or skin list is unavailable.",
                "OK");
            return;
        }

        collectionOutputFolder = NormalizeOutputFolder(
            collectionOutputFolder,
            DefaultCollectionOutputFolder);

        souvenirOutputFolder = NormalizeOutputFolder(
            souvenirOutputFolder,
            DefaultSouvenirOutputFolder);

        EnsureFolderExists(collectionOutputFolder);
        EnsureFolderExists(souvenirOutputFolder);

        GenerationCounters counters = new GenerationCounters();
        Undo.RecordObject(database, "Generate Collection Packages");

        for (int i = 0; i < database.allCollections.Count; i++)
        {
            CollectionData collection = database.allCollections[i];

            if (collection == null)
                continue;

            if (collection.type == CollectionType.Case)
            {
                counters.skippedCaseCollections++;
                continue;
            }

            bool isNormalCollection =
                collection.type == CollectionType.Collection;

            bool isSouvenirCollection =
                collection.type == CollectionType.SouvenirCollection;

            if (!isNormalCollection && !isSouvenirCollection)
                continue;

            List<SkinData> skins = GetCollectionSkins(collection);

            if (skins.Count == 0)
            {
                counters.skippedEmptyCollections++;
                continue;
            }

            CreateOrUpdatePackage(
                collection,
                skins,
                false,
                collectionPackageTemplate,
                collectionOutputFolder,
                counters);

            if (isSouvenirCollection)
            {
                CreateOrUpdatePackage(
                    collection,
                    skins,
                    true,
                    souvenirPackageTemplate,
                    souvenirOutputFolder,
                    counters);
            }
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Collection Packages Generated",
            $"Normal packages created: {counters.normalCreated}\n" +
            $"Normal packages updated: {counters.normalUpdated}\n" +
            $"Souvenir packages created: {counters.souvenirCreated}\n" +
            $"Souvenir packages updated: {counters.souvenirUpdated}\n" +
            $"Registered in GameDatabase: {counters.registered}\n" +
            $"Skipped Case collections: {counters.skippedCaseCollections}\n" +
            $"Skipped empty collections: {counters.skippedEmptyCollections}",
            "OK");
    }

    private void CreateOrUpdatePackage(
        CollectionData collection,
        List<SkinData> skins,
        bool souvenir,
        CaseData template,
        string outputFolder,
        GenerationCounters counters)
    {
        string generatedId = BuildPackageId(collection, souvenir);
        CaseContainerType containerType = souvenir
            ? CaseContainerType.SouvenirPackage
            : CaseContainerType.CollectionPackage;

        CaseShopCategory shopCategory = souvenir
            ? CaseShopCategory.SouvenirCollections
            : CaseShopCategory.Collections;

        CaseData package = FindExistingPackage(
            collection,
            generatedId,
            containerType,
            shopCategory,
            souvenir);

        bool isNew = package == null;

        if (isNew)
        {
            package = CreateInstance<CaseData>();
            EditorUtility.CopySerialized(template, package);

            string fileName = MakeSafeFileName(
                GetPackageDisplayName(collection, souvenir)) +
                ".asset";

            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(outputFolder, fileName)
                    .Replace("\\", "/"));

            AssetDatabase.CreateAsset(package, path);

            if (souvenir)
                counters.souvenirCreated++;
            else
                counters.normalCreated++;
        }
        else
        {
            Undo.RecordObject(package, "Update Collection Package");

            if (overwriteExistingSettingsFromTemplates &&
                package != template)
            {
                EditorUtility.CopySerialized(template, package);
            }

            if (souvenir)
                counters.souvenirUpdated++;
            else
                counters.normalUpdated++;
        }

        ApplyIdentityAndRules(
            package,
            collection,
            generatedId,
            souvenir);

        ReplaceDropPool(package, skins);
        RegisterPackage(package, counters);
        EditorUtility.SetDirty(package);
    }

    private void RegisterPackage(
        CaseData package,
        GenerationCounters counters)
    {
        if (database.allCases == null)
            database.allCases = new List<CaseData>();

        if (database.allCases.Contains(package))
            return;

        database.allCases.Add(package);
        counters.registered++;
    }

    private void RemoveInvalidGeneratedPackages()
    {
        if (database == null || database.allCases == null)
            return;

        List<CaseData> toDelete = new List<CaseData>();

        for (int i = 0; i < database.allCases.Count; i++)
        {
            CaseData package = database.allCases[i];

            if (!IsGeneratorOwnedPackage(package))
                continue;

            CollectionData source =
                GetPrimaryCollectionFromPackage(package);

            bool normalPackage = HasPrefix(
                package,
                CollectionPackagePrefix);

            bool souvenirPackage = HasPrefix(
                package,
                SouvenirPackagePrefix);

            bool validNormal = normalPackage &&
                source != null &&
                (source.type == CollectionType.Collection ||
                 source.type == CollectionType.SouvenirCollection);

            bool validSouvenir = souvenirPackage &&
                source != null &&
                source.type == CollectionType.SouvenirCollection;

            if (!validNormal && !validSouvenir)
                toDelete.Add(package);
        }

        if (toDelete.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Collection Package Cleanup",
                "No invalid generated packages were found.",
                "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Remove Invalid Generated Packages",
            $"Delete {toDelete.Count} generator-owned package assets whose " +
            "source type no longer matches their generated package type?\n\n" +
            "Manually created assets are not affected.",
            "Delete",
            "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(database, "Remove Invalid Collection Packages");
        int deleted = 0;

        for (int i = 0; i < toDelete.Count; i++)
        {
            CaseData package = toDelete[i];
            database.allCases.Remove(package);

            string path = AssetDatabase.GetAssetPath(package);

            if (!string.IsNullOrWhiteSpace(path) &&
                AssetDatabase.DeleteAsset(path))
            {
                deleted++;
            }
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Collection Package Cleanup",
            $"Deleted {deleted} invalid generated packages.",
            "OK");
    }

    private List<SkinData> GetCollectionSkins(
        CollectionData collection)
    {
        List<SkinData> result = new List<SkinData>();

        for (int i = 0; i < database.allSkins.Count; i++)
        {
            SkinData skin = database.allSkins[i];

            if (skin != null &&
                SameCollection(skin.collectionData, collection))
            {
                result.Add(skin);
            }
        }

        result.Sort((a, b) =>
        {
            int rarityCompare = a.rarity.CompareTo(b.rarity);

            if (rarityCompare != 0)
                return rarityCompare;

            int weaponCompare = string.Compare(
                a.weaponName,
                b.weaponName,
                StringComparison.OrdinalIgnoreCase);

            return weaponCompare != 0
                ? weaponCompare
                : string.Compare(
                    a.skinName,
                    b.skinName,
                    StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    private CaseData FindExistingPackage(
        CollectionData collection,
        string generatedId,
        CaseContainerType containerType,
        CaseShopCategory shopCategory,
        bool souvenir)
    {
        if (database.allCases != null)
        {
            for (int i = 0; i < database.allCases.Count; i++)
            {
                CaseData existing = database.allCases[i];

                if (IsMatchingPackage(
                        existing,
                        collection,
                        generatedId,
                        containerType,
                        shopCategory,
                        souvenir))
                {
                    return existing;
                }
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:CaseData");

        for (int i = 0; i < guids.Length; i++)
        {
            CaseData existing = AssetDatabase.LoadAssetAtPath<CaseData>(
                AssetDatabase.GUIDToAssetPath(guids[i]));

            if (IsMatchingPackage(
                    existing,
                    collection,
                    generatedId,
                    containerType,
                    shopCategory,
                    souvenir))
            {
                return existing;
            }
        }

        return null;
    }

    private static bool IsMatchingPackage(
        CaseData existing,
        CollectionData collection,
        string generatedId,
        CaseContainerType containerType,
        CaseShopCategory shopCategory,
        bool souvenir)
    {
        if (existing == null)
            return false;

        if (string.Equals(
                existing.apiId,
                generatedId,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (existing.containerType != containerType ||
            existing.shopCategory != shopCategory)
        {
            return false;
        }

        string expectedName = GetPackageDisplayName(
            collection,
            souvenir);

        if (string.Equals(
                NormalizeName(existing.caseName),
                NormalizeName(expectedName),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return PackageUsesCollection(existing, collection);
    }

    private static bool PackageUsesCollection(
        CaseData package,
        CollectionData collection)
    {
        SerializedObject serializedPackage =
            new SerializedObject(package);

        SerializedProperty dropPool =
            serializedPackage.FindProperty("dropPool");

        if (dropPool == null || !dropPool.isArray)
            return false;

        for (int i = 0; i < dropPool.arraySize; i++)
        {
            SerializedProperty entry =
                dropPool.GetArrayElementAtIndex(i);

            SerializedProperty skinProperty =
                entry.FindPropertyRelative("skin");

            SkinData skin = skinProperty != null
                ? skinProperty.objectReferenceValue as SkinData
                : null;

            if (skin != null &&
                SameCollection(skin.collectionData, collection))
            {
                return true;
            }
        }

        return false;
    }

    private static CollectionData GetPrimaryCollectionFromPackage(
        CaseData package)
    {
        if (package == null)
            return null;

        SerializedObject serializedPackage =
            new SerializedObject(package);

        SerializedProperty dropPool =
            serializedPackage.FindProperty("dropPool");

        if (dropPool == null || !dropPool.isArray)
            return null;

        for (int i = 0; i < dropPool.arraySize; i++)
        {
            SerializedProperty entry =
                dropPool.GetArrayElementAtIndex(i);

            SerializedProperty skinProperty =
                entry.FindPropertyRelative("skin");

            SkinData skin = skinProperty != null
                ? skinProperty.objectReferenceValue as SkinData
                : null;

            if (skin != null && skin.collectionData != null)
                return skin.collectionData;
        }

        return null;
    }

    private static bool IsGeneratorOwnedPackage(CaseData package)
    {
        return HasPrefix(package, CollectionPackagePrefix) ||
               HasPrefix(package, SouvenirPackagePrefix);
    }

    private static bool HasPrefix(
        CaseData package,
        string prefix)
    {
        return package != null &&
               !string.IsNullOrWhiteSpace(package.apiId) &&
               package.apiId.StartsWith(
                   prefix,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyIdentityAndRules(
        CaseData package,
        CollectionData collection,
        string generatedId,
        bool souvenir)
    {
        package.apiId = generatedId;
        package.caseName = GetPackageDisplayName(collection, souvenir);
        package.icon = collection.icon;
        package.containerType = souvenir
            ? CaseContainerType.SouvenirPackage
            : CaseContainerType.CollectionPackage;

        package.allowRareSpecialItem = false;
        package.allowStatTrak = false;
        package.forceSouvenirDrops = souvenir;
        package.shopCategory = souvenir
            ? CaseShopCategory.SouvenirCollections
            : CaseShopCategory.Collections;

        package.isCustomCase = false;
        package.shouldHaveRareSpecial = false;
    }

    private static void ReplaceDropPool(
        CaseData package,
        List<SkinData> skins)
    {
        SerializedObject serializedPackage =
            new SerializedObject(package);

        SerializedProperty dropPool =
            serializedPackage.FindProperty("dropPool");

        if (dropPool == null || !dropPool.isArray)
        {
            Debug.LogError(
                $"{package.name}: CaseData dropPool property was not found.",
                package);
            return;
        }

        dropPool.arraySize = skins.Count;

        for (int i = 0; i < skins.Count; i++)
        {
            SerializedProperty entry =
                dropPool.GetArrayElementAtIndex(i);

            SerializedProperty skinProperty =
                entry.FindPropertyRelative("skin");

            SerializedProperty weightProperty =
                entry.FindPropertyRelative("weight");

            if (skinProperty != null)
                skinProperty.objectReferenceValue = skins[i];

            if (weightProperty != null)
                weightProperty.floatValue = 1f;
        }

        serializedPackage.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool SameCollection(
        CollectionData first,
        CollectionData second)
    {
        if (first == second)
            return true;

        if (first == null || second == null)
            return false;

        return !string.IsNullOrWhiteSpace(first.apiId) &&
               !string.IsNullOrWhiteSpace(second.apiId) &&
               string.Equals(
                   first.apiId.Trim(),
                   second.apiId.Trim(),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildPackageId(
        CollectionData collection,
        bool souvenir)
    {
        string source = !string.IsNullOrWhiteSpace(collection.apiId)
            ? collection.apiId
            : collection.collectionName;

        return (souvenir
                ? SouvenirPackagePrefix
                : CollectionPackagePrefix) +
               Slugify(source);
    }

    private static string GetPackageDisplayName(
        CollectionData collection,
        bool souvenir)
    {
        string collectionName = GetCollectionDisplayName(collection);

        return souvenir
            ? collectionName + " Souvenir Package"
            : collectionName;
    }

    private static string GetCollectionDisplayName(
        CollectionData collection)
    {
        return !string.IsNullOrWhiteSpace(collection.collectionName)
            ? collection.collectionName.Trim()
            : collection.name;
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        value = value.Trim();

        string[] suffixes =
        {
            " Souvenir Package",
            " Collection Package",
            " Package"
        };

        for (int i = 0; i < suffixes.Length; i++)
        {
            string suffix = suffixes[i];

            if (value.EndsWith(
                    suffix,
                    StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(
                    0,
                    value.Length - suffix.Length).Trim();
                break;
            }
        }

        return value;
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unnamed";

        StringBuilder builder = new StringBuilder();
        bool previousUnderscore = false;

        foreach (char character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousUnderscore = false;
            }
            else if (!previousUnderscore)
            {
                builder.Append('_');
                previousUnderscore = true;
            }
        }

        return builder.ToString().Trim('_');
    }

    private static string MakeSafeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Collection Package";

        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');

        return value.Trim();
    }

    private static string NormalizeOutputFolder(
        string folderPath,
        string fallback)
    {
        string normalized = string.IsNullOrWhiteSpace(folderPath)
            ? fallback
            : folderPath.Replace("\\", "/").TrimEnd('/');

        if (!normalized.StartsWith("Assets", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Output folder must be inside the Assets folder.");
        }

        return normalized;
    }

    private static void EnsureFolderExists(string folderPath)
    {
        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    private sealed class GenerationCounters
    {
        public int normalCreated;
        public int normalUpdated;
        public int souvenirCreated;
        public int souvenirUpdated;
        public int registered;
        public int skippedCaseCollections;
        public int skippedEmptyCollections;
    }
}
#endif
