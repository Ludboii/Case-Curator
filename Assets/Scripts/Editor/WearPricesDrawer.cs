using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(WearPrices))]
public class WearPricesDrawer : PropertyDrawer
{
    static readonly string[] labels     = { "Factory New", "Minimal Wear", "Field-Tested", "Well-Worn", "Battle-Scarred" };
    static readonly string[] fieldNames = { "factoryNew", "minimalWear", "fieldTested", "wellWorn", "battleScarred" };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineHeight = EditorGUIUtility.singleLineHeight + 2;
        position.height = EditorGUIUtility.singleLineHeight;

        EditorGUI.LabelField(position, label, EditorStyles.boldLabel);
        position.y += lineHeight;

        EditorGUI.indentLevel++;
        for (int i = 0; i < fieldNames.Length; i++)
        {
            SerializedProperty prop = property.FindPropertyRelative(fieldNames[i]);
            EditorGUI.PropertyField(position, prop, new GUIContent(labels[i]));
            position.y += lineHeight;
        }
        EditorGUI.indentLevel--;

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return (EditorGUIUtility.singleLineHeight + 2) * 6; // header + 5 fields
    }
}