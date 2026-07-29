#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public sealed class ByMykelStickerImporterWindow : EditorWindow
{
    private const string StickerJsonUrl =
        "https://raw.githubusercontent.com/ByMykel/CSGO-API/main/public/api/en/stickers.json";
    private const string CrateJsonUrl =
        "https://raw.githubusercontent.com/ByMykel/CSGO-API/main/public/api/en/crates.json";

    private const string RootFolder = "Assets/Data/Stickers";
    private const string StickerAssetFolder = RootFolder + "/StickerAssets";
    private const string StickerIconFolder = RootFolder + "/StickerIcons";
    private const string CapsuleAssetFolder = RootFolder + "/Capsules";
    private const string CapsuleIconFolder = RootFolder + "/CapsuleIcons";

    [SerializeField] private GameDatabase database;
    [SerializeField] private bool downloadMissingIcons = true;
    [SerializeField] private bool updateExistingMetadata = true;
    [SerializeField] private bool preserveExistingPrices = true;

    private Vector2 scroll;

    [MenuItem("Tools/Case Curator/Stickers/Import All ByMykel Stickers and Capsules")]
    public static void Open()
    {
        GetWindow<ByMykelStickerImporterWindow>(
            "Sticker Importer").Show();
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
            "ByMykel Sticker Import",
            EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Creates or updates every StickerData asset and every CaseData asset " +
            "whose API type is Sticker Capsule. All new prices begin at 0. " +
            "Existing manually edited prices can be preserved.",
            MessageType.Info);

        database = (GameDatabase)EditorGUILayout.ObjectField(
            "Game Database",
            database,
            typeof(GameDatabase),
            false);

        downloadMissingIcons = EditorGUILayout.Toggle(
            "Download Missing Icons",
            downloadMissingIcons);
        updateExistingMetadata = EditorGUILayout.Toggle(
            "Update Existing Metadata",
            updateExistingMetadata);
        preserveExistingPrices = EditorGUILayout.Toggle(
            "Preserve Existing Prices",
            preserveExistingPrices);

        EditorGUILayout.Space(8f);

        using (new EditorGUI.DisabledScope(database == null))
        {
            if (GUILayout.Button("IMPORT ALL STICKERS + CAPSULES", GUILayout.Height(36f)))
                ImportAll();
        }

        if (database == null)
        {
            EditorGUILayout.HelpBox(
                "Select the main GameDatabase. The window also resolves it " +
                "automatically when the project contains exactly one.",
                MessageType.Warning);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(StickerAssetFolder);
        EditorGUILayout.SelectableLabel(StickerIconFolder);
        EditorGUILayout.SelectableLabel(CapsuleAssetFolder);
        EditorGUILayout.SelectableLabel(CapsuleIconFolder);
        EditorGUILayout.EndScrollView();
    }

    private void ImportAll()
    {
        if (database == null)
        {
            EditorUtility.DisplayDialog(
                "GameDatabase Missing",
                "Assign the main GameDatabase first.",
                "OK");
            return;
        }

        EnsureFolders();

        try
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            string stickersJson = DownloadText(
                StickerJsonUrl,
                "Downloading sticker database");
            string cratesJson = DownloadText(
                CrateJsonUrl,
                "Downloading capsule database");

            StickerApiList stickers = ParseArray<StickerApiList>(
                stickersJson,
                "items");
            CrateApiList crates = ParseArray<CrateApiList>(
                cratesJson,
                "items");

            if (stickers == null || stickers.items == null ||
                crates == null || crates.items == null)
            {
                throw new InvalidOperationException(
                    "The ByMykel JSON could not be parsed.");
            }

            Dictionary<string, StickerData> stickerById =
                ImportStickers(stickers.items);
            Dictionary<string, CaseData> capsuleById =
                ImportCapsules(crates.items, stickerById);

            LinkStickerCapsules(stickers.items, stickerById, capsuleById);
            RegisterDatabase(stickerById, capsuleById);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();

            Debug.Log(
                $"Sticker import complete. Stickers: {stickerById.Count:N0}; " +
                $"Sticker Capsules: {capsuleById.Count:N0}.");

            EditorUtility.DisplayDialog(
                "Sticker Import Complete",
                $"Imported {stickerById.Count:N0} stickers and " +
                $"{capsuleById.Count:N0} Sticker Capsules.\n\n" +
                "New item prices are 0 Gold.",
                "OK");
        }
        catch (OperationCanceledException)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
            Debug.LogWarning("Sticker import cancelled. Completed assets were kept.");
        }
        catch (Exception exception)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
            Debug.LogError("Sticker import failed: " + exception);
            EditorUtility.DisplayDialog(
                "Sticker Import Failed",
                exception.Message,
                "OK");
        }
    }

    private Dictionary<string, StickerData> ImportStickers(
        StickerApiItem[] source)
    {
        Dictionary<string, StickerData> result =
            new Dictionary<string, StickerData>(StringComparer.Ordinal);

        for (int i = 0; i < source.Length; i++)
        {
            StickerApiItem api = source[i];

            if (api == null || string.IsNullOrWhiteSpace(api.id))
                continue;

            ThrowWhenCancelled(
                "Importing stickers",
                api.name,
                source.Length > 0 ? i / (float)source.Length : 0f);

            string displayName = CleanStickerName(api.name);
            string assetPath = StickerAssetFolder + "/" +
                               SafeFileName(api.id + "_" + displayName) +
                               ".asset";
            StickerData sticker =
                AssetDatabase.LoadAssetAtPath<StickerData>(assetPath);
            bool created = sticker == null;

            if (created)
            {
                sticker = CreateInstance<StickerData>();
                AssetDatabase.CreateAsset(sticker, assetPath);
            }

            float previousPrice = sticker.marketValue;

            if (created || updateExistingMetadata)
            {
                sticker.apiId = api.id;
                sticker.displayName = displayName;
                sticker.skinName = displayName;
                sticker.weaponName = "Sticker";
                sticker.stickerRarity = ParseStickerRarity(
                    api.rarity != null ? api.rarity.name : "High Grade");
                sticker.effect = api.effect ?? "";
                sticker.marketHashName = api.market_hash_name ?? "";
                sticker.tournamentEvent = api.tournament != null
                    ? api.tournament.name ?? ""
                    : "";
                sticker.teamName = api.team != null
                    ? api.team.name ?? ""
                    : "";
                sticker.playerName = api.player != null
                    ? api.player.name ?? ""
                    : "";
                sticker.year = ParseYear(sticker.tournamentEvent);
                sticker.sourceImageUrl = api.image ?? "";
                sticker.isVanilla = true;
                sticker.canBeStatTrak = false;
                sticker.canBeSouvenir = false;
                sticker.minFloat = 0f;
                sticker.maxFloat = 0f;
            }

            sticker.marketValue = created || !preserveExistingPrices
                ? 0f
                : Mathf.Max(0f, previousPrice);
            sticker.vanillaPrice = sticker.marketValue;
            sticker.rarity = StickerData.GetCompatibilityRarity(
                sticker.stickerRarity);

            if (downloadMissingIcons && !string.IsNullOrWhiteSpace(api.image))
            {
                string iconPath = StickerIconFolder + "/" +
                                  SafeFileName(api.id + "_" + displayName) +
                                  ".png";
                sticker.icon = DownloadSpriteWhenMissing(api.image, iconPath);
            }

            EditorUtility.SetDirty(sticker);
            result[api.id] = sticker;
        }

        return result;
    }

    private Dictionary<string, CaseData> ImportCapsules(
        CrateApiItem[] source,
        Dictionary<string, StickerData> stickerById)
    {
        Dictionary<string, CaseData> result =
            new Dictionary<string, CaseData>(StringComparer.Ordinal);
        List<CrateApiItem> capsules = new List<CrateApiItem>();

        for (int i = 0; i < source.Length; i++)
        {
            CrateApiItem item = source[i];

            if (item != null &&
                string.Equals(
                    item.type,
                    "Sticker Capsule",
                    StringComparison.OrdinalIgnoreCase))
            {
                capsules.Add(item);
            }
        }

        for (int i = 0; i < capsules.Count; i++)
        {
            CrateApiItem api = capsules[i];

            if (api == null || string.IsNullOrWhiteSpace(api.id))
                continue;

            ThrowWhenCancelled(
                "Importing Sticker Capsules",
                api.name,
                capsules.Count > 0 ? i / (float)capsules.Count : 0f);

            string displayName = string.IsNullOrWhiteSpace(api.name)
                ? api.id
                : api.name.Trim();
            string assetPath = CapsuleAssetFolder + "/" +
                               SafeFileName(api.id + "_" + displayName) +
                               ".asset";
            CaseData capsule = AssetDatabase.LoadAssetAtPath<CaseData>(assetPath);
            bool created = capsule == null;

            if (created)
            {
                capsule = CreateInstance<CaseData>();
                AssetDatabase.CreateAsset(capsule, assetPath);
            }

            float previousPrice = capsule.priceInGold;
            int previousXp = capsule.xpRewardOnOpen;
            PlayerRank previousRank = capsule.requiredRank;

            capsule.apiId = api.id;
            capsule.caseName = displayName;
            capsule.containerType = CaseContainerType.StickerCapsule;
            capsule.allowRareSpecialItem = false;
            capsule.allowStatTrak = false;
            capsule.forceSouvenirDrops = false;
            capsule.isCustomCase = false;
            capsule.shouldHaveRareSpecial = false;
            capsule.shopCategory = CaseShopCategory.StickerCapsules;
            capsule.priceInGold = created || !preserveExistingPrices
                ? 0f
                : Mathf.Max(0f, previousPrice);
            capsule.xpRewardOnOpen = created ? 1 : previousXp;
            capsule.requiredRank = created ? default : previousRank;

            if (downloadMissingIcons && !string.IsNullOrWhiteSpace(api.image))
            {
                string iconPath = CapsuleIconFolder + "/" +
                                  SafeFileName(api.id + "_" + displayName) +
                                  ".png";
                capsule.icon = DownloadSpriteWhenMissing(api.image, iconPath);
            }

            BuildCapsuleDrops(capsule, api.contains, stickerById);
            EditorUtility.SetDirty(capsule);
            result[api.id] = capsule;
        }

        return result;
    }

    private static void BuildCapsuleDrops(
        CaseData capsule,
        CrateContainsItem[] contains,
        Dictionary<string, StickerData> stickerById)
    {
        capsule.dropPool = new List<WeightedDrop>();
        capsule.rarityChances = new List<RarityChance>();

        if (contains == null)
            return;

        HashSet<StickerRarity> present = new HashSet<StickerRarity>();

        for (int i = 0; i < contains.Length; i++)
        {
            CrateContainsItem entry = contains[i];

            if (entry == null || string.IsNullOrWhiteSpace(entry.id) ||
                !stickerById.TryGetValue(entry.id, out StickerData sticker) ||
                sticker == null)
            {
                continue;
            }

            capsule.dropPool.Add(new WeightedDrop
            {
                skin = sticker,
                weight = 1f
            });
            present.Add(sticker.stickerRarity);
        }

        List<StickerRarity> rarities = new List<StickerRarity>(present);
        rarities.Sort((a, b) => ((int)a).CompareTo((int)b));
        float[] chances = GetTierChances(rarities.Count);

        for (int i = 0; i < rarities.Count; i++)
        {
            capsule.rarityChances.Add(new RarityChance
            {
                rarity = StickerData.GetCompatibilityRarity(rarities[i]),
                chance = chances[i]
            });
        }
    }

    private static float[] GetTierChances(int count)
    {
        switch (count)
        {
            case 1: return new[] { 100f };
            case 2: return new[] { 80f, 20f };
            case 3: return new[] { 80f, 16f, 4f };
            case 4: return new[] { 80f, 16f, 3.2f, 0.8f };
            default: return new[] { 80f, 16f, 3.2f, 0.64f, 0.16f };
        }
    }

    private static void LinkStickerCapsules(
        StickerApiItem[] source,
        Dictionary<string, StickerData> stickerById,
        Dictionary<string, CaseData> capsuleById)
    {
        for (int i = 0; i < source.Length; i++)
        {
            StickerApiItem api = source[i];

            if (api == null || string.IsNullOrWhiteSpace(api.id) ||
                !stickerById.TryGetValue(api.id, out StickerData sticker) ||
                sticker == null)
            {
                continue;
            }

            sticker.capsules = new List<CaseData>();

            if (api.crates != null)
            {
                for (int j = 0; j < api.crates.Length; j++)
                {
                    ApiReference crate = api.crates[j];

                    if (crate != null &&
                        capsuleById.TryGetValue(crate.id, out CaseData capsule) &&
                        capsule != null &&
                        !sticker.capsules.Contains(capsule))
                    {
                        sticker.capsules.Add(capsule);
                    }
                }
            }

            EditorUtility.SetDirty(sticker);
        }
    }

    private void RegisterDatabase(
        Dictionary<string, StickerData> stickerById,
        Dictionary<string, CaseData> capsuleById)
    {
        Undo.RecordObject(database, "Import stickers and Sticker Capsules");

        if (database.allStickers == null)
            database.allStickers = new List<StickerData>();
        if (database.allSkins == null)
            database.allSkins = new List<SkinData>();
        if (database.allCases == null)
            database.allCases = new List<CaseData>();

        foreach (StickerData sticker in stickerById.Values)
        {
            ReplaceByApiId(database.allStickers, sticker);
            ReplaceSkinByApiId(database.allSkins, sticker);
        }

        foreach (CaseData capsule in capsuleById.Values)
            ReplaceCaseByApiId(database.allCases, capsule);

        database.allStickers.Sort((a, b) => string.Compare(
            a != null ? a.DisplayName : "",
            b != null ? b.DisplayName : "",
            StringComparison.OrdinalIgnoreCase));
        database.allSkins.Sort((a, b) => string.Compare(
            a != null ? a.apiId : "",
            b != null ? b.apiId : "",
            StringComparison.Ordinal));
        database.allCases.Sort((a, b) => string.Compare(
            a != null ? a.caseName : "",
            b != null ? b.caseName : "",
            StringComparison.OrdinalIgnoreCase));

        EditorUtility.SetDirty(database);
    }

    private static void ReplaceByApiId(
        List<StickerData> list,
        StickerData sticker)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            StickerData existing = list[i];

            if (existing == null)
            {
                list.RemoveAt(i);
                continue;
            }

            if (existing.apiId == sticker.apiId)
            {
                list[i] = sticker;
                return;
            }
        }

        list.Add(sticker);
    }

    private static void ReplaceSkinByApiId(
        List<SkinData> list,
        StickerData sticker)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            SkinData existing = list[i];

            if (existing == null)
            {
                list.RemoveAt(i);
                continue;
            }

            if (existing.apiId == sticker.apiId)
            {
                list[i] = sticker;
                return;
            }
        }

        list.Add(sticker);
    }

    private static void ReplaceCaseByApiId(
        List<CaseData> list,
        CaseData capsule)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            CaseData existing = list[i];

            if (existing == null)
            {
                list.RemoveAt(i);
                continue;
            }

            if (existing.apiId == capsule.apiId)
            {
                list[i] = capsule;
                return;
            }
        }

        list.Add(capsule);
    }

    private static Sprite DownloadSpriteWhenMissing(
        string url,
        string assetPath)
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);

        if (existing != null)
            return existing;

        byte[] bytes;

        using (WebClient client = new WebClient())
            bytes = client.DownloadData(url);

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (!texture.LoadImage(bytes))
        {
            DestroyImmediate(texture);
            throw new InvalidOperationException("Could not decode image: " + url);
        }

        byte[] png = texture.EncodeToPNG();
        DestroyImmediate(texture);
        File.WriteAllBytes(assetPath, png);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static string DownloadText(string url, string progressTitle)
    {
        EditorUtility.DisplayProgressBar(progressTitle, url, 0f);

        using (WebClient client = new WebClient())
            return client.DownloadString(url);
    }

    private static T ParseArray<T>(string json, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        return JsonUtility.FromJson<T>(
            "{\"" + fieldName + "\":" + json + "}");
    }

    private static void ThrowWhenCancelled(
        string title,
        string info,
        float progress)
    {
        if (EditorUtility.DisplayCancelableProgressBar(
                title,
                info ?? "",
                Mathf.Clamp01(progress)))
        {
            throw new OperationCanceledException();
        }
    }

    private static StickerRarity ParseStickerRarity(string value)
    {
        if (StickerRarityUtility.TryParse(value, out StickerRarity rarity))
            return rarity;

        return StickerRarity.HighGrade;
    }

    private static string CleanStickerName(string value)
    {
        string name = string.IsNullOrWhiteSpace(value)
            ? "Sticker"
            : value.Trim();

        if (name.StartsWith("Sticker | ", StringComparison.OrdinalIgnoreCase))
            name = name.Substring("Sticker | ".Length);

        return name.Trim();
    }

    private static int ParseYear(string value)
    {
        Match match = Regex.Match(value ?? "", @"\b(20\d{2}|19\d{2})\b");
        return match.Success && int.TryParse(match.Value, out int year)
            ? year
            : 0;
    }

    private static string SafeFileName(string value)
    {
        string safe = Regex.Replace(value ?? "item", @"[^A-Za-z0-9._-]+", "_");
        safe = safe.Trim('_', '.', ' ');
        return string.IsNullOrWhiteSpace(safe) ? "item" : safe;
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

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Data");
        EnsureFolder("Assets/Data", "Stickers");
        EnsureFolder(RootFolder, "StickerAssets");
        EnsureFolder(RootFolder, "StickerIcons");
        EnsureFolder(RootFolder, "Capsules");
        EnsureFolder(RootFolder, "CapsuleIcons");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    [Serializable]
    private sealed class StickerApiList
    {
        public StickerApiItem[] items;
    }

    [Serializable]
    private sealed class CrateApiList
    {
        public CrateApiItem[] items;
    }

    [Serializable]
    private sealed class StickerApiItem
    {
        public string id;
        public string name;
        public ApiRarity rarity;
        public ApiReference[] crates;
        public string type;
        public string effect;
        public string market_hash_name;
        public ApiNamedEntity tournament;
        public ApiNamedEntity team;
        public ApiNamedEntity player;
        public string image;
    }

    [Serializable]
    private sealed class CrateApiItem
    {
        public string id;
        public string name;
        public string type;
        public string image;
        public CrateContainsItem[] contains;
    }

    [Serializable]
    private sealed class CrateContainsItem
    {
        public string id;
        public string name;
        public ApiRarity rarity;
        public string image;
    }

    [Serializable]
    private sealed class ApiRarity
    {
        public string id;
        public string name;
        public string color;
    }

    [Serializable]
    private sealed class ApiReference
    {
        public string id;
        public string name;
        public string image;
    }

    [Serializable]
    private sealed class ApiNamedEntity
    {
        public int id;
        public string name;
        public string tag;
        public string code;
    }
}
#endif
