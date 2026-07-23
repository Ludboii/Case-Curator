#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports normal, StatTrak and vanilla prices for knife/glove SkinData assets
/// from the compact CSV exported from the separate model tabs in the master
/// workbook. The combined "All Rare Specials" worksheet is intentionally not
/// part of this workflow.
/// </summary>
public class KnifeGlovePriceImporterWindow : EditorWindow
{
    private const string DefaultCsvPath =
        "Assets/Data/ImportData/KnifeGlovePrices.csv";

    [SerializeField] private GameDatabase database;
    [SerializeField] private TextAsset priceCsv;

    [Header("Import Rules")]
    [SerializeField]
    [Tooltip(
        "Disabled by default because zero is used as 'not priced yet' in the " +
        "workbook. Enable only when you deliberately want a CSV zero to clear " +
        "an existing in-game price.")]
    private bool overwriteExistingPricesWithZero;

    [SerializeField] private bool logEveryMatchedSkin;

    [MenuItem("Case Curator/Skins/Import Knife and Glove Prices")]
    public static void OpenWindow()
    {
        KnifeGlovePriceImporterWindow window =
            GetWindow<KnifeGlovePriceImporterWindow>();

        window.titleContent = new GUIContent("Knife/Glove Prices");
        window.minSize = new Vector2(640f, 390f);
        window.Show();
    }

    private void OnEnable()
    {
        if (priceCsv == null)
        {
            priceCsv = AssetDatabase.LoadAssetAtPath<TextAsset>(
                DefaultCsvPath);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Knife and Glove Price Import",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Updates only SkinData exterior prices, StatTrak exterior prices, " +
            "vanilla price and vanilla StatTrak price. Skin identity, float " +
            "ranges, rarity, patterns, collections and sprites remain untouched.",
            MessageType.Info);

        EditorGUILayout.Space(8f);

        database = (GameDatabase)EditorGUILayout.ObjectField(
            "Game Database",
            database,
            typeof(GameDatabase),
            false);

        priceCsv = (TextAsset)EditorGUILayout.ObjectField(
            "Knife/Glove Price CSV",
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
            "Matching priority: exact Skin API ID, then weapon + finish name, " +
            "then the SkinData asset name. Vanilla rows match by weapon and the " +
            "SkinData.isVanilla flag. This supports both the new stable workbook " +
            "IDs and older in-game SkinData assets with legacy API IDs.",
            MessageType.None);

        EditorGUILayout.Space(12f);

        bool ready = database != null &&
                     database.allSkins != null &&
                     priceCsv != null;

        using (new EditorGUI.DisabledScope(!ready))
        {
            if (GUILayout.Button("Preview Matches", GUILayout.Height(34f)))
                RunImport(false);

            if (GUILayout.Button(
                    "Import Knife and Glove Prices",
                    GUILayout.Height(42f)))
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Import Knife and Glove Prices",
                    "Update the matched SkinData price fields? No other skin " +
                    "data will be modified.",
                    "Import",
                    "Cancel");

                if (confirmed)
                    RunImport(true);
            }
        }

        if (!ready)
        {
            EditorGUILayout.HelpBox(
                "Assign the main GameDatabase and the knife/glove price CSV.",
                MessageType.Warning);
        }
    }

    private void RunImport(bool applyChanges)
    {
        List<PriceRow> rows = ParseRows(
            priceCsv != null ? priceCsv.text : "");

        int matched = 0;
        int changed = 0;
        int unchanged = 0;
        int unmatched = 0;
        int ambiguous = 0;
        int invalid = 0;
        int valuesApplied = 0;

        List<string> unmatchedRows = new List<string>();
        List<string> ambiguousRows = new List<string>();
        List<string> invalidRows = new List<string>();

        for (int i = 0; i < rows.Count; i++)
        {
            PriceRow row = rows[i];

            if (!row.valid)
            {
                invalid++;
                invalidRows.Add(row.DisplayIdentity);
                continue;
            }

            MatchResult match = FindBestMatch(row);

            if (match.ambiguous)
            {
                ambiguous++;
                ambiguousRows.Add(row.DisplayIdentity);
                continue;
            }

            SkinData skin = match.skin;

            if (skin == null)
            {
                unmatched++;
                unmatchedRows.Add(row.DisplayIdentity);
                continue;
            }

            matched++;

            PriceMutationPreview preview = BuildMutationPreview(skin, row);

            if (!preview.hasChanges)
            {
                unchanged++;

                if (logEveryMatchedSkin)
                {
                    Debug.Log(
                        $"Knife/glove prices unchanged: " +
                        $"{GetDisplayName(skin)} [{row.sourceSheet}]",
                        skin);
                }

                continue;
            }

            changed++;
            valuesApplied += preview.changedValueCount;

            if (logEveryMatchedSkin || !applyChanges)
            {
                Debug.Log(
                    $"Knife/glove price {(applyChanges ? "update" : "preview")}: " +
                    $"{GetDisplayName(skin)} [{row.sourceSheet}] | " +
                    $"{preview.changedValueCount} price value(s) changed.",
                    skin);
            }

            if (!applyChanges)
                continue;

            Undo.RecordObject(skin, "Import Knife and Glove Prices");
            ApplyRow(skin, row);
            EditorUtility.SetDirty(skin);
        }

        if (applyChanges)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        LogProblemRows(
            "KnifeGlovePriceImporter unmatched rows",
            unmatchedRows);

        LogProblemRows(
            "KnifeGlovePriceImporter ambiguous rows",
            ambiguousRows);

        LogProblemRows(
            "KnifeGlovePriceImporter invalid rows",
            invalidRows);

        string action = applyChanges
            ? "Import complete"
            : "Preview complete";

        EditorUtility.DisplayDialog(
            "Knife and Glove Prices",
            $"{action}.\n\n" +
            $"Rows read: {rows.Count}\n" +
            $"Matched: {matched}\n" +
            $"Skins with changes: {changed}\n" +
            $"Already correct / no supplied values: {unchanged}\n" +
            $"Price values changed: {valuesApplied}\n" +
            $"Unmatched: {unmatched}\n" +
            $"Ambiguous: {ambiguous}\n" +
            $"Invalid rows: {invalid}\n\n" +
            (applyChanges
                ? "Only knife/glove price fields were modified."
                : "No assets were modified."),
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

            if (candidate == null || candidate.rarity != Rarity.RareSpecial)
                continue;

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
        string rowWeapon = Normalize(row.weaponName);
        string candidateFinish = candidate.isVanilla
            ? "vanilla"
            : NormalizeFinish(candidate.skinName);
        string rowFinish = NormalizeFinish(row.finishName);
        string candidateAssetName = Normalize(candidate.name);

        int score = 0;

        if (!string.IsNullOrWhiteSpace(candidateApiId) &&
            candidateApiId == rowApiId)
        {
            score += 2000;
        }

        if (candidateWeapon == rowWeapon &&
            candidateFinish == rowFinish)
        {
            score += 1200;
        }

        if (rowFinish == "vanilla" &&
            candidate.isVanilla &&
            candidateWeapon == rowWeapon)
        {
            score += 1500;
        }

        string combined = Normalize(row.weaponName + " " + row.finishName);

        if (!string.IsNullOrWhiteSpace(combined) &&
            candidateAssetName == combined)
        {
            score += 600;
        }

        if (candidateWeapon == rowWeapon)
            score += 50;

        return score;
    }

    private PriceMutationPreview BuildMutationPreview(
        SkinData skin,
        PriceRow row)
    {
        PriceMutationPreview preview = new PriceMutationPreview();

        CountChange(skin.exteriorPrices.factoryNew, row.fnPrice, preview);
        CountChange(skin.exteriorPrices.minimalWear, row.mwPrice, preview);
        CountChange(skin.exteriorPrices.fieldTested, row.ftPrice, preview);
        CountChange(skin.exteriorPrices.wellWorn, row.wwPrice, preview);
        CountChange(skin.exteriorPrices.battleScarred, row.bsPrice, preview);

        CountChange(
            skin.statTrakExteriorPrices.factoryNew,
            row.stFnPrice,
            preview);

        CountChange(
            skin.statTrakExteriorPrices.minimalWear,
            row.stMwPrice,
            preview);

        CountChange(
            skin.statTrakExteriorPrices.fieldTested,
            row.stFtPrice,
            preview);

        CountChange(
            skin.statTrakExteriorPrices.wellWorn,
            row.stWwPrice,
            preview);

        CountChange(
            skin.statTrakExteriorPrices.battleScarred,
            row.stBsPrice,
            preview);

        CountChange(skin.vanillaPrice, row.vanillaPrice, preview);
        CountChange(
            skin.vanillaStatTrakPrice,
            row.vanillaStatTrakPrice,
            preview);

        preview.hasChanges = preview.changedValueCount > 0;
        return preview;
    }

    private void CountChange(
        float current,
        float? supplied,
        PriceMutationPreview preview)
    {
        if (!ShouldApply(supplied))
            return;

        float target = Mathf.Max(0f, supplied.Value);

        if (!Mathf.Approximately(current, target))
            preview.changedValueCount++;
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

        ApplyValue(ref skin.vanillaPrice, row.vanillaPrice);
        ApplyValue(
            ref skin.vanillaStatTrakPrice,
            row.vanillaStatTrakPrice);
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

    private static List<PriceRow> ParseRows(string csv)
    {
        List<PriceRow> rows = new List<PriceRow>();

        if (string.IsNullOrWhiteSpace(csv))
            return rows;

        string[] lines = csv.Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            List<string> columns = ParseCsvLine(lines[i]);

            if (columns.Count < 16)
            {
                rows.Add(
                    PriceRow.Invalid(
                        $"Line {i + 1}: expected 16 columns"));
                continue;
            }

            PriceRow row = new PriceRow
            {
                skinApiId = columns[0].Trim(),
                weaponName = columns[1].Trim(),
                finishName = columns[2].Trim(),
                sourceSheet = columns[3].Trim(),
                fnPrice = ParseNullableFloat(columns[4]),
                mwPrice = ParseNullableFloat(columns[5]),
                ftPrice = ParseNullableFloat(columns[6]),
                wwPrice = ParseNullableFloat(columns[7]),
                bsPrice = ParseNullableFloat(columns[8]),
                stFnPrice = ParseNullableFloat(columns[9]),
                stMwPrice = ParseNullableFloat(columns[10]),
                stFtPrice = ParseNullableFloat(columns[11]),
                stWwPrice = ParseNullableFloat(columns[12]),
                stBsPrice = ParseNullableFloat(columns[13]),
                vanillaPrice = ParseNullableFloat(columns[14]),
                vanillaStatTrakPrice = ParseNullableFloat(columns[15])
            };

            row.valid =
                !string.IsNullOrWhiteSpace(row.weaponName) &&
                !string.IsNullOrWhiteSpace(row.finishName) &&
                row.HasAtLeastOneSuppliedPrice;

            rows.Add(row);
        }

        return rows;
    }

    private static float? ParseNullableFloat(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (float.TryParse(
                value.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float parsed))
        {
            return parsed;
        }

        return null;
    }

    private static List<string> ParseCsvLine(string line)
    {
        List<string> fields = new List<string>();
        StringBuilder current = new StringBuilder();
        bool insideQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char character = line[i];

            if (character == '"')
            {
                if (insideQuotes &&
                    i + 1 < line.Length &&
                    line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }
            }
            else if (character == ',' && !insideQuotes)
            {
                fields.Add(current.ToString());
                current.Length = 0;
            }
            else
            {
                current.Append(character);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static string NormalizeFinish(string value)
    {
        string normalized = Normalize(value);

        if (normalized == "vanilla" ||
            normalized == "starvanilla" ||
            normalized.EndsWith("vanilla", StringComparison.Ordinal))
        {
            return "vanilla";
        }

        return normalized;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        StringBuilder builder = new StringBuilder();
        string lowered = value.Trim().ToLowerInvariant();

        for (int i = 0; i < lowered.Length; i++)
        {
            char character = lowered[i];

            if (char.IsLetterOrDigit(character))
                builder.Append(character);
        }

        return builder.ToString();
    }

    private static string GetDisplayName(SkinData skin)
    {
        if (skin == null)
            return "Unknown Skin";

        return skin.isVanilla
            ? $"{skin.weaponName} | Vanilla"
            : $"{skin.weaponName} | {skin.skinName}";
    }

    private static void LogProblemRows(
        string title,
        List<string> rows)
    {
        if (rows == null || rows.Count == 0)
            return;

        Debug.LogWarning(
            title + ":\n- " + string.Join("\n- ", rows));
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

    private sealed class PriceRow
    {
        public string skinApiId;
        public string weaponName;
        public string finishName;
        public string sourceSheet;

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

        public float? vanillaPrice;
        public float? vanillaStatTrakPrice;
        public bool valid;

        public bool HasAtLeastOneSuppliedPrice =>
            fnPrice.HasValue ||
            mwPrice.HasValue ||
            ftPrice.HasValue ||
            wwPrice.HasValue ||
            bsPrice.HasValue ||
            stFnPrice.HasValue ||
            stMwPrice.HasValue ||
            stFtPrice.HasValue ||
            stWwPrice.HasValue ||
            stBsPrice.HasValue ||
            vanillaPrice.HasValue ||
            vanillaStatTrakPrice.HasValue;

        public string DisplayIdentity =>
            $"{weaponName} | {finishName} [{sourceSheet}]";

        public static PriceRow Invalid(string identity)
        {
            return new PriceRow
            {
                weaponName = identity,
                finishName = "",
                sourceSheet = "",
                valid = false
            };
        }
    }
}
#endif
