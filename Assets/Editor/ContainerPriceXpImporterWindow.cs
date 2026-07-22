#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports only priceInGold and xpRewardOnOpen from the compact CSV exported
/// from the authoritative Container Balance sheet. No ranks, rarity odds,
/// flags, drop pools, icons or other CaseData fields are changed.
/// </summary>
public class ContainerPriceXpImporterWindow : EditorWindow
{
    private const string DefaultCsvPath =
        "Assets/Data/ImportData/ContainerPricesXp.csv";

    [SerializeField] private GameDatabase database;
    [SerializeField] private TextAsset balanceCsv;
    [SerializeField] private bool logEveryMatchedContainer;

    [MenuItem("Case Curator/Containers/Import Prices and XP")]
    public static void OpenWindow()
    {
        ContainerPriceXpImporterWindow window =
            GetWindow<ContainerPriceXpImporterWindow>();

        window.titleContent = new GUIContent("Container Price + XP");
        window.minSize = new Vector2(560f, 330f);
        window.Show();
    }

    private void OnEnable()
    {
        if (balanceCsv == null)
        {
            balanceCsv = AssetDatabase.LoadAssetAtPath<TextAsset>(
                DefaultCsvPath);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Container Balance — Price and XP Import",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Updates only CaseData.priceInGold and CaseData.xpRewardOnOpen. " +
            "All other CaseData settings remain untouched.",
            MessageType.Info);

        EditorGUILayout.Space(8f);

        database = (GameDatabase)EditorGUILayout.ObjectField(
            "Game Database",
            database,
            typeof(GameDatabase),
            false);

        balanceCsv = (TextAsset)EditorGUILayout.ObjectField(
            "Price + XP CSV",
            balanceCsv,
            typeof(TextAsset),
            false);

        logEveryMatchedContainer = EditorGUILayout.ToggleLeft(
            "Log every matched container",
            logEveryMatchedContainer);

        EditorGUILayout.Space(10f);

        EditorGUILayout.HelpBox(
            "The bundled CSV was exported from CaseCuratorBalancingSheet3.4 " +
            "using Source / Collection, CaseData Asset Name, Container Type, " +
            "Shop Category, Price and XP Reward. Collection packages are " +
            "matched through their drop-pool CollectionData when names differ.",
            MessageType.None);

        EditorGUILayout.Space(12f);

        bool ready = database != null &&
                     balanceCsv != null &&
                     database.allCases != null;

        using (new EditorGUI.DisabledScope(!ready))
        {
            if (GUILayout.Button("Preview Matches", GUILayout.Height(34f)))
                RunImport(false);

            if (GUILayout.Button(
                    "Import Prices and XP",
                    GUILayout.Height(42f)))
            {
                bool confirmed = EditorUtility.DisplayDialog(
                    "Import Container Prices and XP",
                    "Update only priceInGold and xpRewardOnOpen on matched " +
                    "CaseData assets?",
                    "Import",
                    "Cancel");

                if (confirmed)
                    RunImport(true);
            }
        }

        if (!ready)
        {
            EditorGUILayout.HelpBox(
                "Assign the main GameDatabase and the bundled CSV.",
                MessageType.Warning);
        }
    }

    private void RunImport(bool applyChanges)
    {
        List<BalanceRow> rows = ParseRows(balanceCsv != null
            ? balanceCsv.text
            : "");

        int matched = 0;
        int changed = 0;
        int unchanged = 0;
        int unmatched = 0;
        int ambiguous = 0;
        int invalid = 0;

        List<string> unmatchedRows = new List<string>();
        List<string> ambiguousRows = new List<string>();

        for (int i = 0; i < rows.Count; i++)
        {
            BalanceRow row = rows[i];

            if (!row.valid)
            {
                invalid++;
                continue;
            }

            MatchResult result = FindBestMatch(row);

            if (result.ambiguous)
            {
                ambiguous++;
                ambiguousRows.Add(row.DisplayIdentity);
                continue;
            }

            if (result.caseData == null)
            {
                unmatched++;
                unmatchedRows.Add(row.DisplayIdentity);
                continue;
            }

            matched++;
            CaseData caseData = result.caseData;

            bool priceChanged =
                !Mathf.Approximately(caseData.priceInGold, row.priceGold);

            bool xpChanged =
                caseData.xpRewardOnOpen != row.xpReward;

            if (!priceChanged && !xpChanged)
            {
                unchanged++;

                if (logEveryMatchedContainer)
                {
                    Debug.Log(
                        $"Container balance unchanged: {caseData.caseName} " +
                        $"({caseData.priceInGold:0.##} Gold, " +
                        $"{caseData.xpRewardOnOpen} XP)",
                        caseData);
                }

                continue;
            }

            changed++;

            if (logEveryMatchedContainer || !applyChanges)
            {
                Debug.Log(
                    $"Container balance {(applyChanges ? "update" : "preview")}: " +
                    $"{caseData.caseName} | " +
                    $"Price {caseData.priceInGold:0.##} → {row.priceGold:0.##} | " +
                    $"XP {caseData.xpRewardOnOpen} → {row.xpReward}",
                    caseData);
            }

            if (!applyChanges)
                continue;

            Undo.RecordObject(caseData, "Import Container Price and XP");
            caseData.priceInGold = Mathf.Max(0f, row.priceGold);
            caseData.xpRewardOnOpen = Mathf.Max(0, row.xpReward);
            EditorUtility.SetDirty(caseData);
        }

        if (applyChanges)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        if (unmatchedRows.Count > 0)
        {
            Debug.LogWarning(
                "ContainerPriceXpImporter unmatched rows:\n- " +
                string.Join("\n- ", unmatchedRows));
        }

        if (ambiguousRows.Count > 0)
        {
            Debug.LogWarning(
                "ContainerPriceXpImporter ambiguous rows:\n- " +
                string.Join("\n- ", ambiguousRows));
        }

        string action = applyChanges ? "Import complete" : "Preview complete";

        EditorUtility.DisplayDialog(
            "Container Price and XP",
            $"{action}.\n\n" +
            $"Rows read: {rows.Count}\n" +
            $"Matched: {matched}\n" +
            $"Changed: {changed}\n" +
            $"Already correct: {unchanged}\n" +
            $"Unmatched: {unmatched}\n" +
            $"Ambiguous: {ambiguous}\n" +
            $"Invalid rows: {invalid}\n\n" +
            (applyChanges
                ? "Only price and earned XP were modified."
                : "No assets were modified."),
            "OK");
    }

    private MatchResult FindBestMatch(BalanceRow row)
    {
        CaseData best = null;
        int bestScore = int.MinValue;
        bool tied = false;

        for (int i = 0; i < database.allCases.Count; i++)
        {
            CaseData candidate = database.allCases[i];

            if (candidate == null ||
                candidate.containerType != row.containerType ||
                candidate.shopCategory != row.shopCategory)
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
            caseData = tied ? null : best,
            ambiguous = tied
        };
    }

    private static int ScoreCandidate(CaseData candidate, BalanceRow row)
    {
        string candidateObjectName = Normalize(candidate.name);
        string candidateDisplayName = Normalize(candidate.caseName);
        string rowAssetName = Normalize(row.caseDataAssetName);
        string rowSourceName = Normalize(row.sourceName);
        string candidateSourceName = Normalize(
            GetPrimaryCollectionName(candidate));

        int score = 0;

        if (!string.IsNullOrWhiteSpace(candidateSourceName) &&
            candidateSourceName == rowSourceName)
        {
            score += 500;
        }

        if (candidateDisplayName == rowAssetName)
            score += 300;

        if (candidateObjectName == rowAssetName)
            score += 260;

        if (candidateDisplayName == rowSourceName)
            score += 220;

        if (candidateObjectName == rowSourceName)
            score += 180;

        // Weapon cases normally have no CollectionData source, so exact case
        // names are their strongest match.
        if (row.containerType == CaseContainerType.WeaponCase &&
            candidateDisplayName == rowSourceName)
        {
            score += 500;
        }

        return score;
    }

    private static string GetPrimaryCollectionName(CaseData caseData)
    {
        if (caseData == null || caseData.dropPool == null)
            return "";

        for (int i = 0; i < caseData.dropPool.Count; i++)
        {
            WeightedDrop drop = caseData.dropPool[i];

            if (drop == null ||
                drop.skin == null ||
                drop.skin.collectionData == null)
            {
                continue;
            }

            CollectionData collection = drop.skin.collectionData;

            return !string.IsNullOrWhiteSpace(collection.collectionName)
                ? collection.collectionName
                : collection.name;
        }

        return "";
    }

    private static List<BalanceRow> ParseRows(string csv)
    {
        List<BalanceRow> rows = new List<BalanceRow>();

        if (string.IsNullOrWhiteSpace(csv))
            return rows;

        string[] lines = csv.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            List<string> columns = ParseCsvLine(lines[i]);

            if (columns.Count < 6)
            {
                rows.Add(BalanceRow.Invalid($"Line {i + 1}"));
                continue;
            }

            bool typeValid = Enum.TryParse(
                columns[2],
                true,
                out CaseContainerType containerType);

            bool categoryValid = Enum.TryParse(
                columns[3],
                true,
                out CaseShopCategory shopCategory);

            bool priceValid = float.TryParse(
                columns[4],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float priceGold);

            bool xpValid = int.TryParse(
                columns[5],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int xpReward);

            rows.Add(
                new BalanceRow
                {
                    sourceName = columns[0].Trim(),
                    caseDataAssetName = columns[1].Trim(),
                    containerType = containerType,
                    shopCategory = shopCategory,
                    priceGold = priceGold,
                    xpReward = xpReward,
                    valid = typeValid &&
                            categoryValid &&
                            priceValid &&
                            xpValid &&
                            !string.IsNullOrWhiteSpace(columns[0])
                });
        }

        return rows;
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

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string lowered = value.Trim().ToLowerInvariant();
        lowered = lowered.Replace("souvenir", "");
        lowered = lowered.Replace("collection package", "collection");
        lowered = lowered.Replace("package", "");

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < lowered.Length; i++)
        {
            char character = lowered[i];

            if (char.IsLetterOrDigit(character))
                builder.Append(character);
        }

        return builder.ToString();
    }

    private sealed class MatchResult
    {
        public CaseData caseData;
        public bool ambiguous;
    }

    private sealed class BalanceRow
    {
        public string sourceName;
        public string caseDataAssetName;
        public CaseContainerType containerType;
        public CaseShopCategory shopCategory;
        public float priceGold;
        public int xpReward;
        public bool valid;

        public string DisplayIdentity =>
            $"{sourceName} [{containerType}/{shopCategory}]";

        public static BalanceRow Invalid(string name)
        {
            return new BalanceRow
            {
                sourceName = name,
                valid = false
            };
        }
    }
}
#endif
