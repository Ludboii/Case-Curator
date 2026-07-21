#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class CollectionPackageGeneratorWindow : EditorWindow
{
    private const string DefaultOutputFolder =
        "Assets/Data/Cases/CollectionPackages";

    private const string GeneratedPackagePrefix =
        "cc_collection_package_";

    [SerializeField] private GameDatabase database;
    [SerializeField] private CaseData templatePackage;
    [SerializeField] private string outputFolder = DefaultOutputFolder;
    [SerializeField] private bool overwriteExistingSettingsFromTemplate;

    [MenuItem("Case Curator/Containers/Generate Collection Packages")]
    public static void OpenWindow()
    {
        CollectionPackageGeneratorWindow window =
            GetWindow<CollectionPackageGeneratorWindow>();

        window.titleContent = new GUIContent("Collection Packages");
        window.minSize = new Vector2(560f, 390f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Collection Package Generator",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "CollectionData.type is authoritative. This tool creates normal " +
            "openable package assets only for entries marked Collection. " +
            "Entries marked Case are skipped. Entries marked " +
            "SouvenirCollection are also skipped for now so their souvenir " +
            "packages can be configured separately.",
            MessageType.Info);

        EditorGUILayout.Space(8f);

        database = (GameDatabase)EditorGUILayout.ObjectField(
            "Game Database",
            database,
            typeof(GameDatabase),
            false);

        templatePackage = (CaseData)EditorGUILayout.ObjectField(
            "Template Package",
            templatePackage,
            typeof(CaseData),
            false);

        outputFolder = EditorGUILayout.TextField(
            "Output Folder",
            outputFolder);

        overwriteExistingSettingsFromTemplate =
            EditorGUILayout.ToggleLeft(
                "Overwrite existing prices, ranks, quality and rarity chances",
                overwriteExistingSettingsFromTemplate);

        EditorGUILayout.Space(8f);

        EditorGUILayout.HelpBox(
            "Use a working normal Collection Package as the template. Skin " +
            "membership is generated from SkinData.collectionData. Existing " +
            "manual prices and ranks are preserved while overwrite is disabled.",
            MessageType.None);

        EditorGUILayout.Space(12f);

        using (new EditorGUI.DisabledScope(
                   database == null || templatePackage == null))
        {
            if (GUILayout.Button(
                    "Generate / Update Collection Packages",
                    GUILayout.Height(38f)))
            {
                GeneratePackages();
            }
        }

        EditorGUILayout.Space(8f);

        using (new EditorGUI.DisabledScope(database == null))
        {
            if (GUILayout.Button(
                    "Remove Generated Non-Collection Packages",
                    GUILayout.Height(32f)))
            {
                RemoveGeneratedNonCollectionPackages();
            }
        }

        EditorGUILayout.HelpBox(
            "Cleanup only removes generator-created assets whose source " +
            "CollectionData is no longer marked Collection. Manually created " +
            "assets without the generator API prefix are never deleted.",
            MessageType.Warning);
    }

    private void GeneratePackages()
    {
        if (database == null || templatePackage == null)
            return;

        if (database.allCollections == null || database.allSkins == null)
        {
            EditorUtility.DisplayDialog(
                "Collection Package Generator",
                "The GameDatabase collection or skin list is unavailable.",
                "OK");
            return;
        }

        outputFolder = NormalizeOutputFolder(outputFolder);
        EnsureFolderExists(outputFolder);

        int created = 0;
        int updated = 0;
        int registered = 0;
        int skippedCase = 0;
        int skippedSouvenir = 0;
        int skippedEmpty = 0;

        Undo.RecordObject(database, "Generate Collection Packages");

        for (int i = 0; i < database.allCollections.Count; i++)
        {
            CollectionData collection = database.allCollections[i];

            if (collection == null)
                continue;

            if (collection.type == CollectionType.Case)
            {
                skippedCase++;
                continue;
            }

            if (collection.type == CollectionType.SouvenirCollection)
            {
                skippedSouvenir++;
                continue;
            }

            if (collection.type != CollectionType.Collection)
                continue;

            List<SkinData> skins = GetCollectionSkins(collection);

            if (skins.Count == 0)
            {
                skippedEmpty++;
                continue;
            }

            string generatedId = BuildPackageId(collection);
            CaseData package = FindExistingPackage(
                collection,
                generatedId);

            bool isNew = package == null;

            if (isNew)
            {
                package = CreateInstance<CaseData>();
                EditorUtility.CopySerialized(templatePackage, package);

                string fileName =
                    MakeSafeFileName(GetCollectionDisplayName(collection)) +
                    " Package.asset";

                string path = AssetDatabase.GenerateUniqueAssetPath(
                    Path.Combine(outputFolder, fileName)
                        .Replace("\\", "/"));

                AssetDatabase.CreateAsset(package, path);
                created++;
            }
            else
            {
                Undo.RecordObject(package, "Update Collection Package");

                if (overwriteExistingSettingsFromTemplate &&
                    package != templatePackage)
                {
                    EditorUtility.CopySerialized(templatePackage, package);
                }

                updated++;
            }

            ApplyCollectionIdentityAndRules(
                package,
                collection,
                generatedId);

            ReplaceDropPool(package, skins);

            if (database.allCases == null)
                database.allCases = new List<CaseData>();

            if (!database.allCases.Contains(package))
            {
                database.allCases.Add(package);
                registered++;
            }

            EditorUtility.SetDirty(package);
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Collection Packages Generated",
            $"Created: {created}\n" +
            $"Updated: {updated}\n" +
            $"Registered in GameDatabase: {registered}\n" +
            $"Skipped Case entries: {skippedCase}\n" +
            $"Skipped SouvenirCollection entries: {skippedSouvenir}\n" +
            $"Skipped empty collections: {skippedEmpty}",
            "OK");
    }

    private void RemoveGeneratedNonCollectionPackages()
    {
        if (database == null || database.allCases == null)
            return;

        List<CaseData> toDelete = new List<CaseData>();

        for (int i = 0; i < database.allCases.Count; i++)
        {
            CaseData package = database.allCases[i];

            if (!IsGeneratorOwnedPackage(package))
                continue;

            CollectionData source = GetPrimaryCollectionFromPackage(package);

            if (source == null || source.type != CollectionType.Collection)
                toDelete.Add(package);
        }

        if (toDelete.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Collection Package Cleanup",
                "No generated non-Collection packages were found.",
                "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Remove Generated Non-Collection Packages",
            $"Delete {toDelete.Count} generator-created package assets whose " +
            "source is not marked Collection?\n\nManually created assets are " +
            "not affected.",
            "Delete",
            "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(database, "Remove Generated Non-Collection Packages");
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
            $"Deleted {deleted} generated non-Collection packages.",
            "OK");
    }

    private List<SkinData> GetCollectionSkins(CollectionData collection)
    {
        List<SkinData> result = new List<SkinData>();

        for (int i = 0; i < database.allSkins.Count; i++)
        {
            SkinData skin = database.allSkins[i];

            if (skin != null && SameCollection(skin.collectionData, collection))
                result.Add(skin);
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
        string generatedId)
    {
        if (database.allCases != null)
        {
            for (int i = 0; i < database.allCases.Count; i++)
            {
                CaseData existing = database.allCases[i];

                if (IsMatchingPackage(existing, collection, generatedId))
                    return existing;
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:CaseData");

        for (int i = 0; i < guids.Length; i++)
        {
            CaseData existing = AssetDatabase.LoadAssetAtPath<CaseData>(
                AssetDatabase.GUIDToAssetPath(guids[i]));

            if (IsMatchingPackage(existing, collection, generatedId))
                return existing;
        }

        return null;
    }

    private static bool IsMatchingPackage(
        CaseData existing,
        CollectionData collection,
        string generatedId)
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

        if (existing.containerType != CaseContainerType.CollectionPackage &&
            existing.shopCategory != CaseShopCategory.Collections)
        {
            return false;
        }

        if (string.Equals(
                NormalizeName(existing.caseName),
                NormalizeName(GetCollectionDisplayName(collection)),
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
        SerializedObject serializedPackage = new SerializedObject(package);
        SerializedProperty dropPool = serializedPackage.FindProperty("dropPool");

        if (dropPool == null || !dropPool.isArray)
            return false;

        for (int i = 0; i < dropPool.arraySize; i++)
        {
            SerializedProperty entry = dropPool.GetArrayElementAtIndex(i);
            SerializedProperty skinProperty = entry.FindPropertyRelative("skin");
            SkinData skin = skinProperty != null
                ? skinProperty.objectReferenceValue as SkinData
                : null;

            if (skin != null && SameCollection(skin.collectionData, collection))
                return true;
        }

        return false;
    }

    private static CollectionData GetPrimaryCollectionFromPackage(
        CaseData package)
    {
        if (package == null)
            return null;

        SerializedObject serializedPackage = new SerializedObject(package);
        SerializedProperty dropPool = serializedPackage.FindProperty("dropPool");

        if (dropPool == null || !dropPool.isArray)
            return null;

        for (int i = 0; i < dropPool.arraySize; i++)
        {
            SerializedProperty entry = dropPool.GetArrayElementAtIndex(i);
            SerializedProperty skinProperty = entry.FindPropertyRelative("skin");
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
        return package != null &&
               !string.IsNullOrWhiteSpace(package.apiId) &&
               package.apiId.StartsWith(
                   GeneratedPackagePrefix,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyCollectionIdentityAndRules(
        CaseData package,
        CollectionData collection,
        string generatedId)
    {
        package.apiId = generatedId;
        package.caseName = GetCollectionDisplayName(collection);
        package.icon = collection.icon;
        package.containerType = CaseContainerType.CollectionPackage;
        package.allowRareSpecialItem = false;
        package.allowStatTrak = false;
        package.forceSouvenirDrops = false;
        package.shopCategory = CaseShopCategory.Collections;
        package.isCustomCase = false;
        package.shouldHaveRareSpecial = false;
    }

    private static void ReplaceDropPool(
        CaseData package,
        List<SkinData> skins)
    {
        SerializedObject serializedPackage = new SerializedObject(package);
        SerializedProperty dropPool = serializedPackage.FindProperty("dropPool");

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
            SerializedProperty entry = dropPool.GetArrayElementAtIndex(i);
            SerializedProperty skinProperty = entry.FindPropertyRelative("skin");
            SerializedProperty weightProperty = entry.FindPropertyRelative("weight");

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

    private static string BuildPackageId(CollectionData collection)
    {
        string source = !string.IsNullOrWhiteSpace(collection.apiId)
            ? collection.apiId
            : collection.collectionName;

        return GeneratedPackagePrefix + Slugify(source);
    }

    private static string GetCollectionDisplayName(CollectionData collection)
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

        if (value.EndsWith(" Package", StringComparison.OrdinalIgnoreCase))
            value = value.Substring(0, value.Length - 8).Trim();

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
            return "Collection";

        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');

        return value.Trim();
    }

    private static string NormalizeOutputFolder(string folderPath)
    {
        string normalized = string.IsNullOrWhiteSpace(folderPath)
            ? DefaultOutputFolder
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
}
#endif
