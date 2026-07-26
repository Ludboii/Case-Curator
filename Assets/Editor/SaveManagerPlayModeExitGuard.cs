#if UNITY_EDITOR
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
    static SaveManagerPlayModeExitGuard()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode)
            return;

        if (!SaveManagerSessionBootstrap.InitializationFinished ||
            !SaveManagerSessionBootstrap.CanSaveCurrentSession)
        {
            Debug.LogWarning(
                "SaveManagerPlayModeExitGuard: Skipped the Play Mode exit save " +
                "because the current persistence session was not confirmed safe. " +
                "Existing save files were left untouched.");
            return;
        }

        SaveManager save = SaveManager.Instance;

        if (save == null)
            save = Object.FindFirstObjectByType<SaveManager>();

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
