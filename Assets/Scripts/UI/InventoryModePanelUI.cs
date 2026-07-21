using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the two InventoryModePanel buttons and always closes the mode chooser
/// before opening the requested inventory screen.
/// </summary>
[DisallowMultipleComponent]
public class InventoryModePanelUI : MonoBehaviour
{
    [Header("Navigation")]
    [SerializeField] private MainPanelController mainPanelController;
    [SerializeField] private Button skinInventoryButton;
    [SerializeField] private Button caseInventoryButton;

    private void Awake()
    {
        if (skinInventoryButton != null)
            skinInventoryButton.onClick.AddListener(OpenSkinInventory);

        if (caseInventoryButton != null)
            caseInventoryButton.onClick.AddListener(OpenCaseInventory);
    }

    private void OnDestroy()
    {
        if (skinInventoryButton != null)
            skinInventoryButton.onClick.RemoveListener(OpenSkinInventory);

        if (caseInventoryButton != null)
            caseInventoryButton.onClick.RemoveListener(OpenCaseInventory);
    }

    public void OpenSkinInventory()
    {
        if (mainPanelController == null)
        {
            Debug.LogWarning(
                "InventoryModePanelUI: MainPanelController is not assigned.",
                this);
            return;
        }

        mainPanelController.ShowSkinInventory();
        CloseModePanelIfStillActive();
    }

    public void OpenCaseInventory()
    {
        if (mainPanelController == null)
        {
            Debug.LogWarning(
                "InventoryModePanelUI: MainPanelController is not assigned.",
                this);
            return;
        }

        mainPanelController.ShowCaseInventory();
        CloseModePanelIfStillActive();
    }

    private void CloseModePanelIfStillActive()
    {
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }
}
