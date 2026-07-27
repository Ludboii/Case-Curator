#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Deletes the local Case Curator save files while the Editor is not in Play
/// Mode. Keeping this outside runtime UI prevents accidental player-facing wipes.
/// </summary>
public static class CaseCuratorSaveResetTool
{
    private const string SaveFileName = "casecatcher_save.json";

    [MenuItem("Tools/Case Curator/Save/Reset Local Save...")]
    public static void ResetLocalSave()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Stop Play Mode",
                "Exit Play Mode before resetting the local save. Otherwise the " +
                "current in-memory state could write a new save when Play Mode stops.",
                "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Reset Local Save",
            "This permanently deletes the main save, backup, temporary save and " +
            "legacy container-completion save for this Editor installation. " +
            "This cannot be undone.",
            "Delete Save",
            "Cancel");

        if (!confirmed)
            return;

        string savePath = Path.Combine(
            Application.persistentDataPath,
            SaveFileName);

        int deletedCount = 0;
        deletedCount += DeleteIfPresent(savePath) ? 1 : 0;
        deletedCount += DeleteIfPresent(savePath + ".bak") ? 1 : 0;
        deletedCount += DeleteIfPresent(savePath + ".tmp") ? 1 : 0;

        ContainerProgressManager.DeleteLegacySaveFile();
        PlayerPrefs.Save();

        Debug.Log(
            $"Case Curator local save reset complete. Deleted {deletedCount} " +
            $"SaveData file(s) from: {Application.persistentDataPath}");

        EditorUtility.DisplayDialog(
            "Save Reset Complete",
            "The local save was deleted. Enter Play Mode to start with a new save.",
            "OK");
    }

    [MenuItem("Tools/Case Curator/Save/Reset Local Save...", true)]
    private static bool ValidateResetLocalSave()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem("Tools/Case Curator/Save/Open Save Folder")]
    public static void OpenSaveFolder()
    {
        EditorUtility.RevealInFinder(Application.persistentDataPath);
    }

    private static bool DeleteIfPresent(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }
}
#endif
