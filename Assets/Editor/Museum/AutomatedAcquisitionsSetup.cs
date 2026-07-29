#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the Automated Acquisitions catalogue, combined wing unlock and all
/// seven data-driven upgrade branches. Existing research order numbers are
/// preserved when this command is rerun.
/// </summary>
public static class AutomatedAcquisitionsSetup
{
    private const string DataFolder =
        "Assets/Data/Museum/AutomatedAcquisitions";
    private const string UpgradeFolder =
        DataFolder + "/Upgrades";
    private const string CatalogPath =
        DataFolder + "/AutoAcquisitionCatalog.asset";
    private const string UnlockPath =
        DataFolder + "/Unlock_AutomatedAcquisitions.asset";

    [MenuItem(
        "Tools/Case Curator/Museum/Apply Automated Acquisitions Wing")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Exit Play Mode",
                "Apply Automated Acquisitions outside Play Mode.",
                "OK");
            return;
        }

        GameDatabase database = FindTargetDatabase();

        if (database == null)
            return;

        if (database.upgradeCatalog == null)
        {
            EditorUtility.DisplayDialog(
                "Upgrade Catalog Missing",
                "Assign UpgradeCatalog on the selected GameDatabase first.",
                "OK");
            return;
        }

        MuseumMilestoneData requiredStep = FindStep(database, 5);

        if (requiredStep == null || string.IsNullOrWhiteSpace(requiredStep.milestoneId))
        {
            EditorUtility.DisplayDialog(
                "Museum Step 5 Missing",
                "Generate the 80-step Museum Staircase before applying this wing.",
                "OK");
            return;
        }

        if (!TryResolveRank("Global Elite V", out PlayerRank requiredRank))
        {
            EditorUtility.DisplayDialog(
                "Rank Missing",
                "PlayerRank does not contain Global Elite V.",
                "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Apply Automated Acquisitions Wing",
            "Create or update the Automated Acquisitions catalogue, preserve " +
            "existing research order, generate seven upgrade branches and " +
            "register them in the UpgradeCatalog?",
            "Apply",
            "Cancel");

        if (!confirmed)
            return;

        EnsureFolders();

        UnlockDefinition wingUnlock = GetOrCreateWingUnlock(
            requiredRank,
            requiredStep.milestoneId);

        AutoAcquisitionCatalogData catalog = GetOrCreateCatalog();
        RebuildCatalog(catalog, database);
        database.autoAcquisitionCatalog = catalog;

        UpgradeData[] upgrades =
        {
            CreateProcessingSpeed(wingUnlock),
            CreateCalibration(wingUnlock),
            CreateIntakeCapacity(wingUnlock),
            CreateProcessingLines(wingUnlock),
            CreateProcurementBudget(wingUnlock),
            CreateOfflineShift(wingUnlock),
            CreateCuratorAlerts(wingUnlock)
        };

        for (int i = 0; i < upgrades.Length; i++)
            RegisterUpgrade(database.upgradeCatalog, upgrades[i]);

        database.upgradeCatalog.RebuildLookup();
        EditorUtility.SetDirty(catalog);
        EditorUtility.SetDirty(database.upgradeCatalog);
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = catalog;
        EditorGUIUtility.PingObject(catalog);

        Debug.Log(
            $"Applied Automated Acquisitions Wing. Categories: " +
            $"{catalog.categories.Count}, research rows: {catalog.containers.Count}, " +
            $"upgrade branches: {upgrades.Length}.",
            catalog);
    }

    private static AutoAcquisitionCatalogData GetOrCreateCatalog()
    {
        AutoAcquisitionCatalogData catalog =
            AssetDatabase.LoadAssetAtPath<AutoAcquisitionCatalogData>(CatalogPath);

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<AutoAcquisitionCatalogData>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        return catalog;
    }

    private static void RebuildCatalog(
        AutoAcquisitionCatalogData catalog,
        GameDatabase database)
    {
        Undo.RecordObject(catalog, "Update Automated Acquisitions catalogue");

        catalog.wingDisplayName = "Automated Acquisitions Wing";
        catalog.wingDescription =
            "Research Bronze-completed containers, fund timed processing lines " +
            "and claim generated items from the Uncatalogued Intake Vault.";
        catalog.categories = new List<AutoAcquisitionCategoryData>
        {
            new AutoAcquisitionCategoryData
            {
                categoryId = AutoAcquisitionCatalogData.WeaponCaseCategoryId,
                displayName = "Weapon Case Archive",
                description =
                    "Sequential automated research for Weapon Cases after Bronze Completion.",
                sortOrder = 0,
                licenseCost = 10000f,
                minimumResearchCost = 500f,
                researchCostMultiplier = 20f,
                supportedContainerTypes = new List<CaseContainerType>
                {
                    CaseContainerType.WeaponCase
                }
            },
            new AutoAcquisitionCategoryData
            {
                categoryId =
                    AutoAcquisitionCatalogData.CollectionPackageCategoryId,
                displayName = "Collection Package Archive",
                description =
                    "Sequential automated research for standard Collection Packages " +
                    "after Bronze Completion.",
                sortOrder = 10,
                licenseCost = 100000f,
                minimumResearchCost = 2500f,
                researchCostMultiplier = 28f,
                supportedContainerTypes = new List<CaseContainerType>
                {
                    CaseContainerType.CollectionPackage
                }
            }
        };

        Dictionary<string, AutoAcquisitionContainerData> existing =
            new Dictionary<string, AutoAcquisitionContainerData>(
                StringComparer.OrdinalIgnoreCase);
        int maximumOrder = -1;

        if (catalog.containers != null)
        {
            for (int i = 0; i < catalog.containers.Count; i++)
            {
                AutoAcquisitionContainerData row = catalog.containers[i];

                if (row == null || string.IsNullOrWhiteSpace(row.containerId))
                    continue;

                existing[row.containerId] = row;
                maximumOrder = Mathf.Max(maximumOrder, row.automatedUnlockOrder);
            }
        }

        List<CaseData> eligible = new List<CaseData>();

        if (database.allCases != null)
        {
            for (int i = 0; i < database.allCases.Count; i++)
            {
                CaseData container = database.allCases[i];

                if (container == null ||
                    (container.containerType != CaseContainerType.WeaponCase &&
                     container.containerType != CaseContainerType.CollectionPackage))
                {
                    continue;
                }

                eligible.Add(container);
            }
        }

        eligible.Sort((a, b) =>
        {
            int type = a.containerType.CompareTo(b.containerType);

            if (type != 0)
                return type;

            int price = a.priceInGold.CompareTo(b.priceInGold);

            return price != 0
                ? price
                : string.Compare(
                    a.caseName,
                    b.caseName,
                    StringComparison.OrdinalIgnoreCase);
        });

        // New rows are appended within each category, while every existing ID
        // keeps its original permanent order number.
        Dictionary<string, int> nextOrderByCategory =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < catalog.categories.Count; i++)
        {
            AutoAcquisitionCategoryData category = catalog.categories[i];
            int categoryMaximum = -1;

            foreach (AutoAcquisitionContainerData row in existing.Values)
            {
                if (row != null &&
                    string.Equals(
                        row.categoryId,
                        category.categoryId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    categoryMaximum = Mathf.Max(
                        categoryMaximum,
                        row.automatedUnlockOrder);
                }
            }

            nextOrderByCategory[category.categoryId] = categoryMaximum + 1;
        }

        List<AutoAcquisitionContainerData> rebuilt =
            new List<AutoAcquisitionContainerData>();

        for (int i = 0; i < eligible.Count; i++)
        {
            CaseData container = eligible[i];
            string id = GetContainerId(container);
            string categoryId = container.containerType == CaseContainerType.WeaponCase
                ? AutoAcquisitionCatalogData.WeaponCaseCategoryId
                : AutoAcquisitionCatalogData.CollectionPackageCategoryId;
            AutoAcquisitionCategoryData category = catalog.GetCategory(categoryId);

            existing.TryGetValue(id, out AutoAcquisitionContainerData row);

            if (row == null)
            {
                row = new AutoAcquisitionContainerData
                {
                    containerId = id,
                    categoryId = categoryId,
                    automatedUnlockOrder = nextOrderByCategory[categoryId]++
                };
            }

            row.containerId = id;
            row.container = container;
            row.categoryId = categoryId;
            row.processingDurationMultiplier = Mathf.Max(
                0.01f,
                row.processingDurationMultiplier <= 0f
                    ? 1f
                    : row.processingDurationMultiplier);
            row.permanentResearchCost = CalculateResearchCost(
                container,
                category);
            rebuilt.Add(row);
        }

        rebuilt.Sort((a, b) =>
        {
            int category = string.Compare(
                a.categoryId,
                b.categoryId,
                StringComparison.OrdinalIgnoreCase);

            if (category != 0)
                return category;

            return a.automatedUnlockOrder.CompareTo(b.automatedUnlockOrder);
        });

        catalog.containers = rebuilt;
        catalog.maximumOfflineOpeningsPerLine = 10000;
        catalog.maximumCalibrationAttempts = 32;
        catalog.runtimeTickSeconds = 1f;
        catalog.exceptionalValueThreshold = 1000f;
        catalog.pristineFloatThreshold = 0.001f;
        catalog.extremeHighFloatThreshold = 0.999f;
    }

    private static float CalculateResearchCost(
        CaseData container,
        AutoAcquisitionCategoryData category)
    {
        if (container == null || category == null)
            return 0f;

        double raw = Math.Max(
            category.minimumResearchCost,
            Math.Max(0f, container.priceInGold) *
            Math.Max(0f, category.researchCostMultiplier));

        return (float)(Math.Ceiling(raw / 100d) * 100d);
    }

    private static UnlockDefinition GetOrCreateWingUnlock(
        PlayerRank requiredRank,
        string milestoneId)
    {
        UnlockDefinition definition =
            AssetDatabase.LoadAssetAtPath<UnlockDefinition>(UnlockPath);

        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<UnlockDefinition>();
            AssetDatabase.CreateAsset(definition, UnlockPath);
        }

        Undo.RecordObject(definition, "Update Automated Acquisitions unlock");
        definition.unlockId = "automated-acquisitions-wing";
        definition.featureId = FeatureId.AutomatedAcquisitionsWing;
        definition.displayName = "Automated Acquisitions Wing";
        definition.requirementMode = UnlockRequirementGroupMode.All;
        definition.noRequirementsMeansUnlocked = false;
        definition.requirements = new List<UnlockRequirement>
        {
            new UnlockRequirement
            {
                requirementType = UnlockRequirementType.PlayerRankAtLeast,
                minimumRank = requiredRank,
                lockedReasonOverride = "Reach Global Elite V."
            },
            new UnlockRequirement
            {
                requirementType = UnlockRequirementType.MuseumMilestoneClaimed,
                requiredMilestoneId = milestoneId,
                lockedReasonOverride = "Claim Museum Staircase step 5."
            }
        };

        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static UpgradeData CreateProcessingSpeed(UnlockDefinition unlock)
    {
        return CreateUpgrade(
            "Upgrade_ProcessingSpeed.asset",
            AutoAcquisitionUpgradeUtility.ProcessingSpeedId,
            "Processing Speed",
            "Reduces processing time for every Automated Acquisition item.",
            400,
            600f,
            unlock,
            new[] { 25000f, 60000f, 150000f, 300000f, 500000f, 1000000f },
            new[] { 500f, 400f, 300f, 180f, 90f, 60f },
            "seconds per item");
    }

    private static UpgradeData CreateCalibration(UnlockDefinition unlock)
    {
        return CreateUpgrade(
            "Upgrade_MachineCalibration.asset",
            AutoAcquisitionUpgradeUtility.CalibrationId,
            "Machine Calibration",
            "Raises automated rarity calibration from 0.80 to the manual 1.00 ceiling.",
            410,
            0.80f,
            unlock,
            new[] { 100000f, 200000f, 400000f, 800000f },
            new[] { 0.85f, 0.90f, 0.95f, 1.00f },
            "manual rarity odds");
    }

    private static UpgradeData CreateIntakeCapacity(UnlockDefinition unlock)
    {
        return CreateUpgrade(
            "Upgrade_IntakeVault.asset",
            AutoAcquisitionUpgradeUtility.IntakeCapacityId,
            "Intake Vault",
            "Raises the number of unclaimed generated items the Intake Vault can hold.",
            420,
            10f,
            unlock,
            new[] { 50000f, 150000f, 400000f, 1000000f },
            new[] { 25f, 50f, 100f, 250f },
            "item capacity");
    }

    private static UpgradeData CreateProcessingLines(UnlockDefinition unlock)
    {
        return CreateUpgrade(
            "Upgrade_ProcessingLines.asset",
            AutoAcquisitionUpgradeUtility.ProcessingLinesId,
            "Processing Lines",
            "Adds concurrent Automated Acquisition processing targets.",
            430,
            1f,
            unlock,
            new[] { 500000f, 2000000f },
            new[] { 2f, 3f },
            "active lines");
    }

    private static UpgradeData CreateProcurementBudget(UnlockDefinition unlock)
    {
        return CreateUpgrade(
            "Upgrade_ProcurementBudget.asset",
            AutoAcquisitionUpgradeUtility.ProcurementBudgetId,
            "Procurement Budget",
            "Raises the maximum Gold deposit held by each processing line.",
            440,
            5000f,
            unlock,
            new[] { 50000f, 150000f, 600000f, 1500000f },
            new[] { 25000f, 100000f, 500000f, 2000000f },
            "Gold per line");
    }

    private static UpgradeData CreateOfflineShift(UnlockDefinition unlock)
    {
        return CreateUpgrade(
            "Upgrade_AcquisitionsShift.asset",
            AutoAcquisitionUpgradeUtility.OfflineShiftId,
            "Acquisitions Shift",
            "Extends the controlled offline-processing window.",
            450,
            1f,
            unlock,
            new[] { 100000f, 300000f, 1000000f, 2500000f },
            new[] { 2f, 4f, 8f, 12f },
            "offline hours");
    }

    private static UpgradeData CreateCuratorAlerts(UnlockDefinition unlock)
    {
        return CreateUpgrade(
            "Upgrade_CuratorAlerts.asset",
            AutoAcquisitionUpgradeUtility.CuratorAlertId,
            "Curator Alerts",
            "Flags or pauses processing for exceptional outputs.",
            460,
            0f,
            unlock,
            new[] { 100000f, 250000f, 600000f, 1500000f },
            new[] { 1f, 2f, 3f, 4f },
            "alert level");
    }

    private static UpgradeData CreateUpgrade(
        string fileName,
        string upgradeId,
        string displayName,
        string description,
        int sortOrder,
        float defaultEffect,
        UnlockDefinition unlock,
        float[] costs,
        float[] effects,
        string effectLabel)
    {
        string path = UpgradeFolder + "/" + fileName;
        UpgradeData upgrade = AssetDatabase.LoadAssetAtPath<UpgradeData>(path);

        if (upgrade == null)
        {
            upgrade = ScriptableObject.CreateInstance<UpgradeData>();
            AssetDatabase.CreateAsset(upgrade, path);
        }

        Sprite preservedIcon = upgrade.icon;
        Undo.RecordObject(upgrade, "Update Automated Acquisitions upgrade");
        upgrade.upgradeId = upgradeId;
        upgrade.displayName = displayName;
        upgrade.description = description;
        upgrade.icon = preservedIcon;
        upgrade.category = UpgradeCategory.AutomatedAcquisitions;
        upgrade.sortOrder = sortOrder;
        upgrade.hiddenUntilUnlocked = false;
        upgrade.unlockDefinition = unlock;
        upgrade.effectType = UpgradeEffectType.GenericValue;
        upgrade.defaultEffectValue = defaultEffect;
        upgrade.levels = new List<UpgradeLevelData>();

        int count = Mathf.Min(
            costs != null ? costs.Length : 0,
            effects != null ? effects.Length : 0);

        for (int i = 0; i < count; i++)
        {
            upgrade.levels.Add(new UpgradeLevelData
            {
                levelName = $"{displayName} {ToRoman(i + 1)}",
                description = $"{effects[i]:0.##} {effectLabel}.",
                currency = UpgradeCurrency.Gold,
                cost = costs[i],
                effectValue = effects[i]
            });
        }

        EditorUtility.SetDirty(upgrade);
        return upgrade;
    }

    private static void RegisterUpgrade(
        UpgradeCatalog catalog,
        UpgradeData upgrade)
    {
        if (catalog == null || upgrade == null)
            return;

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

    private static MuseumMilestoneData FindStep(
        GameDatabase database,
        int stairNumber)
    {
        if (database == null || database.museumMilestones == null)
            return null;

        for (int i = 0; i < database.museumMilestones.Count; i++)
        {
            MuseumMilestoneData milestone = database.museumMilestones[i];

            if (milestone != null && milestone.stairNumber == stairNumber)
                return milestone;
        }

        return null;
    }

    private static string GetContainerId(CaseData container)
    {
        if (container == null)
            return "";

        return !string.IsNullOrWhiteSpace(container.apiId)
            ? container.apiId.Trim()
            : "name:" + Slug(container.caseName);
    }

    private static string Slug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "container";

        char[] buffer = new char[value.Length];
        int length = 0;

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];

            if (!char.IsLetterOrDigit(c))
                continue;

            buffer[length++] = char.ToLowerInvariant(c);
        }

        return length > 0 ? new string(buffer, 0, length) : "container";
    }

    private static bool TryResolveRank(
        string displayName,
        out PlayerRank rank)
    {
        string target = Slug(displayName);
        Array values = Enum.GetValues(typeof(PlayerRank));

        foreach (object value in values)
        {
            PlayerRank candidate = (PlayerRank)value;

            if (Slug(candidate.ToString()) == target ||
                Slug(PlayerProgressUtility.GetRankDisplayName(candidate)) == target)
            {
                rank = candidate;
                return true;
            }
        }

        rank = default;
        return false;
    }

    private static string ToRoman(int value)
    {
        switch (value)
        {
            case 1: return "I";
            case 2: return "II";
            case 3: return "III";
            case 4: return "IV";
            case 5: return "V";
            case 6: return "VI";
            default: return value.ToString();
        }
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
                "More than one GameDatabase exists. Select the intended asset first.",
                "OK");
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<GameDatabase>(
            AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Data");
        EnsureFolder("Assets/Data", "Museum");
        EnsureFolder("Assets/Data/Museum", "AutomatedAcquisitions");
        EnsureFolder(DataFolder, "Upgrades");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
