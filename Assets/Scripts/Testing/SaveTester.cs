using UnityEngine;

public class SaveTester : MonoBehaviour
{
    public void Save()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveGame();
        else
            Debug.LogError("No SaveManager found.");
    }

    public void Load()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.LoadGame();
        else
            Debug.LogError("No SaveManager found.");
    }

    public void DeleteSave()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.DeleteSave();
        else
            Debug.LogError("No SaveManager found.");
    }
}