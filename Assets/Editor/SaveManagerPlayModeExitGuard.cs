#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Flushes dirty SaveManager state before Play Mode teardown, but only after the
/// persistence bootstrap has established that this runtime session is safe to save.
/// This prevents an unloaded blank scene state from overwriting existing SaveData.
/// </summary>
[InitializeOnLoad]
public static class SaveManagerPlayModeExitGuard
{
    private static readonly FieldInfo SaveDirtyField =
        typeof(SaveManager).GetField(
            "saveDirty",
            BindingFlags.Instance | BindingFlags.NonPublic);

    static SaveManagerPlayModeExitGuard()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode)
            return;

        SaveManager save = SaveManager.Instance;

        if (save == null)
            save = Object.FindFirstObjectByType<SaveManager>();

        if (!SaveManagerSessionBootstrap.InitializationFinished ||
            !SaveManagerSessionBootstrap.CanSaveCurrentSession)
        {
            // SaveManager.OnApplicationQuit can run after this editor callback.
            // Clear only the runtime dirty flag so that quit cannot write an
            // unloaded/unsafe state over the existing main and backup files.
            if (save != null && SaveDirtyField != null)
                SaveDirtyField.SetValue(save, false);

            Debug.LogWarning(
                "SaveManagerPlayModeExitGuard: Blocked the Play Mode exit save " +
                "because the current persistence session was not confirmed safe. " +
                "Existing save files were left untouched.");
            return;
        }

        if (save == null || !save.IsDirty)
            return;

        bool saved = save.SaveNow();

        if (!saved)
        {
            Debug.LogError(
                "SaveManagerPlayModeExitGuard: SaveManager was dirty but could " +
                "not be written before Play Mode exited.");
        }
    }
}
#endif
