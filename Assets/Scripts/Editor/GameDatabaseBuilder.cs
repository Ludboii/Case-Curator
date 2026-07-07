using UnityEditor;
using UnityEngine;

public static class GameDatabaseBuilder
{
    const string DatabasePath = "Assets/Data/GameDatabase.asset";

    [MenuItem("Case Catcher/Rebuild Game Database")]
    public static void RebuildGameDatabase()
    {
        GameDatabase database =
            AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);

        if (database == null)
        {
            database = ScriptableObject.CreateInstance<GameDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
            Debug.Log("Created new GameDatabase.asset");
        }

        database.allSkins.Clear();
        database.allCases.Clear();
        database.allCollections.Clear();

        AddAssetsToList("Assets/Data/Skins", database.allSkins);
        AddAssetsToList("Assets/Data/Cases", database.allCases);
        AddAssetsToList("Assets/Data/Collections", database.allCollections);

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Game Database rebuilt: " +
            $"{database.allSkins.Count} skins, " +
            $"{database.allCases.Count} cases, " +
            $"{database.allCollections.Count} collections.");
    }

    static void AddAssetsToList<T>(
        string folder,
        System.Collections.Generic.List<T> list)
        where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets(
            $"t:{typeof(T).Name}",
            new[] { folder });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset != null)
                list.Add(asset);
        }
    }
}