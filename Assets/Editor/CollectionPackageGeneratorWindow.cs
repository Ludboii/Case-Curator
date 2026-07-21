#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class CollectionPackageGeneratorWindow : EditorWindow
{
    [SerializeField] private GameDatabase database;
    [SerializeField] private CaseData templatePackage;
    [SerializeField] private string outputFolder =
        "Assets/Data/Cases/CollectionPackages";

    [SerializeField]
    private bool overwriteExistingSettingsFromTemplate;

    [MenuItem("Case Curator/Containers/Generate Collection Packages")]
    public static void OpenWindow()
    {
        CollectionPackageGeneratorWindow window =
            GetWindow<CollectionPackageGeneratorWindow>();

        window.titleContent =
            new GUIContent("Collection Packages");

        window.minSize = new Vector2(520f, 330f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Collection Package Generator",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Creates or updates one CollectionPackage CaseData asset for every " +
            "CollectionData with type Collection. The package drop pool is " +
            "generated from all SkinData assets linked to that collection. " +
            "Shop settings and rarity chances are copied from the template.",
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
                "Overwrite price, rank, quality and rarity chances on existing packages",
                overwriteExistingSettingsFromTemplate);

        EditorGUILayout.Space(8f);

        EditorGUILayout.HelpBox(
            "Recommended template: your working The Lake Collection package. " +
            "New assets copy its rarity chances and shop defaults. Existing " +
            "packages keep their manually edited prices and ranks unless the " +
            "overwrite option is enabled.",
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
    }

    private void GeneratePackages()
    {
        if (database == null || templatePackage == null)
        {
            EditorUtility.DisplayDialog(
                "Collection Package Generator",
                "Assign both the GameDatabase and a template package.",
                "OK");

            return;
        }

        if (database.allCollections == null ||
            database.allSkins == null)
        {
            EditorUtility.DisplayDialog(
                "Collection Package Generator",
                "The GameDatabase collection or skin list is unavailable.",
                "OK");

            return;
        }

        EnsureFolderExists(outputFolder);

        int created = 0;
        int updated = 0;
        int skippedEmpty = 0;
        int registered = 0;

        Undo.RecordObject(database, "Generate Collection Packages");

        for (int i = 0; i < database.allCollections.Count; i++)
        {
            CollectionData collection = database.allCollections[i];

            if (collection == null ||
                collection.type != CollectionType.Collection)
            {
                continue;
            }

            List<SkinData> skins = GetCollectionSkins(collection);

            if (skins.Count == 0)
            {
                skippedEmpty++;
                continue;
            }

            string generatedId = BuildPackageId(collection);
            CaseData package = FindExistingPackage(generatedId);
            bool isNew = package == null;

            if (isNew)
            {
                package = CreateInstance<CaseData>();
                string fileName =
                    MakeSafeFileName(GetPackageDisplayName(collection)) +
                    ".asset";

                string path = AssetDatabase.GenerateUniqueAssetPath(
                    Path.Combine(outputFolder, fileName)
                        .Replace("\\", "/"));

                AssetDatabase.CreateAsset(package, path);
                created++;
            }
            else
            {
                Undo.RecordObject(package, "Update Collection Package");
                updated++;
            }

            if (isNew || overwriteExistingSettingsFromTemplate)
                EditorUtility.CopySerialized(templatePackage, package);

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
            $"Skipped empty collections: {skippedEmpty}",
            "OK");
    }

    private List<SkinData> GetCollectionSkins(
        CollectionData collection)
    {
        List<SkinData> result = new List<SkinData>();

        for (int i = 0; i < database.allSkins.Count; i++)
        {
            SkinData skin = database.allSkins[i];

            if (skin == null || skin.collectionData == null)
                continue;

            bool sameReference = skin.collectionData == collection;
            bool sameApiId =
                !string.IsNullOrWhiteSpace(collection.apiId) &&
                !string.IsNullOrWhiteSpace(
                    skin.collectionData.apiId) &&
                string.Equals(
                    collection.apiId.Trim(),
                    skin.collectionData.apiId.Trim(),
                    StringComparison.OrdinalIgnoreCase);

            if (sameReference || sameApiId)
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

            if (weaponCompare != 0)
                return weaponCompare;

            return string.Compare(
                a.skinName,
                b.skinName,
                StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    private CaseData FindExistingPackage(string generatedId)
    {
        if (database.allCases != null)
        {
            for (int i = 0; i < database.allCases.Count; i++)
            {
                CaseData existing = database.allCases[i];

                if (existing != null &&
                    string.Equals(
                        existing.apiId,
                        generatedId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return existing;
                }
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:CaseData");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            CaseData existing =
                AssetDatabase.LoadAssetAtPath<CaseData>(path);

            if (existing != null &&
                string.Equals(
                    existing.apiId,
                    generatedId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return existing;
            }
        }

        return null;
    }

    private static void ApplyCollectionIdentityAndRules(
        CaseData package,
        CollectionData collection,
        string generatedId)
    {
        package.apiId = generatedId;
        package.caseName = GetPackageDisplayName(collection);
        package.icon = collection.icon;

        package.containerType =
            CaseContainerType.CollectionPackage;

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

    private static string BuildPackageId(
        CollectionData collection)
    {
        string source = !string.IsNullOrWhiteSpace(collection.apiId)
            ? collection.apiId
            : collection.collectionName;

        return "cc_collection_package_" + Slugify(source);
    }

    private static string GetPackageDisplayName(
        CollectionData collection)
    {
        string name = !string.IsNullOrWhiteSpace(
            collection.collectionName)
                ? collection.collectionName.Trim()
                : collection.name;

        return name.EndsWith(
            "Collection",
            StringComparison.OrdinalIgnoreCase)
                ? name + " Package"
                : name + " Collection Package";
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

    private static void EnsureFolderExists(string folderPath)
    {
        folderPath = string.IsNullOrWhiteSpace(folderPath)
            ? "Assets/Data/Cases/CollectionPackages"
            : folderPath.Replace("\\", "/").TrimEnd('/');

        if (!folderPath.StartsWith("Assets", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Output folder must be inside the Assets folder.");
        }

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
