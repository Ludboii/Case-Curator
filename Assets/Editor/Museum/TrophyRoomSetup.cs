#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the M7 Trophy Room balance asset, eleven rank-gated pedestal upgrades
/// and their sequential unlock definitions. Re-running is safe and preserves icons.
/// </summary>
public static class TrophyRoomSetup
{
    private const string MuseumFolder = "Assets/Data/Museum";
    private const string TrophyFolder = MuseumFolder + "/TrophyRoom";
    private const string UpgradeFolder = "Assets/Data/Upgrades/TrophyRoom";
    private const string UnlockFolder = UpgradeFolder + "/Unlocks";

    private static readonly float[] PedestalCosts =
    {
        50000f,
        100000f,
        200000f,
        400000f,
        750000f,
        1250000f,
        2000000f,
        3000000f,
        5000000f,
        8000000f,
        12000000f
    };

    [MenuItem("Tools/Case Curator/Museum/Apply M7 Trophy Room Foundation")]
    public static void ApplyDefaults()
    {
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

        PlayerRank[] pedestalRanks = ResolvePedestalRanks();

        if (pedestalRanks == null || pedestalRanks.Length != 11)
        {
            EditorUtility.DisplayDialog(
                "Trophy Rank Setup Failed",
                "The project does not contain enough PlayerRank values to map " +
                "the eleven Trophy Room pedestals.",
                "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Apply M7 Trophy Room Foundation",
            "This creates or updates the Trophy Room balance asset and eleven " +
            "rank-gated pedestal upgrades. Existing purchased upgrade levels are " +
            "preserved by stable upgrade ID.",
            "Apply",
            "Cancel");

        if (!confirmed)
            return;

        EnsureFolder("Assets", "Data");
        EnsureFolder("Assets/Data", "Museum");
        EnsureFolder(MuseumFolder, "TrophyRoom");
        EnsureFolder("Assets/Data", "Upgrades");
        EnsureFolder("Assets/Data/Upgrades", "TrophyRoom");
        EnsureFolder(UpgradeFolder, "Unlocks");

        TrophyRoomBalanceData balance = GetOrCreateBalance();
        Undo.RecordObject(database, "Assign Trophy Room balance");
        database.trophyRoomBalance = balance;
        EditorUtility.SetDirty(database);

        UpgradeData previous = null;

        for (int slot = 0; slot < 11; slot++)
        {
            string numeral = ToRoman(slot + 1);
            string upgradeId =
                TrophyRoomUpgradeUtility.GetPedestalUpgradeId(slot);
            string rankName =
                PlayerProgressUtility.GetRankDisplayName(pedestalRanks[slot]);

            UnlockDefinition unlock = GetOrCreateUnlock(
                $"{UnlockFolder}/Unlock_TrophyPedestal_{slot + 1:00}.asset",
                $"unlock-trophy-pedestal-{slot + 1:00}",
                $"Trophy Pedestal {slot + 1}",
                pedestalRanks[slot],
                rankName,
                previous != null ? previous.upgradeId : null);

            UpgradeData upgrade = GetOrCreateUpgrade(
                $"{UpgradeFolder}/Upgrade_TrophyPedestal_{slot + 1:00}.asset",
                upgradeId,
                $"Trophy Pedestal {numeral}",
                $"Unlocks Trophy Room Pedestal {slot + 1}. Requires {rankName}.",
                200 + slot,
                unlock,
                slot < PedestalCosts.Length ? PedestalCosts[slot] : 0f,
                slot + 1);

            RegisterUpgrade(database.upgradeCatalog, upgrade);
            previous = upgrade;
        }

        database.upgradeCatalog.RebuildLookup();
        EditorUtility.SetDirty(database.upgradeCatalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = balance;
        EditorGUIUtility.PingObject(balance);

        Debug.Log(
            "Applied M7 Trophy Room foundation: 11 sequential pedestal upgrades, " +
            "15/35/15/35 Trophy Power weighting, 1.0/1.2/1.5 pedestal " +
            "multipliers and four global focus curves.",
            balance);
    }

    private static TrophyRoomBalanceData GetOrCreateBalance()
    {
        string path = TrophyFolder + "/TrophyRoomBalance.asset";
        TrophyRoomBalanceData balance =
            AssetDatabase.LoadAssetAtPath<TrophyRoomBalanceData>(path);

        if (balance == null)
        {
            balance = ScriptableObject.CreateInstance<TrophyRoomBalanceData>();
            AssetDatabase.CreateAsset(balance, path);
        }

        Undo.RecordObject(balance, "Update Trophy Room balance");

        balance.rarityWeight = 15f;
        balance.marketValueWeight = 35f;
        balance.variantWeight = 15f;
        balance.floatWeight = 35f;
        balance.marketValueAtFullScore = 10000f;

        balance.normalVariantScore = 0f;
        balance.souvenirVariantScore = 0.8f;
        balance.statTrakVariantScore = 1f;

        balance.floorGapWeight = 0.70f;
        balance.rangePositionWeight = 0.20f;
        balance.absoluteFloatWeight = 0.10f;
        balance.highFloatStrength = 0.70f;

        balance.floorGapCurve = Curve(
            (0.00001f, 1.00f),
            (0.00010f, 0.98f),
            (0.00100f, 0.94f),
            (0.00500f, 0.90f),
            (0.01000f, 0.86f),
            (0.03000f, 0.80f),
            (0.05000f, 0.74f),
            (0.10000f, 0.55f),
            (0.20000f, 0.25f),
            (0.30000f, 0.00f));

        balance.ceilingGapCurve = new AnimationCurve(
            balance.floorGapCurve.keys);

        balance.lowRangePositionCurve = Curve(
            (0.000f, 1.00f),
            (0.001f, 1.00f),
            (0.010f, 0.98f),
            (0.050f, 0.90f),
            (0.100f, 0.80f),
            (0.250f, 0.50f),
            (0.500f, 0.00f));

        balance.highRangePositionCurve = new AnimationCurve(
            balance.lowRangePositionCurve.keys);

        balance.absoluteLowFloatCurve = Curve(
            (0.00001f, 1.00f),
            (0.00010f, 0.98f),
            (0.00100f, 0.90f),
            (0.01000f, 0.65f),
            (0.03000f, 0.35f),
            (0.06000f, 0.10f),
            (0.10000f, 0.00f));

        balance.absoluteHighFloatCurve = Curve(
            (0.700f, 0.00f),
            (0.850f, 0.15f),
            (0.930f, 0.35f),
            (0.970f, 0.60f),
            (0.990f, 0.85f),
            (0.999f, 1.00f));

        balance.slotsOneToFiveMultiplier = 1f;
        balance.slotsSixToTenMultiplier = 1.2f;
        balance.slotElevenMultiplier = 1.5f;

        SetFocus(balance.museumGoldIncome, 0.25f, 500f);
        SetFocus(balance.museumDiamondIncome, 0.25f, 500f);
        SetFocus(balance.automatedAcquisitions, 0.25f, 500f);
        SetFocus(balance.giftRetrievals, 0.25f, 500f);

        EditorUtility.SetDirty(balance);
        return balance;
    }

    private static void SetFocus(
        TrophyFocusBalance focus,
        float maximum,
        float halfPower)
    {
        if (focus == null)
            return;

        focus.maximumBonusFraction = maximum;
        focus.halfPowerValue = halfPower;
    }

    private static UnlockDefinition GetOrCreateUnlock(
        string path,
        string unlockId,
        string displayName,
        PlayerRank minimumRank,
        string rankName,
        string previousUpgradeId)
    {
        UnlockDefinition definition =
            AssetDatabase.LoadAssetAtPath<UnlockDefinition>(path);

        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<UnlockDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        Undo.RecordObject(definition, "Update Trophy pedestal unlock");
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
                lockedReasonOverride = $"Requires {rankName}."
            }
        };

        if (!string.IsNullOrWhiteSpace(previousUpgradeId))
        {
            definition.requirements.Add(new UnlockRequirement
            {
                requirementType = UnlockRequirementType.UpgradeLevelAtLeast,
                requiredUpgradeId = previousUpgradeId,
                minimumUpgradeLevel = 1,
                lockedReasonOverride =
                    "Unlock the previous Trophy pedestal first."
            });
        }

        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static UpgradeData GetOrCreateUpgrade(
        string path,
        string upgradeId,
        string displayName,
        string description,
        int sortOrder,
        UnlockDefinition unlock,
        float cost,
        float effectValue)
    {
        UpgradeData upgrade =
            AssetDatabase.LoadAssetAtPath<UpgradeData>(path);

        if (upgrade == null)
        {
            upgrade = ScriptableObject.CreateInstance<UpgradeData>();
            AssetDatabase.CreateAsset(upgrade, path);
        }

        Sprite existingIcon = upgrade.icon;
        Undo.RecordObject(upgrade, "Update Trophy pedestal upgrade");

        upgrade.upgradeId = upgradeId;
        upgrade.displayName = displayName;
        upgrade.description = description;
        upgrade.icon = existingIcon;
        upgrade.category = UpgradeCategory.TrophyRoom;
        upgrade.sortOrder = sortOrder;
        upgrade.hiddenUntilUnlocked = false;
        upgrade.unlockDefinition = unlock;
        upgrade.effectType = UpgradeEffectType.GenericValue;
        upgrade.defaultEffectValue = 0f;
        upgrade.levels = new List<UpgradeLevelData>
        {
            new UpgradeLevelData
            {
                levelName = displayName,
                description = description,
                currency = UpgradeCurrency.Gold,
                cost = Mathf.Max(0f, cost),
                effectValue = effectValue
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

    private static PlayerRank[] ResolvePedestalRanks()
    {
        Array values = Enum.GetValues(typeof(PlayerRank));
        List<PlayerRank> all = new List<PlayerRank>();

        foreach (object value in values)
            all.Add((PlayerRank)value);

        all.Sort((a, b) => ((int)a).CompareTo((int)b));

        string[] requiredNames =
        {
            "Global Elite",
            "Global Elite II",
            "Global Elite III",
            "Global Elite IV",
            "Global Elite V",
            "Global Elite VI",
            "Global Elite VII",
            "Global Elite VIII",
            "Global Elite IX",
            "Global Elite X",
            "The Global Elite"
        };

        PlayerRank[] resolved = new PlayerRank[11];
        bool exact = true;

        for (int i = 0; i < requiredNames.Length; i++)
        {
            bool found = false;

            for (int rankIndex = 0; rankIndex < all.Count; rankIndex++)
            {
                string display = PlayerProgressUtility.GetRankDisplayName(
                    all[rankIndex]);

                bool firstSlotAlias = i == 0 &&
                    string.Equals(
                        display,
                        "Global Elite I",
                        StringComparison.OrdinalIgnoreCase);

                if (!string.Equals(
                        display,
                        requiredNames[i],
                        StringComparison.OrdinalIgnoreCase) &&
                    !firstSlotAlias)
                {
                    continue;
                }

                resolved[i] = all[rankIndex];
                found = true;
                break;
            }

            if (!found)
            {
                exact = false;
                break;
            }
        }

        if (exact)
            return resolved;

        if (all.Count < 11)
            return null;

        Debug.LogWarning(
            "TrophyRoomSetup: Exact Global Elite display names were not found. " +
            "Using the highest eleven PlayerRank values in ascending order.");

        int start = all.Count - 11;

        for (int i = 0; i < 11; i++)
            resolved[i] = all[start + i];

        return resolved;
    }

    private static AnimationCurve Curve(params (float x, float y)[] points)
    {
        Keyframe[] keys = new Keyframe[points.Length];

        for (int i = 0; i < points.Length; i++)
            keys[i] = new Keyframe(points[i].x, points[i].y);

        return new AnimationCurve(keys);
    }

    private static string ToRoman(int value)
    {
        int[] values = { 10, 9, 5, 4, 1 };
        string[] symbols = { "X", "IX", "V", "IV", "I" };
        string result = "";

        for (int i = 0; i < values.Length; i++)
        {
            while (value >= values[i])
            {
                result += symbols[i];
                value -= values[i];
            }
        }

        return result;
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

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
