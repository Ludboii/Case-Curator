#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class CollectionPackageGeneratorWindow : EditorWindow
{
    private const string DefaultCollectionFolder =
        "Assets/Data/Cases/CollectionPackages";

    private const string DefaultSouvenirFolder =
        "Assets/Data/Cases/SouvenirPackages";

    private const string CollectionPrefix = "cc_collection_package_";
    private const string SouvenirPrefix = "cc_souvenir_package_";

    private static readonly HashSet<string> SouvenirCollectionNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "The Mirage Collection",
            "The 2021 Mirage Collection",
            "The Dust 2 Collection",
            "The 2021 Dust 2 Collection",
            "The Inferno Collection",
            "The 2018 Inferno Collection",
            "The Nuke Collection",
            "The 2018 Nuke Collection",
            "The Train Collection",
            "The 2021 Train Collection",
            "The Vertigo Collection",
            "The 2021 Vertigo Collection",
            "The Overpass Collection",
            "The 2024 Overpass Collection",
            "The Ancient Collection",
            "The Anubis Collection",
            "The Cobblestone Collection",
            "The Cache Collection",
            "The Dust Collection",
            "The Italy Collection",
            "The Lake Collection",
            "The Safehouse Collection",
            "The Aztec Collection",
            "The Train 2025 Collection",
            "The Office Collection"
        };

    private static readonly HashSet<string> NormalCollectionNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "The Militia Collection",
            "The Assault Collection",
            "The Achroma Collection",
            "The Harlequin Collection",
            "The Sport & Field Collection",
            "The Graphic Collection",
            "The Control Collection",
            "The Havoc Collection",
            "The Norse Collection",
            "The St. Marc Collection",
            "The Canals Collection",
            "The Gods and Monsters Collection",
            "The Rising Sun Collection",
            "The Chop Shop Collection",
            "The Boreal Collection",
            "The Ascent Collection",
            "The Radiant Collection",
            "The Bank Collection",
            "The Baggage Collection",
            "The Alpha Collection"
        };

    private static readonly HashSet<string> CaseNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Sealed Dead Hand Terminal",
            "Sealed Genesis Terminal",
            "Fever Case",
            "Gallery Case",
            "Kilowatt Case",
            "Revolution Case",
            "Recoil Case",
            "Dreams & Nightmares Case",
            "Snakebite Case",
            "Fracture Case",
            "Prisma 2 Case",
            "CS20 Case",
            "Prisma Case",
            "Danger Zone Case",
            "Horizon Case",
            "Clutch Case",
            "Spectrum Case",
            "Spectrum 2 Case",
            "Glove Case",
            "Gamma Case",
            "Gamma 2 Case",
            "Chroma Case",
            "Chroma 2 Case",
            "Chroma 3 Case",
            "Revolver Case",
            "Shadow Case",
            "Falchion Case",
            "Operation Riptide Case",
            "Operation Broken Fang Case",
            "Shattered Web Case",
            "Operation Hydra Case",
            "Operation Wildfire Case",
            "Operation Vanguard Weapon Case",
            "Operation Breakout Weapon Case",
            "Operation Phoenix Weapon Case",
            "Operation Bravo Case",
            "Huntsman Weapon Case",
            "Winter Offensive Weapon Case",
            "CS:GO Weapon Case",
            "CS:GO Weapon Case 2",
            "CS:GO Weapon Case 3",
            "eSports 2013 Case",
            "eSports 2013 Winter Case",
            "eSports 2014 Summer Case"
        };

    [SerializeField] private GameDatabase database;
    [SerializeField] private CaseData collectionPackageTemplate;
    [SerializeField] private CaseData souvenirPackageTemplate;
    [SerializeField] private string collectionOutputFolder =
        DefaultCollectionFolder;
    [SerializeField] private string souvenirOutputFolder =
        DefaultSouvenirFolder;
    [SerializeField] private bool overwriteExistingSettingsFromTemplates;

    [MenuItem("Case Curator/Containers/Generate Collection Packages")]
    public static void OpenWindow()
    {
        CollectionPackageGeneratorWindow window =
            GetWindow<CollectionPackageGeneratorWindow>();

        window.titleContent = new GUIContent("Collection Packages");
        window.minSize = new Vector2(600f, 500f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Collection and Souvenir Package Generator",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "The corrected CollectionTypes catalog is authoritative. The tool " +
            "repairs CollectionData.type before generation. Every Collection and " +
            "SouvenirCollection receives a normal Collection Package. Every " +
            "SouvenirCollection also receives a separate Souvenir Package. " +
            "Case entries are skipped.",
            MessageType.Info);

        database = (GameDatabase)EditorGUILayout.ObjectField(
            "Game Database", database, typeof(GameDatabase), false);

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

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Output Folders", EditorStyles.boldLabel);

        collectionOutputFolder = EditorGUILayout.TextField(
            "Collection Packages", collectionOutputFolder);

        souvenirOutputFolder = EditorGUILayout.TextField(
            "Souvenir Packages", souvenirOutputFolder);

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
                    "Repair Types and Generate All Packages",
                    GUILayout.Height(42f)))
            {
                GeneratePackages();
            }
        }

        if (!canGenerate)
        {
            EditorGUILayout.HelpBox(
                "Assign the GameDatabase and both templates.",
                MessageType.Warning);
        }
    }

    private void GeneratePackages()
    {
        if (database == null ||
            collectionPackageTemplate == null ||
            souvenirPackageTemplate == null ||
            database.allCollections == null ||
            database.allSkins == null)
        {
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
        Undo.RecordObject(database, "Generate Collection Packages");

        for (int i = 0; i < database.allCollections.Count; i++)
        {
            CollectionData collection = database.allCollections[i];

            if (collection == null)
                continue;

            CollectionType resolvedType = ResolveAuthoritativeType(
                collection,
                out bool foundInCatalog);

            if (!foundInCatalog)
            {
                counters.unknownCollections++;
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

            CreateOrUpdate(
                collection,
                skins,
                false,
                collectionPackageTemplate,
                collectionOutputFolder,
                counters);

            if (resolvedType == CollectionType.SouvenirCollection)
            {
                CreateOrUpdate(
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
            $"Collection types repaired: {counters.typesRepaired}\n" +
            $"Normal packages created: {counters.normalCreated}\n" +
            $"Normal packages updated: {counters.normalUpdated}\n" +
            $"Souvenir packages created: {counters.souvenirCreated}\n" +
            $"Souvenir packages updated: {counters.souvenirUpdated}\n" +
            $"Registered in GameDatabase: {counters.registered}\n" +
            $"Skipped Case entries: {counters.skippedCases}\n" +
            $"Skipped empty collections: {counters.skippedEmpty}\n" +
            $"Unknown names skipped: {counters.unknownCollections}",
            "OK");
    }

    private void CreateOrUpdate(
        CollectionData collection,
        List<SkinData> skins,
        bool souvenir,
        CaseData template,
        string folder,
        Counters counters)
    {
        string generatedId = BuildPackageId(collection, souvenir);
        CaseData package = FindExistingPackage(generatedId);
        bool isNew = package == null;

        if (isNew)
        {
            package = CreateInstance<CaseData>();
            EditorUtility.CopySerialized(template, package);

            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(
                        folder,
                        MakeSafeFileName(
                            GetPackageDisplayName(collection, souvenir)) +
                        ".asset")
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

        ApplyRules(package, collection, generatedId, souvenir);
        ReplaceDropPool(package, skins);

        if (database.allCases == null)
            database.allCases = new List<CaseData>();

        if (!database.allCases.Contains(package))
        {
            database.allCases.Add(package);
            counters.registered++;
        }

        EditorUtility.SetDirty(package);
    }

    private CollectionType ResolveAuthoritativeType(
        CollectionData collection,
        out bool found)
    {
        string displayName = GetCollectionDisplayName(collection);

        if (SouvenirCollectionNames.Contains(displayName))
        {
            found = true;
            return CollectionType.SouvenirCollection;
        }

        if (NormalCollectionNames.Contains(displayName))
        {
            found = true;
            return CollectionType.Collection;
        }

        if (CaseNames.Contains(displayName))
        {
            found = true;
            return CollectionType.Case;
        }

        found = false;
        return collection.type;
    }

    private List<SkinData> GetCollectionSkins(CollectionData collection)
    {
        List<SkinData> result = new List<SkinData>();

        for (int i = 0; i < database.allSkins.Count; i++)
        {
            SkinData skin = database.allSkins[i];

            if (skin != null && SameCollection(
                    skin.collectionData,
                    collection))
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

    private CaseData FindExistingPackage(string generatedId)
    {
        if (database.allCases != null)
        {
            for (int i = 0; i < database.allCases.Count; i++)
            {
                CaseData existing = database.allCases[i];

                if (existing != null && string.Equals(
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
            CaseData existing = AssetDatabase.LoadAssetAtPath<CaseData>(
                AssetDatabase.GUIDToAssetPath(guids[i]));

            if (existing != null && string.Equals(
                    existing.apiId,
                    generatedId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return existing;
            }
        }

        return null;
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
            return;

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

        return (souvenir ? SouvenirPrefix : CollectionPrefix) +
               Slugify(source);
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
        return !string.IsNullOrWhiteSpace(collection.collectionName)
            ? collection.collectionName.Trim()
            : collection.name.Trim();
    }

    private static string Slugify(string value)
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
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');

        return value.Trim();
    }

    private static string NormalizeFolder(
        string folder,
        string fallback)
    {
        string normalized = string.IsNullOrWhiteSpace(folder)
            ? fallback
            : folder.Replace("\\", "/").TrimEnd('/');

        if (!normalized.StartsWith("Assets", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Output folder must be inside Assets.");

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

    private sealed class Counters
    {
        public int typesRepaired;
        public int normalCreated;
        public int normalUpdated;
        public int souvenirCreated;
        public int souvenirUpdated;
        public int registered;
        public int skippedCases;
        public int skippedEmpty;
        public int unknownCollections;
    }
}
#endif
