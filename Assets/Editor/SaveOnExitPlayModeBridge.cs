#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Unity does not always invoke application quit callbacks consistently when
/// the Editor Stop button is pressed, depending on play-mode settings and
/// script reload timing. This guarantees one final save before Play Mode ends.
/// </summary>
[InitializeOnLoad]
public static class SaveOnExitPlayModeBridge
{
    static SaveOnExitPlayModeBridge()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(
        PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode)
            return;

        SaveManager saveManager = SaveManager.Instance;

        if (saveManager == null || !saveManager.IsDirty)
            return;

        saveManager.SaveNow();
    }
}
#endif
