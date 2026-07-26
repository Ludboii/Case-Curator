#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity does not guarantee the same shutdown callback order for every Play Mode
/// configuration. This guard explicitly flushes dirty SaveManager state before
/// scene objects and static singletons are torn down when leaving Play Mode.
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
