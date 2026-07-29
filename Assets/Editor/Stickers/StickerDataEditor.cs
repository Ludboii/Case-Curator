#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// StickerData inherits SkinData so stickers can use the existing inventory and
/// save systems. The weapon-skin fields are compatibility data maintained by
/// StickerData itself and are intentionally hidden from the normal inspector.
/// </summary>
[CustomEditor(typeof(StickerData))]
[CanEditMultipleObjects]
public sealed class StickerDataEditor : Editor
{
    private SerializedProperty apiId;
    private SerializedProperty icon;

    private SerializedProperty displayName;
    private SerializedProperty stickerRarity;
    private SerializedProperty marketValue;

    private SerializedProperty capsules;
    private SerializedProperty tournamentEvent;
    private SerializedProperty teamName;
    private SerializedProperty playerName;
    private SerializedProperty year;

    private SerializedProperty effect;
    private SerializedProperty marketHashName;
    private SerializedProperty sourceImageUrl;

    private void OnEnable()
    {
        apiId = serializedObject.FindProperty("apiId");
        icon = serializedObject.FindProperty("icon");

        displayName = serializedObject.FindProperty("displayName");
        stickerRarity = serializedObject.FindProperty("stickerRarity");
        marketValue = serializedObject.FindProperty("marketValue");

        capsules = serializedObject.FindProperty("capsules");
        tournamentEvent = serializedObject.FindProperty("tournamentEvent");
        teamName = serializedObject.FindProperty("teamName");
        playerName = serializedObject.FindProperty("playerName");
        year = serializedObject.FindProperty("year");

        effect = serializedObject.FindProperty("effect");
        marketHashName = serializedObject.FindProperty("marketHashName");
        sourceImageUrl = serializedObject.FindProperty("sourceImageUrl");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject((StickerData)target), typeof(StickerData), false);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Sticker Asset", EditorStyles.boldLabel);
        Draw(apiId, "API ID");
        Draw(icon, "Icon");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Sticker Identity", EditorStyles.boldLabel);
        Draw(displayName, "Display Name");
        Draw(stickerRarity, "Sticker Rarity");
        Draw(marketValue, "Market Value");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        Draw(capsules, "Capsules", true);
        Draw(tournamentEvent, "Tournament / Event");
        Draw(teamName, "Team");
        Draw(playerName, "Player");
        Draw(year, "Year");

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Import Metadata", EditorStyles.boldLabel);
        Draw(effect, "Effect");
        Draw(marketHashName, "Market Hash Name");
        Draw(sourceImageUrl, "Source Image URL");

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "Weapon-skin compatibility fields, float data, pattern settings, " +
            "Exterior Prices, StatTrak prices, Souvenir prices and vanilla prices " +
            "are maintained automatically and hidden for StickerData assets.",
            MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }

    private static void Draw(
        SerializedProperty property,
        string label,
        bool includeChildren = false)
    {
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren);
    }
}
#endif
