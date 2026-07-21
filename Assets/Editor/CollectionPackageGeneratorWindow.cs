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
        window.minSize = new Vector2(560f, 420f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Collection Package Generator",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "The imported CollectionData Type field is not reliable enough to " +
            "distinguish case collections from standalone map collections. " +
            "This tool now infers case collections from the drop pools of real " +
            "WeaponCase assets and only generates packages for collections that " +
            "are not used by a weapon case.",
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
            "Use your working The Lake Collection asset as the template. " +
            "Generated packages receive their skins from SkinData.collectionData. " +
            "Existing package prices and ranks are preserved while overwrite is disabled.",
            MessageType.None);

        EditorGUILayout.Space(12f);

        using (new EditorGUI.DisabledScope(
                   database == null || templatePackage == null))
        {
            if (GUILayout.Button(
                    "Generate / Update Standalone Collection Packages",
                    GUILayout.Height(38f)))
            {
                GeneratePackages();
            }
        }

        EditorGUILayout.Space(8f);

        using (new EditorGUI.DisabledScope(database == null))
        {
            if (GUILayout.Button(
                    "Remove Incorrect Generated Case-Collection Packages",
                    GUILayout.Height(32f)))
            {
                RemoveIncorrectGeneratedPackages();
            }

            if (GUILayout.Button(
                    "Repair Collection Type Labels",
                    GUILayout.Height(32f)))
            {
                RepairCollectionTypeLabels();
            }
        }

        EditorGUILayout.HelpBox(
            "Cleanup only deletes assets created by this generator whose API ID " +
            "starts with 'cc_collection_package_' and whose skins belong to a " +
            "real weapon case. Manually created packages without that prefix are untouched.",
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

        List<CollectionData> caseCollections =
            GetCollectionsUsedByWeaponCases();

        int created = 0;
        int updated = 0;
        int registered = 0;
        int skippedCaseCollections = 0;
        int skippedEmpty = 0;

        Undo.RecordObject(database, "Generate Collection Packages");

        for (int i = 0; i < database.allCollections.Count; i++)
        {
            CollectionData collection = database.allCollections[i];

            if (collection == null)
                continue;

            if (ContainsCollection(caseCollections, collection))
            {
                skippedCaseCollections++;
                continue;
            }

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
            "Standalone Collection Packages Generated",
            $"Created: {created}\n" +
            $"Updated: {updated}\n" +
            $"Registered in GameDatabase: {registered}\n" +
            $"Skipped weapon-case collections: {skippedCaseCollections}\n" +
            $"Skipped empty collections: {skippedEmpty}",
            "OK");
    }

    private void RemoveIncorrectGeneratedPackages()
    {
        if (database == null || database.allCases == null)
            return;

        List<CollectionData> caseCollections =
            GetCollectionsUsedByWeaponCases();

        List<CaseData> toDelete = new List<CaseData>();

        for (int i = 0; i < database.allCases.Count; i++)
        {
            CaseData package = database.allCases[i];

            if (!IsGeneratorOwnedPackage(package))
                continue;

            CollectionData sourceCollection =
                GetPrimaryCollectionFromPackage(package);

            if (sourceCollection != null &&
                ContainsCollection(caseCollections, sourceCollection))
            {
                toDelete.Add(package);
            }
        }

        if (toDelete.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Collection Package Cleanup",
                "No incorrect generated case-collection packages were found.",
                "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Remove Incorrect Generated Packages",
            $"Delete {toDelete.Count} generator-created packages that belong " +
            "to real weapon cases?\n\nManually created packages without the " +
            "generator API prefix will not be touched.",
            "Delete",
            "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(database, "Remove Incorrect Collection Packages");

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
            $"Deleted {deleted} incorrect generated packages.",
            "OK");
    }

    private void RepairCollectionTypeLabels()
    {
        if (database == null || database.allCollections == null)
            return;

        List<CollectionData> caseCollections =
            GetCollectionsUsedByWeaponCases();

        int markedCase = 0;
        int markedCollection = 0;

        for (int i = 0; i < database.allCollections.Count; i++)
        {
            CollectionData collection = database.allCollections[i];

            if (collection == null)
                continue;

            Undo.RecordObject(collection, "Repair Collection Type");

            CollectionType desiredType =
                ContainsCollection(caseCollections, collection)
                    ? CollectionType.Case
                    : CollectionType.Collection;

            if (collection.type == desiredType)
                continue;

            collection.type = desiredType;
            EditorUtility.SetDirty(collection);

            if (desiredType == CollectionType.Case)
                markedCase++;
            else
                markedCollection++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Collection Type Labels Repaired",
            $"Marked as Case: {markedCase}\n" +
            $"Marked as Collection: {markedCollection}\n\n" +
            "Souvenir packages should reference the same underlying map " +
            "collection rather than requiring a separate collection type.",
            "OK");
    }

    private List<CollectionData> GetCollectionsUsedByWeaponCases()
    {
        List<CollectionData> result = new List<CollectionData>();

        if (database == null || database.allCases == null)
            return result;

        for (int i = 0; i < database.allCases.Count; i++)
        {
            CaseData container = database.allCases[i];

            if (container == null ||
                container.containerType != CaseContainerType.WeaponCase)
            {
                continue;
            }

            AddCollectionsFromContainer(container, result);
        }

        return result;
    }

    private static void AddCollectionsFromContainer(
        CaseData container,
        List<CollectionData> target)
    {
        SerializedObject serializedContainer =
            new SerializedObject(container);

        SerializedProperty dropPool =
            serializedContainer.FindProperty("dropPool");

        if (dropPool == null || !dropPool.isArray)
            return;

        for (int i = 0; i < dropPool.arraySize; i++)
        {
            SerializedProperty entry =
                dropPool.GetArrayElementAtIndex(i);

            SerializedProperty skinProperty =
                entry.FindPropertyRelative("skin");

            SkinData skin = skinProperty != null
                ? skinProperty.objectReferenceValue as SkinData
                : null;

            CollectionData collection = skin != null
                ? skin.collectionData
                : null;

            if (collection != null &&
                !ContainsCollection(target, collection))
            {
                target.Add(collection);
            }
        }
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
        string generatedId)
    {
        if (database.allCases != null)
        {
            for (int i = 0; i < database.allCases.Count; i++)
            {
                CaseData existing = database.allCases[i];

                if (IsMatchingPackage(
                        existing,
                        collection,
                        generatedId))
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
                    generatedId))
            {
                return existing;
            }
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

    private static bool ContainsCollection(
        List<CollectionData> collections,
        CollectionData target)
    {
        if (collections == null || target == null)
            return false;

        for (int i = 0; i < collections.Count; i++)
        {
            if (SameCollection(collections[i], target))
                return true;
        }

        return false;
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
        CollectionData collection)
    {
        string source = !string.IsNullOrWhiteSpace(collection.apiId)
            ? collection.apiId
            : collection.collectionName;

        return GeneratedPackagePrefix + Slugify(source);
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

        if (value.EndsWith(
                " Package",
                StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(0, value.Length - 8).Trim();
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
