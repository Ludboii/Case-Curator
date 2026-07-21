#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class CollectionPackageGeneratorWindow : EditorWindow
{
    private const string DefaultClassificationPath =
        "Assets/Editor/CollectionTypes.txt";

    private const string DefaultCollectionFolder =
        "Assets/Data/Cases/CollectionPackages";

    private const string DefaultSouvenirFolder =
        "Assets/Data/Cases/SouvenirPackages";

    private const string CollectionPrefix = "cc_collection_package_";
    private const string SouvenirPrefix = "cc_souvenir_package_";

    [SerializeField] private GameDatabase database;

    [Header("Classification")]
    [SerializeField] private TextAsset collectionTypesFile;

    [Header("Templates")]
    [SerializeField] private CaseData collectionPackageTemplate;
    [SerializeField] private CaseData souvenirPackageTemplate;

    [Header("Output")]
    [SerializeField] private string collectionOutputFolder =
        DefaultCollectionFolder;

    [SerializeField] private string souvenirOutputFolder =
        DefaultSouvenirFolder;

    [SerializeField]
    private bool overwriteExistingSettingsFromTemplates;

    [SerializeField]
    private bool moveExistingGeneratedAssetsToOutputFolders = true;

    [MenuItem("Case Curator/Containers/Generate Collection Packages")]
    public static void OpenWindow()
    {
        CollectionPackageGeneratorWindow window =
            GetWindow<CollectionPackageGeneratorWindow>();

        window.titleContent = new GUIContent("Collection Packages");
        window.minSize = new Vector2(620f, 560f);
        window.Show();
    }

    private void OnEnable()
    {
        if (collectionTypesFile == null)
        {
            collectionTypesFile =
                AssetDatabase.LoadAssetAtPath<TextAsset>(
                    DefaultClassificationPath);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Collection and Souvenir Package Generator",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "The assigned CollectionTypes text file is authoritative. " +
            "Collection and SouvenirCollection entries receive normal " +
            "Collection Packages. SouvenirCollection entries also receive " +
            "Souvenir Packages. Case entries are skipped.",
            MessageType.Info);

        EditorGUILayout.Space(6f);

        database = (GameDatabase)EditorGUILayout.ObjectField(
            "Game Database",
            database,
            typeof(GameDatabase),
            false);

        collectionTypesFile =
            (TextAsset)EditorGUILayout.ObjectField(
                "Collection Types File",
                collectionTypesFile,
                typeof(TextAsset),
                false);

        EditorGUILayout.Space(8f);
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

        EditorGUILayout.Space(8f);
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

        moveExistingGeneratedAssetsToOutputFolders =
            EditorGUILayout.ToggleLeft(
                "Move existing generated assets into the selected folders",
                moveExistingGeneratedAssetsToOutputFolders);

        EditorGUILayout.Space(12f);

        bool canGenerate = CanGenerate();

        using (new EditorGUI.DisabledScope(!canGenerate))
        {
            if (GUILayout.Button(
                    "Generate / Update Packages",
                    GUILayout.Height(42f)))
            {
                GeneratePackages();
            }

            EditorGUILayout.Space(8f);

            if (GUILayout.Button(
                    "Rebuild All Generator-Owned Packages",
                    GUILayout.Height(38f)))
            {
                RebuildGeneratedPackages();
            }
        }

        if (!canGenerate)
        {
            EditorGUILayout.HelpBox(
                "Assign the GameDatabase, CollectionTypes file and both " +
                "templates before generating.",
                MessageType.Warning);
        }

        EditorGUILayout.Space(8f);

        EditorGUILayout.HelpBox(
            "Use Rebuild once to clear package assets created by older broken " +
            "generator versions. It deletes only assets whose API ID starts " +
            "with cc_collection_package_ or cc_souvenir_package_. Assigned " +
            "template assets are protected.",
            MessageType.Warning);
    }

    private bool CanGenerate()
    {
        return database != null &&
               collectionTypesFile != null &&
               collectionPackageTemplate != null &&
               souvenirPackageTemplate != null;
    }

    private void GeneratePackages()
    {
        if (!CanGenerate() ||
            database.allCollections == null ||
            database.allSkins == null)
        {
            return;
        }

        if (!TryParseClassificationCatalog(
                out Dictionary<string, CollectionType> catalog,
                out List<string> catalogErrors))
        {
            ShowCatalogErrors(catalogErrors);
            return;
        }

        collectionOutputFolder = NormalizeFolder(
            collectionOutputFolder,
            DefaultCollectionFolder);

        souvenirOutputFolder = NormalizeFolder(
            souvenirOutputFolder,
            DefaultSouvenirFolder);

        EnsureFolderExists(collectionOutputFolder);
        EnsureFolderExists(souvenirOutputFolder);

        Counters counters = new Counters();
        HashSet<CaseData> claimedPackages = new HashSet<CaseData>();
        List<string> unknownNames = new List<string>();

        Undo.RecordObject(database, "Generate Collection Packages");

        for (int i = 0; i < database.allCollections.Count; i++)
        {
            CollectionData collection = database.allCollections[i];

            if (collection == null)
                continue;

            if (!TryResolveType(
                    collection,
                    catalog,
                    out CollectionType resolvedType))
            {
                counters.unknownCollections++;
                unknownNames.Add(GetCollectionDisplayName(collection));
                continue;
            }

            if (collection.type != resolvedType)
            {
                Undo.RecordObject(collection, "Repair Collection Type");
                collection.type = resolvedType;
                EditorUtility.SetDirty(collection);
                counters.typesRepaired++;
            }

            if (resolvedType == CollectionType.Case)
            {
                counters.skippedCases++;
                continue;
            }

            List<SkinData> skins = GetCollectionSkins(collection);

            if (skins.Count == 0)
            {
                counters.skippedEmpty++;
                continue;
            }

            CreateOrUpdatePackage(
                collection,
                skins,
                false,
                collectionPackageTemplate,
                collectionOutputFolder,
                claimedPackages,
                counters);

            if (resolvedType == CollectionType.SouvenirCollection)
            {
                CreateOrUpdatePackage(
                    collection,
                    skins,
                    true,
                    souvenirPackageTemplate,
                    souvenirOutputFolder,
                    claimedPackages,
                    counters);
            }
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        LogUnknownCollections(unknownNames);

        EditorUtility.DisplayDialog(
            "Collection Packages Generated",
            $"Collection types repaired: {counters.typesRepaired}\n" +
            $"Normal packages created: {counters.normalCreated}\n" +
            $"Normal packages updated: {counters.normalUpdated}\n" +
            $"Souvenir packages created: {counters.souvenirCreated}\n" +
            $"Souvenir packages updated: {counters.souvenirUpdated}\n" +
            $"Assets moved to output folders: {counters.assetsMoved}\n" +
            $"Registered in GameDatabase: {counters.registered}\n" +
            $"Skipped Case entries: {counters.skippedCases}\n" +
            $"Skipped empty collections: {counters.skippedEmpty}\n" +
            $"Unclassified CollectionData skipped: " +
            $"{counters.unknownCollections}",
            "OK");
    }

    private void RebuildGeneratedPackages()
    {
        if (!CanGenerate())
            return;

        bool confirmed = EditorUtility.DisplayDialog(
            "Rebuild Generated Packages",
            "Delete every generator-owned Collection and Souvenir Package, " +
            "then regenerate them from the current CollectionTypes file?\n\n" +
            "The two assigned template assets are protected.",
            "Rebuild",
            "Cancel");

        if (!confirmed)
            return;

        int deleted = DeleteGeneratorOwnedPackages();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"CollectionPackageGenerator: deleted {deleted} old generated " +
            "package assets before rebuilding.");

        GeneratePackages();
    }

    private int DeleteGeneratorOwnedPackages()
    {
        HashSet<CaseData> packagesToDelete = new HashSet<CaseData>();
        string[] guids = AssetDatabase.FindAssets("t:CaseData");

        for (int i = 0; i < guids.Length; i++)
        {
            CaseData package = AssetDatabase.LoadAssetAtPath<CaseData>(
                AssetDatabase.GUIDToAssetPath(guids[i]));

            if (package == null ||
                package == collectionPackageTemplate ||
                package == souvenirPackageTemplate ||
                !IsGeneratorOwned(package))
            {
                continue;
            }

            packagesToDelete.Add(package);
        }

        if (database.allCases != null)
        {
            Undo.RecordObject(database, "Rebuild Collection Packages");

            for (int i = database.allCases.Count - 1; i >= 0; i--)
            {
                CaseData package = database.allCases[i];

                if (packagesToDelete.Contains(package))
                    database.allCases.RemoveAt(i);
            }

            EditorUtility.SetDirty(database);
        }

        int deleted = 0;

        foreach (CaseData package in packagesToDelete)
        {
            string path = AssetDatabase.GetAssetPath(package);

            if (!string.IsNullOrWhiteSpace(path) &&
                AssetDatabase.DeleteAsset(path))
            {
                deleted++;
            }
        }

        return deleted;
    }

    private void CreateOrUpdatePackage(
        CollectionData collection,
        List<SkinData> skins,
        bool souvenir,
        CaseData template,
        string outputFolder,
        HashSet<CaseData> claimedPackages,
        Counters counters)
    {
        string generatedId = BuildPackageId(collection, souvenir);
        string legacyGeneratedId = BuildLegacyPackageId(collection, souvenir);

        CaseData package = FindExistingPackage(
            collection,
            souvenir,
            generatedId,
            legacyGeneratedId,
            claimedPackages);

        bool isNew = package == null;
        bool wasGeneratorOwned = IsGeneratorOwned(package);

        if (isNew)
        {
            package = CreateInstance<CaseData>();
            EditorUtility.CopySerialized(template, package);

            string path = AssetDatabase.GenerateUniqueAssetPath(
                GetExpectedAssetPath(
                    collection,
                    souvenir,
                    outputFolder));

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

        claimedPackages.Add(package);

        ApplyRules(package, collection, generatedId, souvenir);
        ReplaceDropPool(package, skins);

        if (!isNew &&
            wasGeneratorOwned &&
            moveExistingGeneratedAssetsToOutputFolders &&
            package != collectionPackageTemplate &&
            package != souvenirPackageTemplate)
        {
            if (MoveAssetToExpectedFolder(
                    package,
                    collection,
                    souvenir,
                    outputFolder))
            {
                counters.assetsMoved++;
            }
        }

        RegisterPackage(package, counters);
        EditorUtility.SetDirty(package);
    }

    private CaseData FindExistingPackage(
        CollectionData collection,
        bool souvenir,
        string generatedId,
        string legacyGeneratedId,
        HashSet<CaseData> claimedPackages)
    {
        List<CaseData> candidates = GetAllCaseDataAssets();
        CaseContainerType expectedType = souvenir
            ? CaseContainerType.SouvenirPackage
            : CaseContainerType.CollectionPackage;

        CaseShopCategory expectedCategory = souvenir
            ? CaseShopCategory.SouvenirCollections
            : CaseShopCategory.Collections;

        for (int pass = 0; pass < 4; pass++)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                CaseData candidate = candidates[i];

                if (candidate == null ||
                    claimedPackages.Contains(candidate))
                {
                    continue;
                }

                bool match = false;

                switch (pass)
                {
                    case 0:
                        match = string.Equals(
                            candidate.apiId,
                            generatedId,
                            StringComparison.OrdinalIgnoreCase);
                        break;

                    case 1:
                        match = !string.IsNullOrWhiteSpace(legacyGeneratedId) &&
                            string.Equals(
                                candidate.apiId,
                                legacyGeneratedId,
                                StringComparison.OrdinalIgnoreCase);
                        break;

                    case 2:
                        match = candidate.containerType == expectedType &&
                                candidate.shopCategory == expectedCategory &&
                                PackageUsesCollection(candidate, collection);
                        break;

                    case 3:
                        match = candidate.containerType == expectedType &&
                                candidate.shopCategory == expectedCategory &&
                                string.Equals(
                                    CanonicalKey(candidate.caseName),
                                    CanonicalKey(
                                        GetPackageDisplayName(
                                            collection,
                                            souvenir)),
                                    StringComparison.OrdinalIgnoreCase);
                        break;
                }

                if (match)
                    return candidate;
            }
        }

        return null;
    }

    private List<CaseData> GetAllCaseDataAssets()
    {
        List<CaseData> result = new List<CaseData>();
        HashSet<CaseData> unique = new HashSet<CaseData>();

        if (database.allCases != null)
        {
            for (int i = 0; i < database.allCases.Count; i++)
            {
                CaseData package = database.allCases[i];

                if (package != null && unique.Add(package))
                    result.Add(package);
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:CaseData");

        for (int i = 0; i < guids.Length; i++)
        {
            CaseData package = AssetDatabase.LoadAssetAtPath<CaseData>(
                AssetDatabase.GUIDToAssetPath(guids[i]));

            if (package != null && unique.Add(package))
                result.Add(package);
        }

        return result;
    }

    private void RegisterPackage(CaseData package, Counters counters)
    {
        if (database.allCases == null)
            database.allCases = new List<CaseData>();

        if (database.allCases.Contains(package))
            return;

        database.allCases.Add(package);
        counters.registered++;
    }

    private bool TryParseClassificationCatalog(
        out Dictionary<string, CollectionType> catalog,
        out List<string> errors)
    {
        catalog = new Dictionary<string, CollectionType>(
            StringComparer.OrdinalIgnoreCase);

        errors = new List<string>();

        if (collectionTypesFile == null)
        {
            errors.Add("CollectionTypes file is not assigned.");
            return false;
        }

        string[] lines = collectionTypesFile.text.Split(
            new[] { "\r\n", "\n", "\r" },
            StringSplitOptions.None);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (string.IsNullOrWhiteSpace(line) ||
                line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            int separator = line.LastIndexOf('=');

            if (separator <= 0 || separator >= line.Length - 1)
            {
                errors.Add($"Line {i + 1}: expected Name=CollectionType.");
                continue;
            }

            string displayName = line.Substring(0, separator).Trim();
            string typeText = line.Substring(separator + 1).Trim();

            if (!Enum.TryParse(
                    typeText,
                    true,
                    out CollectionType type))
            {
                errors.Add(
                    $"Line {i + 1}: unknown type '{typeText}'.");
                continue;
            }

            string key = CanonicalKey(displayName);

            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add($"Line {i + 1}: collection name is empty.");
                continue;
            }

            if (catalog.TryGetValue(key, out CollectionType existing) &&
                existing != type)
            {
                errors.Add(
                    $"Line {i + 1}: '{displayName}' conflicts with an " +
                    $"earlier classification.");
                continue;
            }

            catalog[key] = type;
        }

        return errors.Count == 0 && catalog.Count > 0;
    }

    private static bool TryResolveType(
        CollectionData collection,
        Dictionary<string, CollectionType> catalog,
        out CollectionType type)
    {
        string[] candidates =
        {
            collection != null ? collection.collectionName : "",
            collection != null ? collection.name : ""
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            string key = CanonicalKey(candidates[i]);

            if (!string.IsNullOrWhiteSpace(key) &&
                catalog.TryGetValue(key, out type))
            {
                return true;
            }
        }

        type = collection != null
            ? collection.type
            : CollectionType.Collection;

        return false;
    }

    private List<SkinData> GetCollectionSkins(CollectionData collection)
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
            int rarity = a.rarity.CompareTo(b.rarity);

            if (rarity != 0)
                return rarity;

            int weapon = string.Compare(
                a.weaponName,
                b.weaponName,
                StringComparison.OrdinalIgnoreCase);

            return weapon != 0
                ? weapon
                : string.Compare(
                    a.skinName,
                    b.skinName,
                    StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    private static bool PackageUsesCollection(
        CaseData package,
        CollectionData collection)
    {
        if (package == null || collection == null)
            return false;

        SerializedObject serialized = new SerializedObject(package);
        SerializedProperty dropPool = serialized.FindProperty("dropPool");

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

    private static bool SameCollection(
        CollectionData first,
        CollectionData second)
    {
        if (first == second)
            return true;

        if (first == null || second == null)
            return false;

        if (!string.IsNullOrWhiteSpace(first.apiId) &&
            !string.IsNullOrWhiteSpace(second.apiId) &&
            string.Equals(
                first.apiId.Trim(),
                second.apiId.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(
            CanonicalKey(GetCollectionDisplayName(first)),
            CanonicalKey(GetCollectionDisplayName(second)),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyRules(
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
        SerializedObject serialized = new SerializedObject(package);
        SerializedProperty dropPool = serialized.FindProperty("dropPool");

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

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool MoveAssetToExpectedFolder(
        CaseData package,
        CollectionData collection,
        bool souvenir,
        string outputFolder)
    {
        string currentPath = AssetDatabase.GetAssetPath(package);

        if (string.IsNullOrWhiteSpace(currentPath))
            return false;

        string normalizedFolder = outputFolder.TrimEnd('/') + "/";

        if (currentPath.StartsWith(
                normalizedFolder,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string targetPath = AssetDatabase.GenerateUniqueAssetPath(
            GetExpectedAssetPath(
                collection,
                souvenir,
                outputFolder));

        string error = AssetDatabase.MoveAsset(currentPath, targetPath);

        if (!string.IsNullOrWhiteSpace(error))
        {
            Debug.LogWarning(
                $"Could not move generated package '{package.name}': {error}",
                package);
            return false;
        }

        return true;
    }

    private static string GetExpectedAssetPath(
        CollectionData collection,
        bool souvenir,
        string outputFolder)
    {
        return Path.Combine(
                outputFolder,
                MakeSafeFileName(
                    GetPackageDisplayName(collection, souvenir)) +
                ".asset")
            .Replace("\\", "/");
    }

    private static string BuildPackageId(
        CollectionData collection,
        bool souvenir)
    {
        string nameKey = SlugifyDisplayName(
            GetCollectionDisplayName(collection));

        return (souvenir ? SouvenirPrefix : CollectionPrefix) + nameKey;
    }

    private static string BuildLegacyPackageId(
        CollectionData collection,
        bool souvenir)
    {
        if (collection == null ||
            string.IsNullOrWhiteSpace(collection.apiId))
        {
            return "";
        }

        return (souvenir ? SouvenirPrefix : CollectionPrefix) +
               SlugifyRaw(collection.apiId);
    }

    private static string GetPackageDisplayName(
        CollectionData collection,
        bool souvenir)
    {
        string name = GetCollectionDisplayName(collection);

        return souvenir
            ? name + " Souvenir Package"
            : name;
    }

    private static string GetCollectionDisplayName(
        CollectionData collection)
    {
        if (collection == null)
            return "Unnamed Collection";

        if (!string.IsNullOrWhiteSpace(collection.collectionName))
            return collection.collectionName.Trim();

        return collection.name != null
            ? collection.name.Trim()
            : "Unnamed Collection";
    }

    private static string CanonicalKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        value = value.ToLowerInvariant().Replace("&", " and ");
        StringBuilder clean = new StringBuilder();

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            clean.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        string[] rawTokens = clean.ToString().Split(
            new[] { ' ' },
            StringSplitOptions.RemoveEmptyEntries);

        List<string> tokens = new List<string>();

        for (int i = 0; i < rawTokens.Length; i++)
        {
            string token = rawTokens[i];

            if (token == "the" ||
                token == "collection" ||
                token == "package")
            {
                continue;
            }

            if (token == "ii")
                token = "2";

            tokens.Add(token);
        }

        tokens.Sort(StringComparer.Ordinal);
        return string.Join("_", tokens);
    }

    private static string SlugifyDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unnamed";

        value = value.ToLowerInvariant().Replace("&", " and ");
        StringBuilder builder = new StringBuilder();
        bool underscore = false;
        string[] ignoredWords = { "the", "collection" };

        string[] words = value.Split(
            new[] { ' ', '\t' },
            StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i].Trim(
                '.', ',', ':', ';', '-', '_', '(', ')', '[', ']');

            bool ignored = false;

            for (int j = 0; j < ignoredWords.Length; j++)
            {
                if (word == ignoredWords[j])
                {
                    ignored = true;
                    break;
                }
            }

            if (ignored || string.IsNullOrWhiteSpace(word))
                continue;

            if (builder.Length > 0 && !underscore)
                builder.Append('_');

            for (int j = 0; j < word.Length; j++)
            {
                char c = word[j];

                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                    underscore = false;
                }
                else if (!underscore)
                {
                    builder.Append('_');
                    underscore = true;
                }
            }
        }

        return builder.ToString().Trim('_');
    }

    private static string SlugifyRaw(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unnamed";

        StringBuilder builder = new StringBuilder();
        bool underscore = false;

        foreach (char c in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                underscore = false;
            }
            else if (!underscore)
            {
                builder.Append('_');
                underscore = true;
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

    private static bool IsGeneratorOwned(CaseData package)
    {
        if (package == null ||
            string.IsNullOrWhiteSpace(package.apiId))
        {
            return false;
        }

        return package.apiId.StartsWith(
                   CollectionPrefix,
                   StringComparison.OrdinalIgnoreCase) ||
               package.apiId.StartsWith(
                   SouvenirPrefix,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFolder(
        string folder,
        string fallback)
    {
        string normalized = string.IsNullOrWhiteSpace(folder)
            ? fallback
            : folder.Replace("\\", "/").TrimEnd('/');

        if (!normalized.StartsWith(
                "Assets",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Output folder must be inside Assets.");
        }

        return normalized;
    }

    private static void EnsureFolderExists(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    private static void LogUnknownCollections(List<string> unknownNames)
    {
        if (unknownNames == null || unknownNames.Count == 0)
            return;

        unknownNames.Sort(StringComparer.OrdinalIgnoreCase);

        Debug.LogWarning(
            "CollectionPackageGenerator skipped these unclassified " +
            "CollectionData assets:\n- " +
            string.Join("\n- ", unknownNames));
    }

    private static void ShowCatalogErrors(List<string> errors)
    {
        string message = errors != null && errors.Count > 0
            ? string.Join("\n", errors)
            : "The classification file could not be parsed.";

        Debug.LogError(
            "CollectionPackageGenerator classification errors:\n" + message);

        EditorUtility.DisplayDialog(
            "Invalid CollectionTypes File",
            message,
            "OK");
    }

    private sealed class Counters
    {
        public int typesRepaired;
        public int normalCreated;
        public int normalUpdated;
        public int souvenirCreated;
        public int souvenirUpdated;
        public int assetsMoved;
        public int registered;
        public int skippedCases;
        public int skippedEmpty;
        public int unknownCollections;
    }
}
#endif
