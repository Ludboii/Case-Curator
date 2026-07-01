using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkinData))]
public class SkinDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject,
            "m_Script",
            "patternTierGroups", "uniquePatterns",
            "fadeBonusCurve", "fadeRangeStart", "fadeRangeEnd",
            "reverseFadePattern", "fadeOverrides",
            "exteriorPrices", "statTrakExteriorPrices", "souvenirExteriorPrices",
            "vanillaPrice", "vanillaStatTrakPrice");

        SkinData skin = (SkinData)target;

        // --- Pattern lists (Tier Groups / Unique Patterns) ---
        if (skin.patternType != PatternType.None)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Pattern Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("patternTierGroups"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("uniquePatterns"));
        }

        // --- Fade-specific fields ---
        if (skin.patternType == PatternType.Fade)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Fade Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fadeBonusCurve"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fadeRangeStart"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fadeRangeEnd"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("reverseFadePattern"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fadeOverrides"));
        }

        // --- Pricing ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Pricing", EditorStyles.boldLabel);

        if (!skin.isVanilla)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("exteriorPrices"));

            if (skin.canBeStatTrak)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("statTrakExteriorPrices"));

            if (skin.canBeSouvenir)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("souvenirExteriorPrices"));
        }

        if (skin.isVanilla)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("vanillaPrice"));

            if (skin.canBeStatTrak)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("vanillaStatTrakPrice"));
        }

        serializedObject.ApplyModifiedProperties();
    }
}