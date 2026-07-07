#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class CaseBalanceCsvImporter : EditorWindow
{
    [Header("CSV")]
    public TextAsset cratesDataCsv;

    [Header("Generated Assets")]
    public string generatedAssetFolder = "Assets/Generated/OpenableContainers";
    public bool createMissingCaseAssets = true;
    public bool fillNewCollectionDropPoolsFromSkinData = true;
    public bool overwriteExistingDropPools = false;

    [Header("Rarity Chances")]
    public bool addDefaultCollectionOddsIfEmpty = true;

    private readonly List<string> logLines = new List<string>();

    [MenuItem("Case Catcher/Balance/Import Crates Data CSV")]
    public static void Open()
    {
        GetWindow<CaseBalanceCsvImporter>("Crates CSV Importer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Case Catcher - Crates Data CSV Importer", EditorStyles.boldLabel);

        cratesDataCsv = (TextAsset)EditorGUILayout.ObjectField(
            "Crates Data CSV",
            cratesDataCsv,
            typeof(TextAsset),
            false);

        generatedAssetFolder = EditorGUILayout.TextField(
            "Generated Asset Folder",
            generatedAssetFolder);

        createMissingCaseAssets = EditorGUILayout.Toggle(
            "Create Missing CaseData Assets",
            createMissingCaseAssets);

        fillNewCollectionDropPoolsFromSkinData = EditorGUILayout.Toggle(
            "Fill New Collection Drop Pools",
            fillNewCollectionDropPoolsFromSkinData);

        overwriteExistingDropPools = EditorGUILayout.Toggle(
            "Overwrite Existing Drop Pools",
            overwriteExistingDropPools);

        addDefaultCollectionOddsIfEmpty = EditorGUILayout.Toggle(
            "Add Default Collection Odds If Empty",
            addDefaultCollectionOddsIfEmpty);

        EditorGUILayout.Space();

        if (GUILayout.Button("Import / Update CaseData From CSV", GUILayout.Height(35)))
        {
            Import();
        }

        EditorGUILayout.Space();

        foreach (string line in logLines)
        {
            EditorGUILayout.LabelField(line);
        }
    }

    private void Import()
    {
        logLines.Clear();

        if (cratesDataCsv == null)
        {
            Log("No CSV assigned.");
            return;
        }

        EnsureFolderExists(generatedAssetFolder);

        List<Dictionary<string, string>> rows =
            ParseCsvToRows(cratesDataCsv.text);

        CaseData[] allCases = FindAllAssets<CaseData>();
        SkinData[] allSkins = FindAllAssets<SkinData>();

        int updated = 0;
        int created = 0;
        int skipped = 0;

        foreach (Dictionary<string, string> row in rows)
        {
            string containerName = Get(row, "Container");

            if (string.IsNullOrWhiteSpace(containerName))
            {
                skipped++;
                continue;
            }

            string typeText = Get(row, "Type");

            CaseData caseData = FindCaseByName(allCases, containerName);

            if (caseData == null && createMissingCaseAssets)
            {
                caseData = CreateCaseAsset(containerName, typeText);
                created++;

                allCases = FindAllAssets<CaseData>();
            }

            if (caseData == null)
            {
                Log($"Skipped missing asset: {containerName}");
                skipped++;
                continue;
            }

            ApplyRowToCaseData(caseData, row);

            bool isCollectionLike =
                caseData.containerType == CaseContainerType.CollectionPackage ||
                caseData.containerType == CaseContainerType.SouvenirPackage;

            if (isCollectionLike && fillNewCollectionDropPoolsFromSkinData)
            {
                if (overwriteExistingDropPools || caseData.dropPool == null || caseData.dropPool.Count == 0)
                {
                    FillDropPoolFromSkinCollection(caseData, allSkins);
                }

                if (addDefaultCollectionOddsIfEmpty &&
                    (caseData.rarityChances == null || caseData.rarityChances.Count == 0))
                {
                    AddDefaultCollectionOdds(caseData);
                }
            }

            EditorUtility.SetDirty(caseData);
            updated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Log($"Created: {created}");
        Log($"Updated: {updated}");
        Log($"Skipped: {skipped}");
        Log("Done. Now rebuild/refresh your GameDatabase so the shop sees new assets.");
    }

    private void ApplyRowToCaseData(CaseData caseData, Dictionary<string, string> row)
    {
        string containerName = Get(row, "Container");
        string typeText = Get(row, "Type");
        string rarityText = Get(row, "CaseRarity");

        caseData.caseName = containerName;

        if (string.IsNullOrWhiteSpace(caseData.apiId))
        {
            caseData.apiId = BuildApiId(containerName, typeText);
        }

        if (TryParseFloat(Get(row, "Price"), out float price))
        {
            caseData.priceInGold = price;
        }

        if (int.TryParse(Get(row, "XP"), out int xp))
        {
            caseData.xpRewardOnOpen = xp;
        }

        caseData.requiredRank = ParsePlayerRank(Get(row, "UnlockLevel"));
        caseData.quality = ParseCaseQuality(rarityText);

        caseData.containerType = ParseContainerType(typeText);
        caseData.shopCategory = ParseShopCategory(typeText);

        bool hasSouvenirs = ParseYesNo(Get(row, "HasSouvenirs"));
        bool hasStatTrak = ParseYesNo(Get(row, "HasStattrak"));
        bool hasRareSpecial = ParseYesNo(Get(row, "HasRareSpecial"));

        caseData.allowStatTrak = hasStatTrak;
        caseData.allowRareSpecialItem = hasRareSpecial;
        caseData.shouldHaveRareSpecial = hasRareSpecial;

        caseData.forceSouvenirDrops =
            caseData.containerType == CaseContainerType.SouvenirPackage ||
            hasSouvenirs && caseData.containerType == CaseContainerType.SouvenirPackage;

        caseData.isCustomCase =
            caseData.containerType == CaseContainerType.CustomCase;
    }

    private CaseData CreateCaseAsset(string containerName, string typeText)
    {
        CaseData asset = CreateInstance<CaseData>();

        asset.caseName = containerName;
        asset.apiId = BuildApiId(containerName, typeText);
        asset.containerType = ParseContainerType(typeText);
        asset.shopCategory = ParseShopCategory(typeText);

        string fileName = MakeSafeFileName(containerName) + ".asset";
        string path = AssetDatabase.GenerateUniqueAssetPath(
            $"{generatedAssetFolder}/{fileName}");

        AssetDatabase.CreateAsset(asset, path);

        Log($"Created CaseData: {containerName}");

        return asset;
    }

    private void FillDropPoolFromSkinCollection(CaseData caseData, SkinData[] allSkins)
    {
        if (caseData.dropPool == null)
            caseData.dropPool = new List<WeightedDrop>();

        caseData.dropPool.Clear();

        string wantedCollection = NormalizeCollectionName(caseData.caseName);

        foreach (SkinData skin in allSkins)
        {
            if (skin == null)
                continue;

            string skinCollection = NormalizeCollectionName(skin.collection);

            if (skin.collectionData != null)
            {
                // This is intentionally not using collectionData fields directly because
                // CollectionData field names may change. The string collection field is stable.
            }

            if (skinCollection != wantedCollection)
                continue;

            if (caseData.containerType == CaseContainerType.SouvenirPackage &&
                !skin.canBeSouvenir)
                continue;

            WeightedDrop drop = new WeightedDrop();
            drop.skin = skin;
            drop.weight = 1f;

            caseData.dropPool.Add(drop);
        }

        Log($"{caseData.caseName}: filled drop pool with {caseData.dropPool.Count} skins.");
    }

    private void AddDefaultCollectionOdds(CaseData caseData)
    {
        caseData.rarityChances = new List<RarityChance>
        {
            new RarityChance { rarity = Rarity.Consumer, chance = 79.92f },
            new RarityChance { rarity = Rarity.Industrial, chance = 15.98f },
            new RarityChance { rarity = Rarity.MilSpec, chance = 3.20f },
            new RarityChance { rarity = Rarity.Restricted, chance = 0.64f },
            new RarityChance { rarity = Rarity.Classified, chance = 0.128f },
            new RarityChance { rarity = Rarity.Covert, chance = 0.0256f }
        };

        Log($"{caseData.caseName}: added default collection odds.");
    }

    private CaseData FindCaseByName(CaseData[] allCases, string containerName)
    {
        string wanted = NormalizeId(containerName);

        foreach (CaseData caseData in allCases)
        {
            if (caseData == null)
                continue;

            if (NormalizeId(caseData.caseName) == wanted)
                return caseData;

            if (!string.IsNullOrWhiteSpace(caseData.apiId) &&
                NormalizeId(caseData.apiId) == wanted)
                return caseData;
        }

        return null;
    }

    private static T[] FindAllAssets<T>() where T : UnityEngine.Object
    {
        List<T> assets = new List<T>();

        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset != null)
                assets.Add(asset);
        }

        return assets.ToArray();
    }

    private static string BuildApiId(string containerName, string typeText)
    {
        string prefix = "cc_case";

        CaseContainerType type = ParseContainerType(typeText);

        switch (type)
        {
            case CaseContainerType.CollectionPackage:
                prefix = "cc_collection";
                break;

            case CaseContainerType.SouvenirPackage:
                prefix = "cc_souvenir";
                break;

            case CaseContainerType.CustomCase:
                prefix = "cc_custom";
                break;

            default:
                prefix = "cc_case";
                break;
        }

        return prefix + "_" + Slug(containerName);
    }

    private static CaseContainerType ParseContainerType(string text)
    {
        string value = NormalizeId(text);

        if (value.Contains("souvenir"))
            return CaseContainerType.SouvenirPackage;

        if (value.Contains("standardcollections") ||
            value.Contains("collection"))
            return CaseContainerType.CollectionPackage;

        if (value.Contains("custom"))
            return CaseContainerType.CustomCase;

        return CaseContainerType.WeaponCase;
    }

    private static CaseShopCategory ParseShopCategory(string text)
    {
        CaseContainerType type = ParseContainerType(text);

        switch (type)
        {
            case CaseContainerType.CollectionPackage:
                return CaseShopCategory.Collections;

            case CaseContainerType.SouvenirPackage:
                return CaseShopCategory.SouvenirCollections;

            case CaseContainerType.CustomCase:
                return CaseShopCategory.CustomCases;

            default:
                return CaseShopCategory.Cases;
        }
    }

    private static CaseQuality ParseCaseQuality(string text)
    {
        string value = NormalizeId(text);

        switch (value)
        {
            case "consumer":
                return CaseQuality.Consumer;

            case "industrial":
                return CaseQuality.Industrial;

            case "milspec":
                return CaseQuality.MilSpec;

            case "restricted":
                return CaseQuality.Restricted;

            case "classified":
                return CaseQuality.Classified;

            case "covert":
                return CaseQuality.Covert;

            case "gold":
                return CaseQuality.Gold;

            default:
                return CaseQuality.Consumer;
        }
    }

    private static PlayerRank ParsePlayerRank(string text)
    {
        string value = NormalizeId(text);

        switch (value)
        {
            case "silveri":
                return PlayerRank.SilverI;
            case "silverii":
                return PlayerRank.SilverII;
            case "silveriii":
                return PlayerRank.SilverIII;
            case "silverelite":
                return PlayerRank.SilverElite;
            case "silverelitemaster":
                return PlayerRank.SilverEliteMaster;

            case "goldnovai":
                return PlayerRank.GoldNovaI;
            case "goldnovaii":
                return PlayerRank.GoldNovaII;
            case "goldnovaiii":
                return PlayerRank.GoldNovaIII;
            case "goldnovamaster":
                return PlayerRank.GoldNovaMaster;

            case "masterguardiani":
                return PlayerRank.MasterGuardianI;
            case "masterguardianii":
                return PlayerRank.MasterGuardianII;
            case "masterguardianelite":
                return PlayerRank.MasterGuardianElite;
            case "distinguishedmasterguardian":
                return PlayerRank.DistinguishedMasterGuardian;

            case "legendaryeagle":
                return PlayerRank.LegendaryEagle;
            case "legendaryeaglemaster":
                return PlayerRank.LegendaryEagleMaster;
            case "suprememasterfirstclass":
                return PlayerRank.SupremeMasterFirstClass;

            case "globalelite":
                return PlayerRank.GlobalElite;
            case "globaleliteii":
                return PlayerRank.GlobalEliteII;
            case "globaleliteiii":
                return PlayerRank.GlobalEliteIII;
            case "globaleliteiv":
                return PlayerRank.GlobalEliteIV;
            case "globalelitev":
                return PlayerRank.GlobalEliteV;
            case "globalelitevi":
                return PlayerRank.GlobalEliteVI;
            case "globalelitevii":
                return PlayerRank.GlobalEliteVII;
            case "globaleliteviii":
                return PlayerRank.GlobalEliteVIII;
            case "globaleliteix":
                return PlayerRank.GlobalEliteIX;
            case "globalelitex":
                return PlayerRank.GlobalEliteX;
            case "theglobalelite":
                return PlayerRank.TheGlobalElite;

            default:
                return PlayerRank.SilverI;
        }
    }

    private static bool ParseYesNo(string text)
    {
        string value = NormalizeId(text);

        return value == "yes" ||
               value == "true" ||
               value == "1" ||
               value == "y";
    }

    private static bool TryParseFloat(string text, out float value)
    {
        text = text.Replace(",", ".").Trim();

        return float.TryParse(
            text,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static string NormalizeCollectionName(string text)
    {
        string value = text;

        value = value.Replace("Souvenir", "");
        value = value.Replace("Package", "");
        value = value.Replace("Collection", "");
        value = value.Replace("The", "");

        return NormalizeId(value);
    }

    private static string NormalizeId(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        StringBuilder builder = new StringBuilder();

        foreach (char c in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
                builder.Append(c);
        }

        return builder.ToString();
    }

    private static string Slug(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "unnamed";

        StringBuilder builder = new StringBuilder();

        foreach (char c in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (c == ' ' || c == '-' || c == '_' || c == ':' || c == '/')
            {
                if (builder.Length > 0 && builder[builder.Length - 1] != '_')
                    builder.Append('_');
            }
        }

        return builder.ToString().Trim('_');
    }

    private static string MakeSafeFileName(string text)
    {
        string fileName = Slug(text);

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "new_container";

        return fileName;
    }

    private static string Get(Dictionary<string, string> row, string key)
    {
        if (row.TryGetValue(key, out string value))
            return value;

        return "";
    }

    private static List<Dictionary<string, string>> ParseCsvToRows(string csv)
    {
        List<List<string>> table = ParseCsv(csv);
        List<Dictionary<string, string>> rows = new List<Dictionary<string, string>>();

        if (table.Count <= 1)
            return rows;

        List<string> headers = table[0];

        for (int r = 1; r < table.Count; r++)
        {
            Dictionary<string, string> row = new Dictionary<string, string>();

            for (int c = 0; c < headers.Count; c++)
            {
                string header = headers[c];

                if (string.IsNullOrWhiteSpace(header))
                    continue;

                string value = c < table[r].Count ? table[r][c] : "";
                row[header.Trim()] = value.Trim();
            }

            rows.Add(row);
        }

        return rows;
    }

    private static List<List<string>> ParseCsv(string csv)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> currentRow = new List<string>();
        StringBuilder currentValue = new StringBuilder();

        bool insideQuotes = false;

        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];

            if (c == '"')
            {
                if (insideQuotes && i + 1 < csv.Length && csv[i + 1] == '"')
                {
                    currentValue.Append('"');
                    i++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }
            }
            else if (c == ',' && !insideQuotes)
            {
                currentRow.Add(currentValue.ToString());
                currentValue.Clear();
            }
            else if ((c == '\n' || c == '\r') && !insideQuotes)
            {
                if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                    i++;

                currentRow.Add(currentValue.ToString());
                currentValue.Clear();

                bool hasAnyValue = false;

                foreach (string value in currentRow)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        hasAnyValue = true;
                        break;
                    }
                }

                if (hasAnyValue)
                    rows.Add(currentRow);

                currentRow = new List<string>();
            }
            else
            {
                currentValue.Append(c);
            }
        }

        currentRow.Add(currentValue.ToString());

        if (currentRow.Count > 1 || !string.IsNullOrWhiteSpace(currentRow[0]))
            rows.Add(currentRow);

        return rows;
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts = folderPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private void Log(string message)
    {
        logLines.Add(message);
        Debug.Log(message);
    }
}
#endif