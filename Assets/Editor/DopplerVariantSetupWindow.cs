#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Converts legacy generic Doppler/Gamma Doppler case-family assets into
/// concrete source-backed phase/gem SkinData assets. CaseData keeps the generic
/// family entry so its weighted odds stay unchanged.
/// </summary>
public sealed class DopplerVariantSetupWindow : EditorWindow
{
    private const string SkinsJsonUrl =
        "https://raw.githubusercontent.com/ByMykel/CSGO-API/main/public/api/en/skins.json";

    private const string VariantRoot = "Assets/Data/Skins/DopplerVariants";
    private const string IconRoot = VariantRoot + "/Icons";

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
            "Creates only Doppler variants that actually exist in ByMykel's current " +
            "CS2 dataset. Matching uses the structured weapon/pattern/phase fields " +
            "and paint-index fallbacks instead of assuming the phase is part of name. " +
            "CaseData keeps its generic family entry, so case odds do not change. " +
            "The generic family preview icon is changed to the source-backed Phase 1 icon.",
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
            "Existing Doppler price fields are preserved. Re-run Case Curator > Skins > " +
            "Import Knife and Glove Prices only when you intentionally want to import " +
            "your authored Doppler prices.",
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

            List<SkinData> parents = CollectGenericParents();

            int created = 0;
            int updated = 0;
            int parentFamiliesMissing = 0;
            int iconsDownloaded = 0;
            int sourceVariantsMatched = 0;

            for (int parentIndex = 0; parentIndex < parents.Count; parentIndex++)
            {
                SkinData parent = parents[parentIndex];

                if (parent == null)
                    continue;

                bool gamma = DopplerVariantUtility.IsGammaDopplerFamily(parent);
                List<ApiVariantMatch> matches =
                    FindAvailableApiVariants(parsed.items, parent, gamma);

                if (matches.Count == 0)
                {
                    parentFamiliesMissing++;
                    Debug.LogWarning(
                        "Doppler setup: no source-backed " +
                        (gamma ? "Gamma Doppler" : "Doppler") +
                        " variants were found for " + parent.weaponName +
                        ". Matching checked ByMykel weapon.name, pattern.name/id, " +
                        "phase, paint_index, name and market_hash_name.",
                        parent);
                    continue;
                }

                List<SkinData> concreteForParent = new List<SkinData>();

                for (int variantIndex = 0; variantIndex < matches.Count; variantIndex++)
                {
                    ApiVariantMatch match = matches[variantIndex];
                    SkinApiItem api = match.api;
                    string finishName = match.finishName;

                    float progress = parents.Count > 0
                        ? (parentIndex + variantIndex / (float)Mathf.Max(1, matches.Count)) /
                          parents.Count
                        : 0f;

                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Setting up Doppler variants",
                            parent.weaponName + " | " + finishName,
                            progress))
                    {
                        throw new OperationCanceledException();
                    }

                    sourceVariantsMatched++;

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
                        Sprite sprite = GetOrDownloadSprite(
                            api,
                            finishName,
                            out downloaded);

                        if (sprite != null)
                            variant.icon = sprite;

                        if (downloaded)
                            iconsDownloaded++;
                    }

                    variant.patternType = gamma
                        ? PatternType.GammaDoppler
                        : PatternType.Doppler;

                    EditorUtility.SetDirty(variant);
                    concreteForParent.Add(variant);
                    RegisterConcreteVariant(variant);
                }

                // A generic family is only a weighted case-pool placeholder.
                // Its inspect/preview image should consistently represent Phase 1.
                SkinData phaseOne = FindPhaseOne(concreteForParent);

                if (phaseOne != null && phaseOne.icon != null)
                    parent.icon = phaseOne.icon;

                parent.patternType = gamma
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
                $"Source-backed variants matched: {sourceVariantsMatched}\n" +
                $"Concrete variants created: {created}\n" +
                $"Concrete variants updated: {updated}\n" +
                $"Icons downloaded: {iconsDownloaded}\n" +
                $"Families with no ByMykel variants: {parentFamiliesMissing}\n\n" +
                "Variants absent from ByMykel are intentionally not created. " +
                "Existing authored price fields were preserved.";

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

    private List<ApiVariantMatch> FindAvailableApiVariants(
        SkinApiItem[] items,
        SkinData parent,
        bool gamma)
    {
        Dictionary<string, ApiVariantMatch> bestByPhase =
            new Dictionary<string, ApiVariantMatch>(StringComparer.OrdinalIgnoreCase);

        if (items == null || parent == null)
            return new List<ApiVariantMatch>();

        for (int i = 0; i < items.Length; i++)
        {
            SkinApiItem api = items[i];

            if (api == null || !WeaponMatches(api, parent.weaponName))
                continue;

            string apiFamily = GetApiFamily(api);

            if (gamma)
            {
                if (!string.Equals(
                        apiFamily,
                        "Gamma Doppler",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }
            else if (!string.Equals(
                         apiFamily,
                         "Doppler",
                         StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string phase = CanonicalizePhase(GetApiPhase(api));

            if (!IsAllowedPhase(phase, gamma))
                continue;

            string finishName = (gamma ? "Gamma Doppler" : "Doppler") +
                                " (" + phase + ")";
            int score = ScoreApiMatch(api, parent.weaponName, apiFamily, phase);

            if (!bestByPhase.TryGetValue(phase, out ApiVariantMatch existing) ||
                score > existing.score)
            {
                bestByPhase[phase] = new ApiVariantMatch
                {
                    api = api,
                    phase = phase,
                    finishName = finishName,
                    score = score
                };
            }
        }

        List<ApiVariantMatch> result =
            new List<ApiVariantMatch>(bestByPhase.Values);

        result.Sort((a, b) =>
            GetPhaseOrder(a != null ? a.phase : null)
                .CompareTo(GetPhaseOrder(b != null ? b.phase : null)));

        return result;
    }

    private static int ScoreApiMatch(
        SkinApiItem api,
        string parentWeaponName,
        string family,
        string phase)
    {
        if (api == null)
            return int.MinValue;

        int score = 0;
        string expectedWeapon = NormalizeToken(parentWeaponName);

        if (api.weapon != null &&
            string.Equals(
                NormalizeToken(api.weapon.name),
                expectedWeapon,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 1000;
        }

        if (api.pattern != null &&
            string.Equals(
                NormalizeFamily(api.pattern.name),
                family,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 500;
        }

        if (!string.IsNullOrWhiteSpace(api.phase) &&
            string.Equals(
                CanonicalizePhase(api.phase),
                phase,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 300;
        }

        string paintPhase = CanonicalizePhase(GetPhaseFromPaintIndex(api.paint_index));

        if (!string.IsNullOrWhiteSpace(paintPhase) &&
            string.Equals(paintPhase, phase, StringComparison.OrdinalIgnoreCase))
        {
            score += 250;
        }

        string displayWeapon = ExtractWeaponFromDisplayName(api.name);

        if (string.Equals(
                NormalizeToken(displayWeapon),
                expectedWeapon,
                StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }

        if (!string.IsNullOrWhiteSpace(api.image))
            score += 10;

        return score;
    }

    private static bool WeaponMatches(SkinApiItem api, string parentWeaponName)
    {
        if (api == null)
            return false;

        string expected = NormalizeToken(parentWeaponName);

        if (string.IsNullOrWhiteSpace(expected))
            return false;

        if (api.weapon != null &&
            string.Equals(
                NormalizeToken(api.weapon.name),
                expected,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string[] displays =
        {
            api.name,
            api.market_hash_name
        };

        for (int i = 0; i < displays.Length; i++)
        {
            string weapon = ExtractWeaponFromDisplayName(displays[i]);

            if (string.Equals(
                    NormalizeToken(weapon),
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetApiFamily(SkinApiItem api)
    {
        if (api == null)
            return null;

        string paintFamily = GetFamilyFromPaintIndex(api.paint_index);

        if (!string.IsNullOrWhiteSpace(paintFamily))
            return paintFamily;

        if (api.pattern != null)
        {
            string patternFamily = NormalizeFamily(api.pattern.name);

            if (!string.IsNullOrWhiteSpace(patternFamily))
                return patternFamily;

            patternFamily = NormalizeFamily(api.pattern.id);

            if (!string.IsNullOrWhiteSpace(patternFamily))
                return patternFamily;
        }

        string nameFamily = NormalizeFamily(api.name);

        if (!string.IsNullOrWhiteSpace(nameFamily))
            return nameFamily;

        return NormalizeFamily(api.market_hash_name);
    }

    private static string NormalizeFamily(string value)
    {
        string token = NormalizeToken(value);

        if (string.IsNullOrWhiteSpace(token))
            return null;

        if (token.IndexOf("gamma doppler", StringComparison.OrdinalIgnoreCase) >= 0 ||
            (token.IndexOf("gamma", StringComparison.OrdinalIgnoreCase) >= 0 &&
             token.IndexOf("doppler", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return "Gamma Doppler";
        }

        if (token.IndexOf("doppler", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Doppler";

        return null;
    }

    private static string GetApiPhase(SkinApiItem api)
    {
        if (api == null)
            return null;

        if (!string.IsNullOrWhiteSpace(api.phase))
            return api.phase;

        string fromPaint = GetPhaseFromPaintIndex(api.paint_index);

        if (!string.IsNullOrWhiteSpace(fromPaint))
            return fromPaint;

        string fromPattern = ExtractPhase(api.pattern != null ? api.pattern.name : null);

        if (!string.IsNullOrWhiteSpace(fromPattern))
            return fromPattern;

        fromPattern = ExtractPhase(api.pattern != null ? api.pattern.id : null);

        if (!string.IsNullOrWhiteSpace(fromPattern))
            return fromPattern;

        string fromName = ExtractPhase(api.name);

        if (!string.IsNullOrWhiteSpace(fromName))
            return fromName;

        return ExtractPhase(api.market_hash_name);
    }

    private static string ExtractPhase(string value)
    {
        string token = NormalizeToken(value);

        if (string.IsNullOrWhiteSpace(token))
            return null;

        if (Regex.IsMatch(token, @"\bphase\s*1\b", RegexOptions.IgnoreCase))
            return "Phase 1";
        if (Regex.IsMatch(token, @"\bphase\s*2\b", RegexOptions.IgnoreCase))
            return "Phase 2";
        if (Regex.IsMatch(token, @"\bphase\s*3\b", RegexOptions.IgnoreCase))
            return "Phase 3";
        if (Regex.IsMatch(token, @"\bphase\s*4\b", RegexOptions.IgnoreCase))
            return "Phase 4";
        if (token.IndexOf("black pearl", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Black Pearl";
        if (token.IndexOf("sapphire", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Sapphire";
        if (token.IndexOf("ruby", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Ruby";
        if (token.IndexOf("emerald", StringComparison.OrdinalIgnoreCase) >= 0)
            return "Emerald";

        return null;
    }

    private static string CanonicalizePhase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string extracted = ExtractPhase(value);

        if (!string.IsNullOrWhiteSpace(extracted))
            return extracted;

        string token = NormalizeToken(value);

        if (string.Equals(token, "p1", StringComparison.OrdinalIgnoreCase))
            return "Phase 1";
        if (string.Equals(token, "p2", StringComparison.OrdinalIgnoreCase))
            return "Phase 2";
        if (string.Equals(token, "p3", StringComparison.OrdinalIgnoreCase))
            return "Phase 3";
        if (string.Equals(token, "p4", StringComparison.OrdinalIgnoreCase))
            return "Phase 4";

        return null;
    }

    private static string GetPhaseFromPaintIndex(string paintIndex)
    {
        if (!int.TryParse((paintIndex ?? "").Trim(), out int index))
            return null;

        switch (index)
        {
            // Standard Doppler paint kits.
            case 415: return "Ruby";
            case 416: return "Sapphire";
            case 417: return "Black Pearl";
            case 418: return "Phase 1";
            case 419: return "Phase 2";
            case 420: return "Phase 3";
            case 421: return "Phase 4";

            // Gamma Doppler paint kits.
            case 568: return "Emerald";
            case 569: return "Phase 1";
            case 570: return "Phase 2";
            case 571: return "Phase 3";
            case 572: return "Phase 4";
            default: return null;
        }
    }

    private static string GetFamilyFromPaintIndex(string paintIndex)
    {
        if (!int.TryParse((paintIndex ?? "").Trim(), out int index))
            return null;

        if (index >= 415 && index <= 421)
            return "Doppler";

        if (index >= 568 && index <= 572)
            return "Gamma Doppler";

        return null;
    }

    private static bool IsAllowedPhase(string phase, bool gamma)
    {
        if (string.IsNullOrWhiteSpace(phase))
            return false;

        if (phase == "Phase 1" ||
            phase == "Phase 2" ||
            phase == "Phase 3" ||
            phase == "Phase 4")
        {
            return true;
        }

        if (gamma)
            return phase == "Emerald";

        return phase == "Ruby" ||
               phase == "Sapphire" ||
               phase == "Black Pearl";
    }

    private static int GetPhaseOrder(string phase)
    {
        if (phase == "Phase 1") return 0;
        if (phase == "Phase 2") return 1;
        if (phase == "Phase 3") return 2;
        if (phase == "Phase 4") return 3;
        if (phase == "Ruby") return 4;
        if (phase == "Sapphire") return 5;
        if (phase == "Black Pearl") return 6;
        if (phase == "Emerald") return 4;
        return 100;
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
        SkinData variant = FindRegisteredVariant(
            api.id,
            parent.weaponName,
            finishName);

        string assetPath = VariantRoot + "/" +
                           SafeFileName(
                               api.id + "_" + parent.weaponName + "_" + finishName) +
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
            // Do not invent a new balance. New source-backed assets inherit the
            // legacy family values until the authored knife-price CSV is imported.
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

            if (string.Equals(
                    skin.weaponName,
                    weaponName,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    skin.skinName,
                    finishName,
                    StringComparison.OrdinalIgnoreCase))
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

        return null;
    }

    private static string ExtractWeaponFromDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string text = value.Replace("★", " ").Trim();

        text = Regex.Replace(
            text,
            @"^\s*(StatTrak(?:™)?|Souvenir)\s+",
            "",
            RegexOptions.IgnoreCase);

        int separator = text.IndexOf('|');

        if (separator >= 0)
            text = text.Substring(0, separator);

        return text.Trim();
    }

    private static string NormalizeToken(string value)
    {
        string text = (value ?? "")
            .Replace("★", " ")
            .Replace("™", " ")
            .Trim();

        text = Regex.Replace(
            text,
            @"^\s*(StatTrak|Souvenir)\s+",
            "",
            RegexOptions.IgnoreCase);

        text = Regex.Replace(text, @"[^A-Za-z0-9]+", " ");
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string SafeFileName(string value)
    {
        string text = Regex.Replace(
            value ?? "item",
            "[^A-Za-z0-9._-]+",
            "_");
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
        public string market_hash_name;
        public string image;
        public string paint_index;
        public float min_float;
        public float max_float;
        public bool stattrak;
        public bool souvenir;
        public string phase;
        public SkinApiWeapon weapon;
        public SkinApiPattern pattern;
    }

    [Serializable]
    private sealed class SkinApiWeapon
    {
        public string id;
        public int weapon_id;
        public string name;
    }

    [Serializable]
    private sealed class SkinApiPattern
    {
        public string id;
        public string name;
    }

    private sealed class ApiVariantMatch
    {
        public SkinApiItem api;
        public string phase;
        public string finishName;
        public int score;
    }
}
#endif
