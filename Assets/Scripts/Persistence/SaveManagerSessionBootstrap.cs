using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Establishes a safe persistence session after scene singletons are available.
/// Existing SaveData is loaded automatically before gameplay state can be saved.
/// If save files exist but neither file has a readable version header, saving is
/// blocked for the session so a blank runtime state cannot overwrite recovery data.
/// </summary>
public sealed class SaveManagerSessionBootstrap : MonoBehaviour
{
    public static bool InitializationFinished { get; private set; }
    public static bool CanSaveCurrentSession { get; private set; }
    public static bool ExistingSaveWasLoaded { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        InitializationFinished = false;
        CanSaveCurrentSession = false;
        ExistingSaveWasLoaded = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SaveManagerSessionBootstrap existing =
            FindFirstObjectByType<SaveManagerSessionBootstrap>();

        if (existing != null)
            return;

        GameObject go = new GameObject("SaveManagerSessionBootstrap");
        DontDestroyOnLoad(go);
        go.AddComponent<SaveManagerSessionBootstrap>();
    }

    private IEnumerator Start()
    {
        // Allow scene Awake/Start methods to establish the manager singletons.
        const int maximumWaitFrames = 300;
        int waitedFrames = 0;

        while ((SaveManager.Instance == null ||
                InventoryManager.Instance == null ||
                SaveManager.Instance.database == null) &&
               waitedFrames < maximumWaitFrames)
        {
            waitedFrames++;
            yield return null;
        }

        SaveManager save = SaveManager.Instance;

        if (save == null || InventoryManager.Instance == null)
        {
            InitializationFinished = true;
            CanSaveCurrentSession = false;
            Debug.LogError(
                "SaveManagerSessionBootstrap: SaveManager or InventoryManager " +
                "was unavailable. Automatic loading and saving are blocked for " +
                "this Play Mode session to protect existing SaveData.");
            yield break;
        }

        string savePath = Path.Combine(
            Application.persistentDataPath,
            "casecatcher_save.json");
        string backupPath = savePath + ".bak";

        bool mainExists = File.Exists(savePath);
        bool backupExists = File.Exists(backupPath);

        if (!mainExists && !backupExists)
        {
            // This is a genuine new game. The current runtime state may be saved.
            InitializationFinished = true;
            CanSaveCurrentSession = true;
            ExistingSaveWasLoaded = false;
            yield break;
        }

        bool mainReadable = HasReadableVersionHeader(savePath);
        bool backupReadable = HasReadableVersionHeader(backupPath);

        if (!mainReadable && !backupReadable)
        {
            InitializationFinished = true;
            CanSaveCurrentSession = false;
            ExistingSaveWasLoaded = false;
            Debug.LogError(
                "SaveManagerSessionBootstrap: Existing main and backup save " +
                "files are unreadable. Automatic saving is blocked so they are " +
                "not overwritten. Check the Console and preserve both files.");
            yield break;
        }

        save.LoadGame();

        // LoadGame uses the readable main file first and falls back to the backup.
        // At this point the managers and database were available and at least one
        // candidate passed the version-header validation.
        ExistingSaveWasLoaded = true;
        CanSaveCurrentSession = true;
        InitializationFinished = true;

        Debug.Log(
            "SaveManagerSessionBootstrap: Existing SaveData loaded automatically.");
    }

    private static bool HasReadableVersionHeader(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            string json = File.ReadAllText(path);

            if (string.IsNullOrWhiteSpace(json) ||
                !json.Contains("\"saveVersion\""))
            {
                return false;
            }

            SaveVersionHeader header =
                JsonUtility.FromJson<SaveVersionHeader>(json);

            return header != null &&
                   header.saveVersion >= 1 &&
                   header.saveVersion <= SaveData.CurrentVersion;
        }
        catch
        {
            return false;
        }
    }
}
