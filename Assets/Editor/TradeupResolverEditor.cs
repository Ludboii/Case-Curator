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
            "Every mapped Rare Special item stays in the Covert output pool. " +
            "With five StatTrak Covert inputs, knives that support StatTrak " +
            "remain StatTrak, while gloves and other normal-only outputs are " +
            "awarded without StatTrak.",
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

            int totalRareSpecialCount = 0;
            int statTrakCapableCount = 0;
            int normalOnlyCount = 0;
            int gloveCount = 0;

            if (rareSpecialCase.dropPool != null)
            {
                HashSet<SkinData> uniqueRareSpecials =
                    new HashSet<SkinData>();

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

                    if (!uniqueRareSpecials.Add(skin))
                        continue;

                    if (skin.canBeStatTrak)
                        statTrakCapableCount++;
                    else
                        normalOnlyCount++;

                    if (LooksLikeGlove(skin.weaponName))
                        gloveCount++;
                }

                totalRareSpecialCount = uniqueRareSpecials.Count;
            }

            if (totalRareSpecialCount == 0)
            {
                Debug.LogError(
                    $"{label}: {rareSpecialCase.caseName} contains no " +
                    "Rare Special outputs. {source.collectionName} cannot " +
                    "be used for a Covert tradeup.");
                errorCount++;
                continue;
            }

            Debug.Log(
                $"{label}: {source.collectionName} -> " +
                $"{rareSpecialCase.caseName}. Total outputs: " +
                $"{totalRareSpecialCount}; StatTrak-capable: " +
                $"{statTrakCapableCount}; normal-only: {normalOnlyCount}; " +
                $"gloves: {gloveCount}.");
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
