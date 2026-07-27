#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rebuilds the Arsenal/Rare Special Vault catalog split and creates the six
/// sequential Rare Vault stage upgrades. Re-running is safe and preserves
/// manually assigned wing/category/upgrade icons where matching IDs exist.
/// </summary>
public static class MuseumUnlockProgressionSetup
{
    private const string MuseumDataFolder = "Assets/Data/Museum";
    private const string MuseumUnlockFolder =
        MuseumDataFolder + "/Unlocks";
    private const string UpgradeFolder =
        "Assets/Data/Upgrades/RareSpecialVault";
    private const string UpgradeUnlockFolder =
        UpgradeFolder + "/Unlocks";

    private static readonly float[] StageCosts =
    {
        250000f,
        500000f,
        1000000f,
        2000000f,
        4000000f,
        8000000f
    };

    private static readonly string[] StageNames =
    {
        "Budget Knives",
        "Budget Gloves",
        "Mid Knives",
        "Premium Knives",
        "Premium Gloves",
        "Icon Knives"
    };

    private static readonly string[] StageDescriptions =
    {
        "Unlocks Navaja Knife, Shadow Daggers, Gut Knife, Falchion Knife, " +
        "Survival Knife and Paracord Knife exhibits.",

        "Unlocks Hand Wraps, Broken Fang Gloves, Bloodhound Gloves and " +
        "Hydra Gloves exhibits.",

        "Unlocks Kukri Knife, Ursus Knife, Flip Knife, Huntsman Knife, " +
        "Nomad Knife, Bowie Knife and Classic Knife exhibits.",

        "Unlocks Bayonet, Talon Knife, Skeleton Knife and Stiletto Knife " +
        "exhibits.",

        "Unlocks Specialist Gloves, Sport Gloves, Moto Gloves and Driver " +
        "Gloves exhibits.",

        "Unlocks Butterfly Knife, Karambit and M9 Bayonet exhibits."
    };

    [MenuItem(
        "Tools/Case Curator/Museum/Apply Museum Unlock Progression")]
    public static void ApplyDefaults()
    {
        GameDatabase database = FindTargetDatabase();

        if (database == null)
            return;

        if (database.museumCatalog == null)
        {
            EditorUtility.DisplayDialog(
                "Museum Catalog Missing",
                "Assign MuseumCatalogConfig on the selected GameDatabase first.",
                "OK");
            return;
        }

        if (database.upgradeCatalog == null)
        {
            EditorUtility.DisplayDialog(
                "Upgrade Catalog Missing",
                "Assign UpgradeCatalog on the selected GameDatabase first.",
                "OK");
            return;
        }

        if (!TryResolveRank(
                "Legendary Eagle Master",
                out PlayerRank rareVaultRank))
        {
            EditorUtility.DisplayDialog(
                "Rank Not Found",
                "PlayerRank does not contain Legendary Eagle Master.",
                "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Apply Museum Unlock Progression",
            "This will rebuild the Arsenal Wing categories, move all Rare " +
            "Special knives/gloves into a separate Rare Special Vault, add the " +
            "Zeus x27 Equipment category and create six sequential Rare Vault " +
            "stage upgrades. Other Museum wings are preserved.",
            "Apply",
            "Cancel");

        if (!confirmed)
            return;

        EnsureFolders();

        UnlockDefinition vaultUnlock = GetOrCreateRankUnlock(
            MuseumUnlockFolder + "/Unlock_RareSpecialVault.asset",
            "museum-rare-special-vault",
            "Rare Special Vault",
            rareVaultRank,
            "Reach Legendary Eagle Master to unlock the Rare Special Vault.");

        UpgradeData previousStage = null;

        for (int stage = 1;
             stage <= MuseumUnlockProgressionUtility.RareVaultStageCount;
             stage++)
        {
            string upgradeId =
                MuseumUnlockProgressionUtility.GetRareVaultStageUpgradeId(stage);

            UnlockDefinition stageUnlock = GetOrCreateStageUnlock(
                stage,
                rareVaultRank,
                previousStage != null ? previousStage.upgradeId : null);

            UpgradeData upgrade = GetOrCreateStageUpgrade(
                stage,
                upgradeId,
                stageUnlock);

            RegisterUpgrade(database.upgradeCatalog, upgrade);
            previousStage = upgrade;
        }

        RebuildMuseumCatalog(database, vaultUnlock);

        database.upgradeCatalog.RebuildLookup();
        EditorUtility.SetDirty(database.upgradeCatalog);
        EditorUtility.SetDirty(database.museumCatalog);
        EditorUtility.SetDirty(database);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = database.museumCatalog;
        EditorGUIUtility.PingObject(database.museumCatalog);

        Debug.Log(
            "Applied Museum unlock progression: rank-gated Arsenal weapons, " +
            "Equipment/Zeus x27 category, separate Rare Special Vault and six " +
            "sequential Rare Vault stage upgrades.",
            database.museumCatalog);
    }

    private static void RebuildMuseumCatalog(
        GameDatabase database,
        UnlockDefinition vaultUnlock)
    {
        MuseumCatalogConfig catalog = database.museumCatalog;
        List<MuseumWingConfig> existing = catalog.wings != null
            ? new List<MuseumWingConfig>(catalog.wings)
            : new List<MuseumWingConfig>();

        MuseumWingConfig oldArsenal = FindWing(
            existing,
            MuseumUnlockProgressionUtility.ArsenalWingId,
            "arsenal");

        MuseumWingConfig oldVault = FindWing(
            existing,
            MuseumUnlockProgressionUtility.RareSpecialVaultWingId,
            "rarespecialvault");

        MuseumWingConfig arsenal = oldArsenal ?? new MuseumWingConfig();
        MuseumWingConfig vault = oldVault ?? new MuseumWingConfig();

        List<MuseumCategoryConfig> oldArsenalCategories =
            arsenal.categories != null
                ? new List<MuseumCategoryConfig>(arsenal.categories)
                : new List<MuseumCategoryConfig>();

        List<MuseumCategoryConfig> oldVaultCategories =
            vault.categories != null
                ? new List<MuseumCategoryConfig>(vault.categories)
                : new List<MuseumCategoryConfig>();

        ConfigureArsenal(arsenal, oldArsenalCategories);
        ConfigureRareVault(
            vault,
            oldVaultCategories,
            database,
            vaultUnlock);

        List<MuseumWingConfig> rebuilt = new List<MuseumWingConfig>
        {
            arsenal,
            vault
        };

        for (int i = 0; i < existing.Count; i++)
        {
            MuseumWingConfig wing = existing[i];

            if (wing == null ||
                ReferenceEquals(wing, oldArsenal) ||
                ReferenceEquals(wing, oldVault))
            {
                continue;
            }

            rebuilt.Add(wing);
        }

        Undo.RecordObject(catalog, "Rebuild Museum unlock catalog");
        catalog.defaultWingId =
            MuseumUnlockProgressionUtility.ArsenalWingId;
        catalog.wings = rebuilt;
    }

    private static void ConfigureArsenal(
        MuseumWingConfig wing,
        List<MuseumCategoryConfig> oldCategories)
    {
        wing.wingId = MuseumUnlockProgressionUtility.ArsenalWingId;
        wing.displayName = "Arsenal Wing";
        wing.description =
            "Donate weapon skins as their Museum exhibits unlock through the " +
            "competitive rank ladder.";
        wing.sortOrder = 0;
        wing.includeInCompletion = true;
        wing.unlockDefinition = null;

        wing.categories = new List<MuseumCategoryConfig>
        {
            Category(
                oldCategories,
                "museum-arsenal-pistols",
                "Pistols",
                "Sidearm exhibits.",
                0,
                "USP-S", "Glock-18", "P2000", "P250", "CZ75-Auto",
                "Dual Berettas", "Five-SeveN", "Tec-9", "Desert Eagle",
                "R8 Revolver"),

            Category(
                oldCategories,
                "museum-arsenal-smgs",
                "SMGs",
                "Submachine-gun exhibits.",
                10,
                "MP9", "MAC-10", "UMP-45", "PP-Bizon", "MP7",
                "MP5-SD", "P90"),

            Category(
                oldCategories,
                "museum-arsenal-machine-guns",
                "Machine Guns",
                "Heavy machine-gun exhibits.",
                20,
                "Negev", "M249"),

            Category(
                oldCategories,
                "museum-arsenal-shotguns",
                "Shotguns",
                "Shotgun exhibits.",
                30,
                "XM1014", "Sawed-Off", "MAG-7", "Nova"),

            Category(
                oldCategories,
                "museum-arsenal-rifles",
                "Rifles",
                "Assault-rifle exhibits.",
                40,
                "Galil AR", "FAMAS", "SG 553", "AUG", "M4A4",
                "M4A1-S", "AK-47"),

            Category(
                oldCategories,
                "museum-arsenal-snipers",
                "Snipers",
                "Sniper-rifle exhibits.",
                50,
                "G3SG1", "SCAR-20", "SSG 08", "AWP"),

            Category(
                oldCategories,
                "museum-arsenal-equipment",
                "Equipment",
                "Special equipment exhibits.",
                60,
                "Zeus x27")
        };
    }

    private static void ConfigureRareVault(
        MuseumWingConfig wing,
        List<MuseumCategoryConfig> oldCategories,
        GameDatabase database,
        UnlockDefinition vaultUnlock)
    {
        wing.wingId =
            MuseumUnlockProgressionUtility.RareSpecialVaultWingId;
        wing.displayName = "Rare Special Vault";
        wing.description =
            "Secure knife and glove exhibits. Each family requires its matching " +
            "Rare Vault Stage upgrade.";
        wing.sortOrder = 10;
        wing.includeInCompletion = true;
        wing.unlockDefinition = vaultUnlock;

        CollectRareSpecialWeaponNames(
            database,
            out List<string> knives,
            out List<string> gloves);

        wing.categories = new List<MuseumCategoryConfig>
        {
            Category(
                oldCategories,
                "museum-rare-vault-knives",
                "Knives",
                "Rare Special knife families.",
                0,
                knives),

            Category(
                oldCategories,
                "museum-rare-vault-gloves",
                "Gloves",
                "Rare Special glove families.",
                10,
                gloves)
        };
    }

    private static MuseumCategoryConfig Category(
        List<MuseumCategoryConfig> oldCategories,
        string id,
        string displayName,
        string description,
        int sortOrder,
        params string[] weaponNames)
    {
        return Category(
            oldCategories,
            id,
            displayName,
            description,
            sortOrder,
            weaponNames != null
                ? new List<string>(weaponNames)
                : new List<string>());
    }

    private static MuseumCategoryConfig Category(
        List<MuseumCategoryConfig> oldCategories,
        string id,
        string displayName,
        string description,
        int sortOrder,
        List<string> weaponNames)
    {
        MuseumCategoryConfig category = FindCategory(
            oldCategories,
            id,
            displayName) ?? new MuseumCategoryConfig();

        category.categoryId = id;
        category.displayName = displayName;
        category.description = description;
        category.sortOrder = sortOrder;
        category.includeInCompletion = true;
        category.unlockDefinition = null;
        category.filter = new MuseumCatalogFilter
        {
            filterMode = MuseumCatalogFilterMode.ListedWeapons,
            weaponNames = weaponNames ?? new List<string>(),
            includeNormal = true,
            includeStatTrak = true,
            includeSouvenir = true,
            includeVanilla = true
        };

        return category;
    }

    private static void CollectRareSpecialWeaponNames(
        GameDatabase database,
        out List<string> knives,
        out List<string> gloves)
    {
        HashSet<string> knifeSet =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> gloveSet =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (database != null && database.allSkins != null)
        {
            for (int i = 0; i < database.allSkins.Count; i++)
            {
                SkinData skin = database.allSkins[i];

                if (skin == null ||
                    skin.rarity != Rarity.RareSpecial ||
                    string.IsNullOrWhiteSpace(skin.weaponName))
                {
                    continue;
                }

                string weaponName = skin.weaponName.Trim();

                if (IsGloveFamily(weaponName))
                    gloveSet.Add(weaponName);
                else
                    knifeSet.Add(weaponName);
            }
        }

        knives = new List<string>(knifeSet);
        gloves = new List<string>(gloveSet);
        knives.Sort(StringComparer.OrdinalIgnoreCase);
        gloves.Sort(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsGloveFamily(string weaponName)
    {
        string value = Normalize(weaponName);

        return value.Contains("glove") ||
               value.Contains("handwrap") ||
               value.Contains("bloodhound") ||
               value.Contains("hydra") ||
               value.Contains("brokenfang");
    }

    private static UnlockDefinition GetOrCreateRankUnlock(
        string path,
        string unlockId,
        string displayName,
        PlayerRank minimumRank,
        string lockedReason)
    {
        UnlockDefinition definition =
            AssetDatabase.LoadAssetAtPath<UnlockDefinition>(path);

        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<UnlockDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        Undo.RecordObject(definition, "Update Museum rank unlock");
        definition.unlockId = unlockId;
        definition.featureId = FeatureId.Custom;
        definition.displayName = displayName;
        definition.requirementMode = UnlockRequirementGroupMode.All;
        definition.noRequirementsMeansUnlocked = false;
        definition.requirements = new List<UnlockRequirement>
        {
            new UnlockRequirement
            {
                requirementType = UnlockRequirementType.PlayerRankAtLeast,
                minimumRank = minimumRank,
                lockedReasonOverride = lockedReason
            }
        };

        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static UnlockDefinition GetOrCreateStageUnlock(
        int stage,
        PlayerRank minimumRank,
        string previousUpgradeId)
    {
        string path = UpgradeUnlockFolder +
                      $"/Unlock_RareVaultStage_{stage:00}.asset";

        UnlockDefinition definition =
            AssetDatabase.LoadAssetAtPath<UnlockDefinition>(path);

        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<UnlockDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        Undo.RecordObject(definition, "Update Rare Vault stage unlock");
        definition.unlockId = $"museum-rare-vault-stage-{stage:00}";
        definition.featureId = FeatureId.Custom;
        definition.displayName = $"Rare Vault Stage {stage}";
        definition.requirementMode = UnlockRequirementGroupMode.All;
        definition.noRequirementsMeansUnlocked = false;
        definition.requirements = new List<UnlockRequirement>
        {
            new UnlockRequirement
            {
                requirementType = UnlockRequirementType.PlayerRankAtLeast,
                minimumRank = minimumRank,
                lockedReasonOverride =
                    "Reach Legendary Eagle Master to access Rare Vault upgrades."
            }
        };

        if (!string.IsNullOrWhiteSpace(previousUpgradeId))
        {
            definition.requirements.Add(new UnlockRequirement
            {
                requirementType =
                    UnlockRequirementType.UpgradeLevelAtLeast,
                requiredUpgradeId = previousUpgradeId,
                minimumUpgradeLevel = 1,
                lockedReasonOverride =
                    "Purchase the previous Rare Vault Stage first."
            });
        }

        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static UpgradeData GetOrCreateStageUpgrade(
        int stage,
        string upgradeId,
        UnlockDefinition unlock)
    {
        string path = UpgradeFolder +
                      $"/Upgrade_RareVaultStage_{stage:00}.asset";

        UpgradeData upgrade =
            AssetDatabase.LoadAssetAtPath<UpgradeData>(path);

        if (upgrade == null)
        {
            upgrade = ScriptableObject.CreateInstance<UpgradeData>();
            AssetDatabase.CreateAsset(upgrade, path);
        }

        Sprite existingIcon = upgrade.icon;
        string displayName = $"Rare Vault Stage {stage}: {StageNames[stage - 1]}";

        Undo.RecordObject(upgrade, "Update Rare Vault stage upgrade");
        upgrade.upgradeId = upgradeId;
        upgrade.displayName = displayName;
        upgrade.description = StageDescriptions[stage - 1];
        upgrade.icon = existingIcon;
        upgrade.category = UpgradeCategory.Museum;
        upgrade.sortOrder = 300 + stage;
        upgrade.hiddenUntilUnlocked = false;
        upgrade.unlockDefinition = unlock;
        upgrade.effectType = UpgradeEffectType.GenericValue;
        upgrade.defaultEffectValue = 0f;
        upgrade.levels = new List<UpgradeLevelData>
        {
            new UpgradeLevelData
            {
                levelName = displayName,
                description = StageDescriptions[stage - 1],
                currency = UpgradeCurrency.Gold,
                cost = StageCosts[stage - 1],
                effectValue = stage
            }
        };

        EditorUtility.SetDirty(upgrade);
        return upgrade;
    }

    private static void RegisterUpgrade(
        UpgradeCatalog catalog,
        UpgradeData upgrade)
    {
        SerializedObject serialized = new SerializedObject(catalog);
        SerializedProperty upgrades = serialized.FindProperty("upgrades");

        if (upgrades == null)
            return;

        for (int i = 0; i < upgrades.arraySize; i++)
        {
            SerializedProperty element = upgrades.GetArrayElementAtIndex(i);
            UpgradeData existing = element.objectReferenceValue as UpgradeData;

            if (existing == upgrade)
                return;

            if (existing != null &&
                string.Equals(
                    existing.upgradeId,
                    upgrade.upgradeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                element.objectReferenceValue = upgrade;
                serialized.ApplyModifiedProperties();
                return;
            }
        }

        int index = upgrades.arraySize;
        upgrades.InsertArrayElementAtIndex(index);
        upgrades.GetArrayElementAtIndex(index).objectReferenceValue = upgrade;
        serialized.ApplyModifiedProperties();
    }

    private static MuseumWingConfig FindWing(
        List<MuseumWingConfig> wings,
        string exactId,
        string normalizedNameTerm)
    {
        if (wings == null)
            return null;

        string normalizedId = Normalize(exactId);

        for (int i = 0; i < wings.Count; i++)
        {
            MuseumWingConfig wing = wings[i];

            if (wing == null)
                continue;

            if (Normalize(wing.wingId) == normalizedId)
                return wing;
        }

        for (int i = 0; i < wings.Count; i++)
        {
            MuseumWingConfig wing = wings[i];

            if (wing != null &&
                Normalize(wing.DisplayName).Contains(normalizedNameTerm))
            {
                return wing;
            }
        }

        return null;
    }

    private static MuseumCategoryConfig FindCategory(
        List<MuseumCategoryConfig> categories,
        string exactId,
        string displayName)
    {
        if (categories == null)
            return null;

        string normalizedId = Normalize(exactId);
        string normalizedName = Normalize(displayName);

        for (int i = 0; i < categories.Count; i++)
        {
            MuseumCategoryConfig category = categories[i];

            if (category == null)
                continue;

            if (Normalize(category.categoryId) == normalizedId ||
                Normalize(category.DisplayName) == normalizedName)
            {
                return category;
            }
        }

        return null;
    }

    private static bool TryResolveRank(
        string displayName,
        out PlayerRank rank)
    {
        string target = Normalize(displayName);
        Array values = Enum.GetValues(typeof(PlayerRank));

        foreach (object value in values)
        {
            PlayerRank candidate = (PlayerRank)value;

            if (Normalize(candidate.ToString()) == target ||
                Normalize(
                    PlayerProgressUtility.GetRankDisplayName(candidate)) == target)
            {
                rank = candidate;
                return true;
            }
        }

        rank = default(PlayerRank);
        return false;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        char[] buffer = new char[value.Length];
        int length = 0;

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];

            if (!char.IsLetterOrDigit(character))
                continue;

            buffer[length++] = char.ToLowerInvariant(character);
        }

        return new string(buffer, 0, length);
    }

    private static GameDatabase FindTargetDatabase()
    {
        if (Selection.activeObject is GameDatabase selected)
            return selected;

        string[] guids = AssetDatabase.FindAssets("t:GameDatabase");

        if (guids == null || guids.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "GameDatabase Not Found",
                "Create or select a GameDatabase asset, then run this command again.",
                "OK");
            return null;
        }

        if (guids.Length > 1)
        {
            EditorUtility.DisplayDialog(
                "Select GameDatabase",
                "More than one GameDatabase exists. Select the intended asset " +
                "in the Project window, then run this command again.",
                "OK");
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<GameDatabase>(path);
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Data");
        EnsureFolder("Assets/Data", "Museum");
        EnsureFolder(MuseumDataFolder, "Unlocks");
        EnsureFolder("Assets/Data", "Upgrades");
        EnsureFolder("Assets/Data/Upgrades", "RareSpecialVault");
        EnsureFolder(UpgradeFolder, "Unlocks");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
