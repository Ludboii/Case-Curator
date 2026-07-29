#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class StickerPriceImporterWindow : EditorWindow
{
    [SerializeField] private GameDatabase database;
    [SerializeField] private TextAsset csvFile;
    [SerializeField] private bool multiplyImportedPrices;
    [SerializeField, Min(0f)] private float priceMultiplier = 1f;

    private Vector2 scroll;
    private string preview = "";

    [MenuItem("Case Curator/Stickers/Import Sticker Prices")]
    public static void Open()
    {
        GetWindow<StickerPriceImporterWindow>(
            "Sticker Price Import").Show();
    }

    private void OnEnable()
    {
        ResolveDatabase();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Sticker Price CSV Import",
            EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Required columns: StickerApiId or StickerName, plus MarketValue. " +
            "Extra columns such as Rarity, CapsuleName, TournamentEvent, " +
            "TeamPlayer, Year and IconPath are accepted and ignored.",
            MessageType.Info);

        database = (GameDatabase)EditorGUILayout.ObjectField(
            "Game Database",
            database,
            typeof(GameDatabase),
            false);
        csvFile = (TextAsset)EditorGUILayout.ObjectField(
            "CSV File",
            csvFile,
            typeof(TextAsset),
            false);
        multiplyImportedPrices = EditorGUILayout.Toggle(
            "Apply Multiplier",
            multiplyImportedPrices);

        using (new EditorGUI.DisabledScope(!multiplyImportedPrices))
        {
            priceMultiplier = EditorGUILayout.FloatField(
                "Price Multiplier",
                Mathf.Max(0f, priceMultiplier));
        }

        EditorGUILayout.Space(8f);

        using (new EditorGUI.DisabledScope(database == null || csvFile == null))
        {
            if (GUILayout.Button("PREVIEW MATCHES", GUILayout.Height(28f)))
                Preview();

            if (GUILayout.Button("IMPORT MARKET VALUES", GUILayout.Height(34f)))
                Import();
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(preview, GUILayout.MinHeight(220f));
        EditorGUILayout.EndScrollView();
    }

    private void Preview()
    {
        List<PriceRow> rows = ParseRows(out string parseMessage);
        MatchSummary summary = Match(rows, false);
        preview = BuildSummary(parseMessage, summary);
    }

    private void Import()
    {
        List<PriceRow> rows = ParseRows(out string parseMessage);
        MatchSummary summary = Match(rows, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        preview = BuildSummary(parseMessage, summary);

        EditorUtility.DisplayDialog(
            "Sticker Prices Imported",
            $"Updated {summary.updated:N0} StickerData assets.\n" +
            $"Unmatched: {summary.unmatched.Count:N0}.\n" +
            $"Invalid rows: {summary.invalid.Count:N0}.",
            "OK");
    }

    private MatchSummary Match(List<PriceRow> rows, bool apply)
    {
        MatchSummary summary = new MatchSummary();

        if (database == null || rows == null)
            return summary;

        Dictionary<string, StickerData> byId =
            new Dictionary<string, StickerData>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<StickerData>> byName =
            new Dictionary<string, List<StickerData>>(
                StringComparer.OrdinalIgnoreCase);

        if (database.allStickers != null)
        {
            for (int i = 0; i < database.allStickers.Count; i++)
            {
                StickerData sticker = database.allStickers[i];

                if (sticker == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(sticker.apiId))
                    byId[sticker.apiId.Trim()] = sticker;

                string name = NormalizeName(sticker.DisplayName);

                if (!byName.TryGetValue(name, out List<StickerData> matches))
                {
                    matches = new List<StickerData>();
                    byName.Add(name, matches);
                }

                matches.Add(sticker);
            }
        }

        for (int i = 0; i < rows.Count; i++)
        {
            PriceRow row = rows[i];

            if (row == null || !row.valid)
            {
                summary.invalid.Add(
                    row != null ? row.error : $"Row {i + 2}: invalid.");
                continue;
            }

            StickerData match = null;

            if (!string.IsNullOrWhiteSpace(row.apiId))
                byId.TryGetValue(row.apiId.Trim(), out match);

            if (match == null && !string.IsNullOrWhiteSpace(row.name))
            {
                string key = NormalizeName(row.name);

                if (byName.TryGetValue(key, out List<StickerData> matches))
                {
                    if (matches.Count == 1)
                        match = matches[0];
                    else if (matches.Count > 1)
                    {
                        summary.ambiguous.Add(
                            $"{row.name}: {matches.Count} assets share this name; " +
                            "provide StickerApiId.");
                        continue;
                    }
                }
            }

            if (match == null)
            {
                summary.unmatched.Add(
                    !string.IsNullOrWhiteSpace(row.apiId)
                        ? row.apiId
                        : row.name);
                continue;
            }

            summary.matched++;

            if (!apply)
                continue;

            Undo.RecordObject(match, "Import sticker market value");
            float multiplier = multiplyImportedPrices
                ? Mathf.Max(0f, priceMultiplier)
                : 1f;
            match.marketValue = Mathf.Max(0f, row.marketValue * multiplier);
            match.vanillaPrice = match.marketValue;
            EditorUtility.SetDirty(match);
            summary.updated++;
        }

        if (apply && InventoryManager.Instance != null)
            InventoryManager.Instance.RecalculateCachedTotalMarketValue();

        return summary;
    }

    private List<PriceRow> ParseRows(out string message)
    {
        List<PriceRow> result = new List<PriceRow>();
        message = "";

        if (csvFile == null || string.IsNullOrWhiteSpace(csvFile.text))
        {
            message = "No CSV content.";
            return result;
        }

        string[] lines = csvFile.text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');

        int headerLine = -1;
        List<string> headers = null;
        char delimiter = ',';

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
            return result;
        }

        int idIndex = FindHeader(headers, "StickerApiId", "ApiId", "ID");
        int nameIndex = FindHeader(headers, "StickerName", "DisplayName", "Name");
        int valueIndex = FindHeader(headers, "MarketValue", "Price", "Value");

        if (valueIndex < 0 || (idIndex < 0 && nameIndex < 0))
        {
            message =
                "CSV needs MarketValue and either StickerApiId or StickerName.";
            return result;
        }

        for (int i = headerLine + 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            List<string> fields = ParseCsvLine(lines[i], delimiter);
            PriceRow row = new PriceRow
            {
                lineNumber = i + 1,
                apiId = GetField(fields, idIndex),
                name = GetField(fields, nameIndex)
            };

            string rawValue = GetField(fields, valueIndex)
                .Replace(" ", "")
                .Replace("Gold", "")
                .Trim();

            if (!float.TryParse(
                    rawValue,
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out row.marketValue))
            {
                row.valid = false;
                row.error = $"Row {row.lineNumber}: invalid MarketValue '{rawValue}'.";
            }
            else if (string.IsNullOrWhiteSpace(row.apiId) &&
                     string.IsNullOrWhiteSpace(row.name))
            {
                row.valid = false;
                row.error = $"Row {row.lineNumber}: no sticker identity.";
            }
            else
            {
                row.valid = true;
            }

            result.Add(row);
        }

        message = $"Parsed {result.Count:N0} data rows using '{delimiter}'.";
        return result;
    }

    private static string BuildSummary(
        string parseMessage,
        MatchSummary summary)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(parseMessage);
        builder.AppendLine($"Matched: {summary.matched:N0}");
        builder.AppendLine($"Updated: {summary.updated:N0}");
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
        return index >= 0 && index < fields.Count
            ? fields[index].Trim()
            : "";
    }

    private static string NormalizeName(string value)
    {
        string result = (value ?? "").Trim();

        if (result.StartsWith("Sticker | ", StringComparison.OrdinalIgnoreCase))
            result = result.Substring("Sticker | ".Length);

        return result.Trim();
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

    private sealed class PriceRow
    {
        public int lineNumber;
        public string apiId;
        public string name;
        public float marketValue;
        public bool valid;
        public string error;
    }

    private sealed class MatchSummary
    {
        public int matched;
        public int updated;
        public readonly List<string> unmatched = new List<string>();
        public readonly List<string> ambiguous = new List<string>();
        public readonly List<string> invalid = new List<string>();
    }
}
#endif
