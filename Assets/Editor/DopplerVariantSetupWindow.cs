#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Converts the project's legacy generic Doppler/Gamma Doppler family assets
/// into concrete phase/gem content while leaving CaseData pointed at the generic
/// family entry. This preserves existing case odds and gives inventory/results
/// actual variant SkinData assets with their own names, icons and prices.
/// </summary>
public sealed class DopplerVariantSetupWindow : EditorWindow
{
    private const string SkinsJsonUrl =
        "https://raw.githubusercontent.com/ByMykel/CSGO-API/main/public/api/en/skins.json";

    private const string VariantRoot = "Assets/Data/Skins/DopplerVariants";
    private const string IconRoot = VariantRoot + "/Icons";

    private static readonly string[] DopplerVariants =
    {
        "Doppler (Phase 1)",
        "Doppler (Phase 2)",
        "Doppler (Phase 3)",
        "Doppler (Phase 4)",
        "Doppler (Ruby)",
        "Doppler (Sapphire)",
        "Doppler (Black Pearl)"
    };

    private static readonly string[] GammaVariants =
    {
        "Gamma Doppler (Phase 1)",
        "Gamma Doppler (Phase 2)",
        "Gamma Doppler (Phase 3)",
        "Gamma Doppler (Phase 4)",
        "Gamma Doppler (Emerald)"
    };

    [SerializeField] private GameDatabase database;
    [SerializeField] private bool downloadMissingIcons = true;
    [SerializeField] private bool preserveExistingPrices = true;

    private Vector2 scroll;

    [MenuItem("Case Curator/Skins/Setup Doppler Variants")]
    public static void Open()
    {
        GetWindow<DopplerVariantSetupWindow>("Doppler Variants").Show();
    }

    private void OnEnable()
    {
        ResolveDatabase();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Doppler / Gamma Doppler Setup", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Creates concrete Phase 1-4 and gem SkinData assets from ByMykel's " +
            "current CS2 dataset. CaseData keeps its generic Doppler family " +
            "entry, so case odds do not change. The generic family's preview " +
            "icon is changed to Phase 1.",
            MessageType.Info);

        database = (GameDatabase)EditorGUILayout.ObjectField(
            "Game Database",
            database,
            typeof(GameDatabase),
            false);

        downloadMissingIcons = EditorGUILayout.Toggle(
            "Download Missing Variant Icons",
            downloadMissingIcons);

        preserveExistingPrices = EditorGUILayout.Toggle(
            "Preserve Existing Variant Prices",
            preserveExistingPrices);

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "After setup, run Case Curator > Skins > Import Knife and Glove Prices " +
            "again with the latest CSV. The importer will match each concrete " +
            "Doppler phase/gem by weapon + finish name.",
            MessageType.None);

        using (new EditorGUI.DisabledScope(database == null))
        {
            if (GUILayout.Button("SETUP ALL DOPPLER VARIANTS", GUILayout.Height(38f)))
                SetupAll();
        }

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(VariantRoot);
        EditorGUILayout.SelectableLabel(IconRoot);
        EditorGUILayout.EndScrollView();
    }

    private void SetupAll()
    {
        if (database == null)
            return;

        EnsureFolders();

        try
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string json = DownloadText(SkinsJsonUrl);
            SkinApiList parsed = JsonUtility.FromJson<SkinApiList>(
                "{\"items\":" + json + "}");

            if (parsed == null || parsed.items == null)
                throw new InvalidOperationException("Could not parse ByMykel skins.json.");

            Dictionary<string, SkinApiItem> byDisplayName =
                BuildApiLookup(parsed.items);
            List<SkinData> parents = CollectGenericParents();

            int created = 0;
            int updated = 0;
            int missing = 0;
            int iconsDownloaded = 0;

            for (int parentIndex = 0; parentIndex < parents.Count; parentIndex++)
            {
                SkinData parent = parents[parentIndex];

                if (parent == null)
                    continue;

                string[] expected = DopplerVariantUtility.IsGammaDopplerFamily(parent)
                    ? GammaVariants
                    : DopplerVariants;

                List<SkinData> concreteForParent = new List<SkinData>();

                for (int variantIndex = 0; variantIndex < expected.Length; variantIndex++)
                {
                    string finishName = expected[variantIndex];
                    string lookupName = NormalizeDisplayName(
                        parent.weaponName + " | " + finishName);

                    float progress = parents.Count > 0
                        ? (parentIndex + variantIndex / (float)expected.Length) /
                          parents.Count
                        : 0f;

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Setting up Doppler variants",
                            parent.weaponName + " | " + finishName,
                            progress))
                    {
                        throw new OperationCanceledException();
                    }

                    if (!byDisplayName.TryGetValue(lookupName, out SkinApiItem api))
                    {
                        missing++;
                        Debug.LogWarning(
                            $"Doppler setup: ByMykel entry not found for " +
                            $"{parent.weaponName} | {finishName}.",
                            parent);
                        continue;
                    }

                    SkinData variant = FindOrCreateVariant(
                        parent,
                        api,
                        finishName,
                        out bool wasCreated);

                    if (variant == null)
                        continue;

                    if (wasCreated)
                        created++;
                    else
                        updated++;

                    if (downloadMissingIcons &&
                        !string.IsNullOrWhiteSpace(api.image))
                    {
                        bool downloaded;
                        Sprite sprite = GetOrDownloadSprite(api, finishName, out downloaded);

                        if (sprite != null)
                            variant.icon = sprite;

                        if (downloaded)
                            iconsDownloaded++;
                    }

                    variant.patternType =
                        DopplerVariantUtility.IsGammaDopplerFamily(parent)
                            ? PatternType.GammaDoppler
                            : PatternType.Doppler;

                    EditorUtility.SetDirty(variant);
                    concreteForParent.Add(variant);
                    RegisterConcreteVariant(variant);
                }

                SkinData phaseOne = FindPhaseOne(concreteForParent);

                if (phaseOne != null && phaseOne.icon != null)
                    parent.icon = phaseOne.icon;

                parent.patternType =
                    DopplerVariantUtility.IsGammaDopplerFamily(parent)
                        ? PatternType.GammaDoppler
                        : PatternType.Doppler;

                RegisterLegacyParent(parent);
                EditorUtility.SetDirty(parent);
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();

            string summary =
                $"Generic families found: {parents.Count}\n" +
                $"Concrete variants created: {created}\n" +
                $"Concrete variants updated: {updated}\n" +
                $"Icons downloaded: {iconsDownloaded}\n" +
                $"Missing API variants: {missing}\n\n" +
                "Next: re-run Case Curator > Skins > Import Knife and Glove Prices " +
                "with the latest KnifeGlovePrices CSV.";

            Debug.Log("Doppler variant setup complete.\n" + summary);
            EditorUtility.DisplayDialog("Doppler Setup Complete", summary, "OK");
        }
        catch (OperationCanceledException)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
            Debug.LogWarning("Doppler variant setup cancelled. Completed assets were kept.");
        }
        catch (Exception exception)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
            Debug.LogError("Doppler variant setup failed: " + exception);
            EditorUtility.DisplayDialog("Doppler Setup Failed", exception.Message, "OK");
        }
    }

    private List<SkinData> CollectGenericParents()
    {
        List<SkinData> result = new List<SkinData>();
        HashSet<SkinData> seen = new HashSet<SkinData>();

        AddParents(database.allSkins, result, seen);
        AddParents(database.legacyDopplerParents, result, seen);

        if (database.allCases != null)
        {
            for (int i = 0; i < database.allCases.Count; i++)
            {
                CaseData caseData = database.allCases[i];

                if (caseData == null || caseData.dropPool == null)
                    continue;

                for (int j = 0; j < caseData.dropPool.Count; j++)
                {
                    WeightedDrop drop = caseData.dropPool[j];
                    SkinData skin = drop != null ? drop.skin : null;

                    if (DopplerVariantUtility.IsGenericParent(skin) && seen.Add(skin))
                        result.Add(skin);
                }
            }
        }

        result.Sort((a, b) =>
        {
            int weapon = string.Compare(
                a != null ? a.weaponName : "",
                b != null ? b.weaponName : "",
                StringComparison.OrdinalIgnoreCase);

            if (weapon != 0)
                return weapon;

            return string.Compare(
                a != null ? a.skinName : "",
                b != null ? b.skinName : "",
                StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    private static void AddParents(
        List<SkinData> source,
        List<SkinData> target,
        HashSet<SkinData> seen)
    {
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            SkinData skin = source[i];

            if (DopplerVariantUtility.IsGenericParent(skin) && seen.Add(skin))
                target.Add(skin);
        }
    }

    private SkinData FindOrCreateVariant(
        SkinData parent,
        SkinApiItem api,
        string finishName,
        out bool created)
    {
        created = false;
        SkinData variant = FindRegisteredVariant(api.id, parent.weaponName, finishName);
        string assetPath = VariantRoot + "/" +
                           SafeFileName(api.id + "_" + parent.weaponName + "_" + finishName) +
                           ".asset";

        if (variant == null)
            variant = AssetDatabase.LoadAssetAtPath<SkinData>(assetPath);

        WearPrices previousNormal = default;
        WearPrices previousStatTrak = default;
        WearPrices previousSouvenir = default;
        float previousVanilla = 0f;
        float previousVanillaStatTrak = 0f;
        bool hadExisting = variant != null;

        if (hadExisting && preserveExistingPrices)
        {
            previousNormal = variant.exteriorPrices;
            previousStatTrak = variant.statTrakExteriorPrices;
            previousSouvenir = variant.souvenirExteriorPrices;
            previousVanilla = variant.vanillaPrice;
            previousVanillaStatTrak = variant.vanillaStatTrakPrice;
        }

        if (variant == null)
        {
            variant = CreateInstance<SkinData>();
            AssetDatabase.CreateAsset(variant, assetPath);
            created = true;
        }

        variant.skinName = finishName;
        variant.weaponName = parent.weaponName;
        variant.collection = parent.collection;
        variant.collectionData = parent.collectionData;
        variant.apiId = api.id;
        variant.paintIndex = string.IsNullOrWhiteSpace(api.paint_index)
            ? parent.paintIndex
            : api.paint_index;
        variant.rarity = parent.rarity;
        variant.minFloat = api.max_float > api.min_float
            ? api.min_float
            : parent.minFloat;
        variant.maxFloat = api.max_float > api.min_float
            ? api.max_float
            : parent.maxFloat;
        variant.canBeStatTrak = api.stattrak || parent.canBeStatTrak;
        variant.canBeSouvenir = api.souvenir && parent.canBeSouvenir;
        variant.isVanilla = false;
        variant.patternType = finishName.StartsWith(
                "Gamma Doppler",
                StringComparison.OrdinalIgnoreCase)
            ? PatternType.GammaDoppler
            : PatternType.Doppler;

        if (hadExisting && preserveExistingPrices)
        {
            variant.exteriorPrices = previousNormal;
            variant.statTrakExteriorPrices = previousStatTrak;
            variant.souvenirExteriorPrices = previousSouvenir;
            variant.vanillaPrice = previousVanilla;
            variant.vanillaStatTrakPrice = previousVanillaStatTrak;
        }
        else if (created)
        {
            // Safe fallback until the dedicated CSV is imported. This preserves
            // any old generic-family price rather than inventing a new value.
            variant.exteriorPrices = parent.exteriorPrices;
            variant.statTrakExteriorPrices = parent.statTrakExteriorPrices;
            variant.souvenirExteriorPrices = parent.souvenirExteriorPrices;
        }

        return variant;
    }

    private SkinData FindRegisteredVariant(
        string apiId,
        string weaponName,
        string finishName)
    {
        if (database.allSkins == null)
            return null;

        for (int i = 0; i < database.allSkins.Count; i++)
        {
            SkinData skin = database.allSkins[i];

            if (skin == null)
                continue;

            if (!string.IsNullOrWhiteSpace(apiId) &&
                string.Equals(skin.apiId, apiId, StringComparison.Ordinal))
            {
                return skin;
            }

            if (string.Equals(skin.weaponName, weaponName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(skin.skinName, finishName, StringComparison.OrdinalIgnoreCase))
            {
                return skin;
            }
        }

        return null;
    }

    private void RegisterConcreteVariant(SkinData variant)
    {
        if (variant == null)
            return;

        if (database.allSkins == null)
            database.allSkins = new List<SkinData>();

        if (!database.allSkins.Contains(variant))
            database.allSkins.Add(variant);

        if (database.legacyDopplerParents != null)
            database.legacyDopplerParents.Remove(variant);
    }

    private void RegisterLegacyParent(SkinData parent)
    {
        if (parent == null)
            return;

        if (database.legacyDopplerParents == null)
            database.legacyDopplerParents = new List<SkinData>();

        if (!database.legacyDopplerParents.Contains(parent))
            database.legacyDopplerParents.Add(parent);

        if (database.allSkins != null)
            database.allSkins.Remove(parent);
    }

    private Sprite GetOrDownloadSprite(
        SkinApiItem api,
        string finishName,
        out bool downloaded)
    {
        downloaded = false;
        string path = IconRoot + "/" +
                      SafeFileName(api.id + "_" + finishName) +
                      ".png";

        if (!File.Exists(path))
        {
            using (WebClient client = new WebClient())
            {
                byte[] bytes = client.DownloadData(api.image);
                File.WriteAllBytes(path, bytes);
            }

            downloaded = true;
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer != null)
        {
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           !importer.alphaIsTransparency;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;

            if (changed)
                importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static SkinData FindPhaseOne(List<SkinData> variants)
    {
        if (variants == null)
            return null;

        for (int i = 0; i < variants.Count; i++)
        {
            SkinData skin = variants[i];

            if (skin != null &&
                (skin.skinName ?? "").IndexOf(
                    "Phase 1",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return skin;
            }
        }

        return variants.Count > 0 ? variants[0] : null;
    }

    private static Dictionary<string, SkinApiItem> BuildApiLookup(SkinApiItem[] items)
    {
        Dictionary<string, SkinApiItem> lookup =
            new Dictionary<string, SkinApiItem>(StringComparer.OrdinalIgnoreCase);

        if (items == null)
            return lookup;

        for (int i = 0; i < items.Length; i++)
        {
            SkinApiItem api = items[i];

            if (api == null || string.IsNullOrWhiteSpace(api.name))
                continue;

            string key = NormalizeDisplayName(api.name);

            if (!lookup.ContainsKey(key))
                lookup.Add(key, api);
        }

        return lookup;
    }

    private static string NormalizeDisplayName(string value)
    {
        string text = (value ?? "").Replace("★", " ").Trim();
        return Regex.Replace(text, "\\s+", " ");
    }

    private static string SafeFileName(string value)
    {
        string text = Regex.Replace(value ?? "item", "[^A-Za-z0-9._-]+", "_");
        text = Regex.Replace(text, "_+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(text) ? "item" : text;
    }

    private static string DownloadText(string url)
    {
        using (WebClient client = new WebClient())
            return client.DownloadString(url);
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Data");
        EnsureFolder("Assets/Data", "Skins");
        EnsureFolder("Assets/Data/Skins", "DopplerVariants");
        EnsureFolder(VariantRoot, "Icons");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private void ResolveDatabase()
    {
        if (database != null)
            return;

        if (Selection.activeObject is GameDatabase selected)
        {
            database = selected;
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:GameDatabase");

        if (guids.Length == 1)
        {
            database = AssetDatabase.LoadAssetAtPath<GameDatabase>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }

    [Serializable]
    private sealed class SkinApiList
    {
        public SkinApiItem[] items;
    }

    [Serializable]
    private sealed class SkinApiItem
    {
        public string id;
        public string name;
        public string image;
        public string paint_index;
        public float min_float;
        public float max_float;
        public bool stattrak;
        public bool souvenir;
    }
}
#endif
