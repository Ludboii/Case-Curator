using UnityEngine;
using UnityEngine.UI;

public class InventoryTabController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject skinInventoryPanel;
    public GameObject caseInventoryPanel;

    [Header("Buttons")]
    public Button skinsButton;
    public Button casesButton;

    private void Awake()
    {
        if (skinsButton != null)
        {
            skinsButton.onClick.RemoveAllListeners();
            skinsButton.onClick.AddListener(ShowSkins);
        }

        if (casesButton != null)
        {
            casesButton.onClick.RemoveAllListeners();
            casesButton.onClick.AddListener(ShowCases);
        }
    }

    private void Start()
    {
        ShowSkins();
    }

    public void ShowSkins()
    {
        if (skinInventoryPanel != null)
            skinInventoryPanel.SetActive(true);

        if (caseInventoryPanel != null)
            caseInventoryPanel.SetActive(false);
    }

    public void ShowCases()
    {
        if (skinInventoryPanel != null)
            skinInventoryPanel.SetActive(false);

        if (caseInventoryPanel != null)
            caseInventoryPanel.SetActive(true);

        InventoryUI caseInventoryUI =
            caseInventoryPanel != null
                ? caseInventoryPanel.GetComponentInChildren<InventoryUI>(true)
                : null;

        if (caseInventoryUI != null)
            caseInventoryUI.Refresh();
    }
}