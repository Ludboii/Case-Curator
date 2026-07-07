using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(PatternTierGroup))]
public class PatternTierGroupDrawer : PropertyDrawer
{
    static Dictionary<string, string> pasteBuffers = new Dictionary<string, string>();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var tierProp = property.FindPropertyRelative("tier");
        var multProp = property.FindPropertyRelative("multiplier");
        var idsProp = property.FindPropertyRelative("patternIds");

        float lineHeight = EditorGUIUtility.singleLineHeight + 2;
        Rect r = position;
        r.height = EditorGUIUtility.singleLineHeight;

        EditorGUI.LabelField(r, label, EditorStyles.boldLabel);
        r.y += lineHeight;

        EditorGUI.PropertyField(r, tierProp);
        r.y += lineHeight;

        EditorGUI.PropertyField(r, multProp);
        r.y += lineHeight;

        EditorGUI.LabelField(r, $"Pattern IDs ({idsProp.arraySize} total)");
        r.y += lineHeight;

        string key = property.propertyPath;
        if (!pasteBuffers.ContainsKey(key)) pasteBuffers[key] = "";
        pasteBuffers[key] = EditorGUI.TextField(r, "Paste (comma sep)", pasteBuffers[key]);
        r.y += lineHeight;

        if (GUI.Button(r, "Apply Paste (overwrites list below)"))
        {
            idsProp.ClearArray();
            string[] parts = pasteBuffers[key].Split(',');
            int index = 0;
            foreach (var part in parts)
            {
                if (int.TryParse(part.Trim(), out int id))
                {
                    idsProp.InsertArrayElementAtIndex(index);
                    idsProp.GetArrayElementAtIndex(index).intValue = id;
                    index++;
                }
            }
            pasteBuffers[key] = "";
        }
        r.y += lineHeight;

        EditorGUI.PropertyField(r, idsProp, new GUIContent("List (manual edit)"), true);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var idsProp = property.FindPropertyRelative("patternIds");
        float lineHeight = EditorGUIUtility.singleLineHeight + 2;
        float listHeight = EditorGUI.GetPropertyHeight(idsProp, true);
        return lineHeight * 6 + listHeight;
    }
}