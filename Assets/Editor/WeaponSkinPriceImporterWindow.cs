#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports normal, StatTrak and Souvenir wear prices for regular weapon skins.
/// Rare Special knives/gloves are intentionally excluded and continue to use
/// KnifeGlovePriceImporterWindow. StickerData is also excluded.
/// </summary>
public sealed class WeaponSkinPriceImporterWindow : EditorWindow
{
    private const string DefaultCsvPath =
        "Assets/Data/ImportData/WeaponSkinPrices.csv";

    [SerializeField] private GameDatabase database;
    [SerializeField] private TextAsset priceCsv;

    [Header("Import Rules")]
    [SerializeField]
    [Tooltip(
        "Enabled by default because zero in the authored weapon-skin CSV means " +
        "that variant has no price / is unavailable. Disable this when using a " +
        "partial CSV where zero means 'not priced yet'.")]
    private bool overwriteExistingPricesWithZero = true;

    [SerializeField] private bool logEveryMatchedSkin;

    private Vector2 scroll;
    private string preview = "";

    [MenuItem("Case Curator/Skins/Import Weapon Skin Prices")]
    public static void OpenWindow()
    {
        WeaponSkinPriceImporterWindow window =
            GetWindow<WeaponSkinPriceImporterWindow>();
        window.titleContent = new GUIContent("Weapon Skin Prices");
        window.minSize = new Vector2(700f, 500f);
        window.Show();
    }

    private void OnEnable()
    {
        ResolveDatabase();

        if (priceCsv == null)
        {
            priceCsv = AssetDatabase.LoadAssetAtPath<TextAsset>(
                DefaultCsvPath);
        }
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Weapon Skin Price CSV Import",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Updates only normal, StatTrak and Souvenir wear-price fields on " +
            "regular weapon SkinData assets. Knives, gloves, stickers, identity, " +
            "rarity, float ranges, collections, patterns and sprites are untouched.",
            MessageType.Info);

        database = (GameDatabase)EditorGUILayout.ObjectField(
            "Game Database",
            database,
            typeof(GameDatabase),
            false);

        priceCsv = (TextAsset)EditorGUILayout.ObjectField(
            "Weapon Skin Price CSV",
            priceCsv,
            typeof(TextAsset),
            false);

        overwriteExistingPricesWithZero = EditorGUILayout.ToggleLeft(
            "Allow CSV zero values to overwrite existing prices",
            overwriteExistingPricesWithZero);

        logEveryMatchedSkin = EditorGUILayout.ToggleLeft(
            "Log every matched skin",
            logEveryMatchedSkin);

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "Matching priority: Skin API ID, exact WeaponName + SkinName, then " +
            "DisplayName. The bundled CSV intentionally leaves SkinApiId blank " +
            "and matches by weapon + finish name.",
            MessageType.None);

        EditorGUILayout.Space(10f);

        bool ready = database != null &&
                     database.allSkins != null &&
                     priceCsv != null;

        using (new EditorGUI.DisabledScope(!ready))
        {
            if (GUILayout.Button("PREVIEW MATCHES", GUILayout.Height(32f)))
                RunImport(false);

            if (GUILayout.Button(
                    "IMPORT WEAPON SKIN PRICES",
                    GUILayout.Height(40f)))
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Import Weapon Skin Prices",
                    "Update the matched regular weapon SkinData price fields? " +
                    "No non-price data will be modified.",
                    "Import",
                    "Cancel");

                if (confirmed)
                    RunImport(true);
            }
        }

        if (!ready)
        {
            EditorGUILayout.HelpBox(
                "Assign the main GameDatabase and a weapon-skin price CSV.",
                MessageType.Warning);
        }

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Preview / Results", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(preview, GUILayout.MinHeight(250f));
        EditorGUILayout.EndScrollView();
    }

    private void RunImport(bool applyChanges)
    {
        List<PriceRow> rows = ParseRows(
            priceCsv != null ? priceCsv.text : "",
            out string parseMessage);

        ImportSummary summary = new ImportSummary();

        for (int i = 0; i < rows.Count; i++)
        {
            PriceRow row = rows[i];

            if (row == null || !row.valid)
            {
                summary.invalid.Add(
                    row != null ? row.error : $"Row {i + 2}: invalid.");
                continue;
            }

            MatchResult match = FindBestMatch(row);

            if (match.ambiguous)
            {
                summary.ambiguous.Add(row.DisplayIdentity);
                continue;
            }

            SkinData skin = match.skin;

            if (skin == null)
            {
                summary.unmatched.Add(row.DisplayIdentity);
                continue;
            }

            summary.matched++;
            PriceMutationPreview mutation = BuildMutationPreview(skin, row);

            if (!mutation.hasChanges)
            {
                summary.unchanged++;
                continue;
            }

            summary.changed++;
            summary.valuesChanged += mutation.changedValueCount;

            if (logEveryMatchedSkin || !applyChanges)
            {
                Debug.Log(
                    $"Weapon skin price {(applyChanges ? "update" : "preview")}: " +
                    $"{skin.weaponName} | {skin.skinName} — " +
                    $"{mutation.changedValueCount} value(s).",
                    skin);
            }

            if (!applyChanges)
                continue;

            Undo.RecordObject(skin, "Import Weapon Skin Prices");
            ApplyRow(skin, row);
            EditorUtility.SetDirty(skin);
            summary.updated++;
        }

        if (applyChanges)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (InventoryManager.Instance != null)
                InventoryManager.Instance.RecalculateCachedTotalMarketValue();
        }

        preview = BuildSummary(parseMessage, summary, applyChanges);

        EditorUtility.DisplayDialog(
            "Weapon Skin Prices",
            preview,
            "OK");
    }

    private MatchResult FindBestMatch(PriceRow row)
    {
        SkinData best = null;
        int bestScore = int.MinValue;
        bool tied = false;

        for (int i = 0; i < database.allSkins.Count; i++)
        {
            SkinData candidate = database.allSkins[i];

            if (candidate == null ||
                candidate is StickerData ||
                candidate.rarity == Rarity.RareSpecial)
            {
                continue;
            }

            int score = ScoreCandidate(candidate, row);

            if (score <= 0)
                continue;

            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
                tied = false;
            }
            else if (score == bestScore && candidate != best)
            {
                tied = true;
            }
        }

        return new MatchResult
        {
            skin = tied ? null : best,
            ambiguous = tied
        };
    }

    private static int ScoreCandidate(SkinData candidate, PriceRow row)
    {
        string candidateApiId = Normalize(candidate.apiId);
        string rowApiId = Normalize(row.skinApiId);
        string candidateWeapon = Normalize(candidate.weaponName);
        string candidateSkin = Normalize(candidate.skinName);
        string rowWeapon = Normalize(row.weaponName);
        string rowSkin = Normalize(row.skinName);
        string candidateDisplay = Normalize(
            candidate.weaponName + " " + candidate.skinName);
        string rowDisplay = Normalize(row.displayName);

        int score = 0;

        if (!string.IsNullOrWhiteSpace(rowApiId) &&
            candidateApiId == rowApiId)
        {
            score += 5000;
        }

        if (!string.IsNullOrWhiteSpace(rowWeapon) &&
            !string.IsNullOrWhiteSpace(rowSkin) &&
            candidateWeapon == rowWeapon &&
            candidateSkin == rowSkin)
        {
            score += 3000;
        }

        if (!string.IsNullOrWhiteSpace(rowDisplay) &&
            candidateDisplay == rowDisplay)
        {
            score += 1800;
        }

        string candidatePipeDisplay = Normalize(
            candidate.weaponName + " | " + candidate.skinName);

        if (!string.IsNullOrWhiteSpace(rowDisplay) &&
            candidatePipeDisplay == rowDisplay)
        {
            score += 1800;
        }

        if (!string.IsNullOrWhiteSpace(rowWeapon) &&
            candidateWeapon == rowWeapon)
        {
            score += 50;
        }

        return score;
    }

    private PriceMutationPreview BuildMutationPreview(
        SkinData skin,
        PriceRow row)
    {
        PriceMutationPreview result = new PriceMutationPreview();

        CountChange(skin.exteriorPrices.factoryNew, row.fnPrice, result);
        CountChange(skin.exteriorPrices.minimalWear, row.mwPrice, result);
        CountChange(skin.exteriorPrices.fieldTested, row.ftPrice, result);
        CountChange(skin.exteriorPrices.wellWorn, row.wwPrice, result);
        CountChange(skin.exteriorPrices.battleScarred, row.bsPrice, result);

        CountChange(
            skin.statTrakExteriorPrices.factoryNew,
            row.stFnPrice,
            result);
        CountChange(
            skin.statTrakExteriorPrices.minimalWear,
            row.stMwPrice,
            result);
        CountChange(
            skin.statTrakExteriorPrices.fieldTested,
            row.stFtPrice,
            result);
        CountChange(
            skin.statTrakExteriorPrices.wellWorn,
            row.stWwPrice,
            result);
        CountChange(
            skin.statTrakExteriorPrices.battleScarred,
            row.stBsPrice,
            result);

        CountChange(
            skin.souvenirExteriorPrices.factoryNew,
            row.svFnPrice,
            result);
        CountChange(
            skin.souvenirExteriorPrices.minimalWear,
            row.svMwPrice,
            result);
        CountChange(
            skin.souvenirExteriorPrices.fieldTested,
            row.svFtPrice,
            result);
        CountChange(
            skin.souvenirExteriorPrices.wellWorn,
            row.svWwPrice,
            result);
        CountChange(
            skin.souvenirExteriorPrices.battleScarred,
            row.svBsPrice,
            result);

        result.hasChanges = result.changedValueCount > 0;
        return result;
    }

    private void CountChange(
        float current,
        float? supplied,
        PriceMutationPreview result)
    {
        if (!ShouldApply(supplied))
            return;

        float target = Mathf.Max(0f, supplied.Value);

        if (!Mathf.Approximately(current, target))
            result.changedValueCount++;
    }

    private void ApplyRow(SkinData skin, PriceRow row)
    {
        WearPrices normal = skin.exteriorPrices;
        ApplyValue(ref normal.factoryNew, row.fnPrice);
        ApplyValue(ref normal.minimalWear, row.mwPrice);
        ApplyValue(ref normal.fieldTested, row.ftPrice);
        ApplyValue(ref normal.wellWorn, row.wwPrice);
        ApplyValue(ref normal.battleScarred, row.bsPrice);
        skin.exteriorPrices = normal;

        WearPrices statTrak = skin.statTrakExteriorPrices;
        ApplyValue(ref statTrak.factoryNew, row.stFnPrice);
        ApplyValue(ref statTrak.minimalWear, row.stMwPrice);
        ApplyValue(ref statTrak.fieldTested, row.stFtPrice);
        ApplyValue(ref statTrak.wellWorn, row.stWwPrice);
        ApplyValue(ref statTrak.battleScarred, row.stBsPrice);
        skin.statTrakExteriorPrices = statTrak;

        WearPrices souvenir = skin.souvenirExteriorPrices;
        ApplyValue(ref souvenir.factoryNew, row.svFnPrice);
        ApplyValue(ref souvenir.minimalWear, row.svMwPrice);
        ApplyValue(ref souvenir.fieldTested, row.svFtPrice);
        ApplyValue(ref souvenir.wellWorn, row.svWwPrice);
        ApplyValue(ref souvenir.battleScarred, row.svBsPrice);
        skin.souvenirExteriorPrices = souvenir;
    }

    private void ApplyValue(ref float target, float? supplied)
    {
        if (!ShouldApply(supplied))
            return;

        target = Mathf.Max(0f, supplied.Value);
    }

    private bool ShouldApply(float? supplied)
    {
        if (!supplied.HasValue)
            return false;

        return overwriteExistingPricesWithZero || supplied.Value > 0f;
    }

    private static List<PriceRow> ParseRows(
        string csv,
        out string message)
    {
        List<PriceRow> rows = new List<PriceRow>();
        message = "";

        if (string.IsNullOrWhiteSpace(csv))
        {
            message = "No CSV content.";
            return rows;
        }

        string[] lines = csv.Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');

        int headerLine = -1;
        char delimiter = ',';
        List<string> headers = null;

        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            delimiter = DetectDelimiter(lines[i]);
            headers = ParseCsvLine(lines[i], delimiter);
            headerLine = i;
            break;
        }

        if (headers == null)
        {
            message = "CSV has no header row.";
            return rows;
        }

        int apiId = FindHeader(headers, "SkinApiId", "ApiId", "ID");
        int weapon = FindHeader(headers, "WeaponName", "Weapon");
        int skin = FindHeader(headers, "SkinName", "FinishName", "Finish");
        int display = FindHeader(headers, "DisplayName", "Name");

        int fn = FindHeader(headers, "FNPrice", "FN", "FactoryNew");
        int mw = FindHeader(headers, "MWPrice", "MW", "MinimalWear");
        int ft = FindHeader(headers, "FTPrice", "FT", "FieldTested");
        int ww = FindHeader(headers, "WWPrice", "WW", "WellWorn");
        int bs = FindHeader(headers, "BSPrice", "BS", "BattleScarred");

        int stFn = FindHeader(headers, "STFNPrice", "StatTrakFNPrice");
        int stMw = FindHeader(headers, "STMWPrice", "StatTrakMWPrice");
        int stFt = FindHeader(headers, "STFTPrice", "StatTrakFTPrice");
        int stWw = FindHeader(headers, "STWWPrice", "StatTrakWWPrice");
        int stBs = FindHeader(headers, "STBSPrice", "StatTrakBSPrice");

        int svFn = FindHeader(headers, "SouvenirFNPrice", "SVFNPrice");
        int svMw = FindHeader(headers, "SouvenirMWPrice", "SVMWPrice");
        int svFt = FindHeader(headers, "SouvenirFTPrice", "SVFTPrice");
        int svWw = FindHeader(headers, "SouvenirWWPrice", "SVWWPrice");
        int svBs = FindHeader(headers, "SouvenirBSPrice", "SVBSPrice");

        bool hasIdentity = apiId >= 0 ||
                           (weapon >= 0 && skin >= 0) ||
                           display >= 0;
        bool hasAnyPrice = fn >= 0 || mw >= 0 || ft >= 0 || ww >= 0 || bs >= 0 ||
                           stFn >= 0 || stMw >= 0 || stFt >= 0 || stWw >= 0 || stBs >= 0 ||
                           svFn >= 0 || svMw >= 0 || svFt >= 0 || svWw >= 0 || svBs >= 0;

        if (!hasIdentity || !hasAnyPrice)
        {
            message =
                "CSV needs a skin identity (SkinApiId, WeaponName + SkinName, " +
                "or DisplayName) and at least one price column.";
            return rows;
        }

        for (int i = headerLine + 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            List<string> fields = ParseCsvLine(lines[i], delimiter);
            PriceRow row = new PriceRow
            {
                lineNumber = i + 1,
                skinApiId = GetField(fields, apiId),
                weaponName = GetField(fields, weapon),
                skinName = GetField(fields, skin),
                displayName = GetField(fields, display)
            };

            bool identityPresent =
                !string.IsNullOrWhiteSpace(row.skinApiId) ||
                (!string.IsNullOrWhiteSpace(row.weaponName) &&
                 !string.IsNullOrWhiteSpace(row.skinName)) ||
                !string.IsNullOrWhiteSpace(row.displayName);

            if (!identityPresent)
            {
                row.valid = false;
                row.error = $"Row {row.lineNumber}: missing skin identity.";
                rows.Add(row);
                continue;
            }

            bool valid = true;
            row.fnPrice = ParseOptionalPrice(fields, fn, row.lineNumber, "FNPrice", ref valid, out string error);
            row.error = error;
            row.mwPrice = ParseOptionalPrice(fields, mw, row.lineNumber, "MWPrice", ref valid, out error);
            AppendError(row, error);
            row.ftPrice = ParseOptionalPrice(fields, ft, row.lineNumber, "FTPrice", ref valid, out error);
            AppendError(row, error);
            row.wwPrice = ParseOptionalPrice(fields, ww, row.lineNumber, "WWPrice", ref valid, out error);
            AppendError(row, error);
            row.bsPrice = ParseOptionalPrice(fields, bs, row.lineNumber, "BSPrice", ref valid, out error);
            AppendError(row, error);

            row.stFnPrice = ParseOptionalPrice(fields, stFn, row.lineNumber, "STFNPrice", ref valid, out error);
            AppendError(row, error);
            row.stMwPrice = ParseOptionalPrice(fields, stMw, row.lineNumber, "STMWPrice", ref valid, out error);
            AppendError(row, error);
            row.stFtPrice = ParseOptionalPrice(fields, stFt, row.lineNumber, "STFTPrice", ref valid, out error);
            AppendError(row, error);
            row.stWwPrice = ParseOptionalPrice(fields, stWw, row.lineNumber, "STWWPrice", ref valid, out error);
            AppendError(row, error);
            row.stBsPrice = ParseOptionalPrice(fields, stBs, row.lineNumber, "STBSPrice", ref valid, out error);
            AppendError(row, error);

            row.svFnPrice = ParseOptionalPrice(fields, svFn, row.lineNumber, "SouvenirFNPrice", ref valid, out error);
            AppendError(row, error);
            row.svMwPrice = ParseOptionalPrice(fields, svMw, row.lineNumber, "SouvenirMWPrice", ref valid, out error);
            AppendError(row, error);
            row.svFtPrice = ParseOptionalPrice(fields, svFt, row.lineNumber, "SouvenirFTPrice", ref valid, out error);
            AppendError(row, error);
            row.svWwPrice = ParseOptionalPrice(fields, svWw, row.lineNumber, "SouvenirWWPrice", ref valid, out error);
            AppendError(row, error);
            row.svBsPrice = ParseOptionalPrice(fields, svBs, row.lineNumber, "SouvenirBSPrice", ref valid, out error);
            AppendError(row, error);

            row.valid = valid;
            rows.Add(row);
        }

        message = $"Parsed {rows.Count:N0} data rows using '{delimiter}'.";
        return rows;
    }

    private static float? ParseOptionalPrice(
        List<string> fields,
        int index,
        int lineNumber,
        string column,
        ref bool valid,
        out string error)
    {
        error = "";

        if (index < 0)
            return null;

        string raw = GetField(fields, index);

        if (string.IsNullOrWhiteSpace(raw))
            return null;

        raw = raw.Replace("Gold", "")
            .Replace(" ", "")
            .Trim();

        if (!float.TryParse(
                raw,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out float value))
        {
            valid = false;
            error = $"Row {lineNumber}: invalid {column} '{raw}'.";
            return null;
        }

        return Mathf.Max(0f, value);
    }

    private static void AppendError(PriceRow row, string error)
    {
        if (row == null || string.IsNullOrWhiteSpace(error))
            return;

        if (string.IsNullOrWhiteSpace(row.error))
            row.error = error;
        else
            row.error += " " + error;
    }

    private static string BuildSummary(
        string parseMessage,
        ImportSummary summary,
        bool apply)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(apply ? "Import complete." : "Preview complete.");
        builder.AppendLine(parseMessage);
        builder.AppendLine();
        builder.AppendLine($"Matched: {summary.matched:N0}");
        builder.AppendLine($"Skins with changes: {summary.changed:N0}");
        builder.AppendLine($"Updated assets: {summary.updated:N0}");
        builder.AppendLine($"Already correct: {summary.unchanged:N0}");
        builder.AppendLine($"Price values changed: {summary.valuesChanged:N0}");
        builder.AppendLine($"Unmatched: {summary.unmatched.Count:N0}");
        builder.AppendLine($"Ambiguous: {summary.ambiguous.Count:N0}");
        builder.AppendLine($"Invalid: {summary.invalid.Count:N0}");

        AppendList(builder, "UNMATCHED", summary.unmatched);
        AppendList(builder, "AMBIGUOUS", summary.ambiguous);
        AppendList(builder, "INVALID", summary.invalid);
        return builder.ToString();
    }

    private static void AppendList(
        StringBuilder builder,
        string title,
        List<string> values)
    {
        if (values == null || values.Count == 0)
            return;

        builder.AppendLine();
        builder.AppendLine(title);

        int maximum = Mathf.Min(100, values.Count);

        for (int i = 0; i < maximum; i++)
            builder.AppendLine("- " + values[i]);

        if (values.Count > maximum)
            builder.AppendLine($"... plus {values.Count - maximum:N0} more.");
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

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        StringBuilder builder = new StringBuilder(value.Length);
        string lower = value.Trim().ToLowerInvariant();

        for (int i = 0; i < lower.Length; i++)
        {
            char c = lower[i];

            if (char.IsLetterOrDigit(c))
                builder.Append(c);
        }

        return builder.ToString();
    }

    private static char DetectDelimiter(string line)
    {
        int commas = CountOutsideQuotes(line, ',');
        int semicolons = CountOutsideQuotes(line, ';');
        return semicolons > commas ? ';' : ',';
    }

    private static int CountOutsideQuotes(string line, char character)
    {
        bool quoted = false;
        int count = 0;

        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
                quoted = !quoted;
            else if (!quoted && line[i] == character)
                count++;
        }

        return count;
    }

    private static List<string> ParseCsvLine(string line, char delimiter)
    {
        List<string> fields = new List<string>();
        StringBuilder current = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            char character = line[i];

            if (character == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == delimiter && !quoted)
            {
                fields.Add(current.ToString().Trim());
                current.Length = 0;
            }
            else
            {
                current.Append(character);
            }
        }

        fields.Add(current.ToString().Trim());
        return fields;
    }

    private static int FindHeader(
        List<string> headers,
        params string[] names)
    {
        if (headers == null)
            return -1;

        for (int i = 0; i < headers.Count; i++)
        {
            for (int j = 0; j < names.Length; j++)
            {
                if (string.Equals(
                        headers[i].Trim(),
                        names[j],
                        StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static string GetField(List<string> fields, int index)
    {
        return index >= 0 && fields != null && index < fields.Count
            ? fields[index].Trim()
            : "";
    }

    private sealed class PriceRow
    {
        public int lineNumber;
        public string skinApiId;
        public string weaponName;
        public string skinName;
        public string displayName;

        public float? fnPrice;
        public float? mwPrice;
        public float? ftPrice;
        public float? wwPrice;
        public float? bsPrice;
        public float? stFnPrice;
        public float? stMwPrice;
        public float? stFtPrice;
        public float? stWwPrice;
        public float? stBsPrice;
        public float? svFnPrice;
        public float? svMwPrice;
        public float? svFtPrice;
        public float? svWwPrice;
        public float? svBsPrice;

        public bool valid;
        public string error;

        public string DisplayIdentity
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(weaponName) &&
                    !string.IsNullOrWhiteSpace(skinName))
                {
                    return weaponName + " | " + skinName;
                }

                if (!string.IsNullOrWhiteSpace(displayName))
                    return displayName;

                return skinApiId;
            }
        }
    }

    private sealed class MatchResult
    {
        public SkinData skin;
        public bool ambiguous;
    }

    private sealed class PriceMutationPreview
    {
        public bool hasChanges;
        public int changedValueCount;
    }

    private sealed class ImportSummary
    {
        public int matched;
        public int changed;
        public int updated;
        public int unchanged;
        public int valuesChanged;
        public readonly List<string> unmatched = new List<string>();
        public readonly List<string> ambiguous = new List<string>();
        public readonly List<string> invalid = new List<string>();
    }
}
#endif