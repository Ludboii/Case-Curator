#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TradeupResolver))]
public class TradeupResolverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField(
            "Covert Mapping Tools",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Map only Covert sources that are allowed to produce a Rare " +
            "Special item. Normal Covert contracts may use knives and gloves. " +
            "StatTrak Covert contracts automatically exclude gloves and every " +
            "other Rare Special skin with Can Be StatTrak disabled.",
            MessageType.Info);

        if (GUILayout.Button("Validate Covert Tradeup Mappings"))
            ValidateMappings();
    }

    private void ValidateMappings()
    {
        serializedObject.Update();

        SerializedProperty mappings =
            serializedObject.FindProperty("covertTradeupPools");

        if (mappings == null)
        {
            Debug.LogError(
                "TradeupResolverEditor: Could not find covertTradeupPools.");
            return;
        }

        int errorCount = 0;
        int warningCount = 0;
        HashSet<CollectionData> seenSources =
            new HashSet<CollectionData>();

        for (int i = 0; i < mappings.arraySize; i++)
        {
            SerializedProperty mapping =
                mappings.GetArrayElementAtIndex(i);

            CollectionData source =
                mapping.FindPropertyRelative("sourceCollection")
                    .objectReferenceValue as CollectionData;

            CaseData rareSpecialCase =
                mapping.FindPropertyRelative("rareSpecialCase")
                    .objectReferenceValue as CaseData;

            string label = $"Mapping {i + 1}";

            if (source == null)
            {
                Debug.LogError($"{label}: Source Collection is missing.");
                errorCount++;
                continue;
            }

            if (!seenSources.Add(source))
            {
                Debug.LogError(
                    $"{label}: {source.collectionName} is mapped more than once.");
                errorCount++;
            }

            if (rareSpecialCase == null)
            {
                Debug.LogError(
                    $"{label}: {source.collectionName} has no Rare Special Case.");
                errorCount++;
                continue;
            }

            int normalRareSpecialCount = 0;
            int statTrakRareSpecialCount = 0;
            int gloveCount = 0;

            if (rareSpecialCase.dropPool != null)
            {
                HashSet<SkinData> normalUnique = new HashSet<SkinData>();
                HashSet<SkinData> statTrakUnique = new HashSet<SkinData>();

                for (int dropIndex = 0;
                     dropIndex < rareSpecialCase.dropPool.Count;
                     dropIndex++)
                {
                    WeightedDrop drop =
                        rareSpecialCase.dropPool[dropIndex];

                    if (drop == null ||
                        drop.skin == null ||
                        drop.skin.rarity != Rarity.RareSpecial)
                    {
                        continue;
                    }

                    SkinData skin = drop.skin;
                    normalUnique.Add(skin);

                    if (skin.canBeStatTrak)
                        statTrakUnique.Add(skin);

                    if (LooksLikeGlove(skin.weaponName))
                        gloveCount++;
                }

                normalRareSpecialCount = normalUnique.Count;
                statTrakRareSpecialCount = statTrakUnique.Count;
            }

            if (normalRareSpecialCount == 0)
            {
                Debug.LogError(
                    $"{label}: {rareSpecialCase.caseName} contains no " +
                    "Rare Special outputs. {source.collectionName} cannot " +
                    "be used for a Covert tradeup.");
                errorCount++;
                continue;
            }

            if (statTrakRareSpecialCount == 0)
            {
                Debug.LogWarning(
                    $"{label}: {source.collectionName} supports normal Covert " +
                    $"tradeups ({normalRareSpecialCount} outputs), but no " +
                    "StatTrak Covert tradeup. This is expected for glove-only pools.");
                warningCount++;
            }
            else
            {
                Debug.Log(
                    $"{label}: {source.collectionName} -> " +
                    $"{rareSpecialCase.caseName}. Normal outputs: " +
                    $"{normalRareSpecialCount}; StatTrak outputs: " +
                    $"{statTrakRareSpecialCount}; glove entries: {gloveCount}.");
            }
        }

        if (mappings.arraySize == 0)
        {
            Debug.LogWarning(
                "TradeupResolver has no Covert tradeup mappings. " +
                "Five-Covert contracts will be unavailable.");
            warningCount++;
        }

        Debug.Log(
            $"Covert mapping validation complete. " +
            $"Mappings: {mappings.arraySize}, Errors: {errorCount}, " +
            $"Warnings: {warningCount}.");
    }

    private static bool LooksLikeGlove(string weaponName)
    {
        if (string.IsNullOrWhiteSpace(weaponName))
            return false;

        string normalized = weaponName.ToLowerInvariant();

        return normalized.Contains("glove") ||
               normalized.Contains("hand wrap") ||
               normalized.Contains("handwrap");
    }
}
#endif
