using UnityEngine;

public class MainPanelController : MonoBehaviour
{
    [Header("Main Panels")]
    public GameObject caseShopPanel;
    public GameObject inventoryModePanel;
    public GameObject skinInventoryPanel;
    public GameObject caseInventoryPanel;
    public GameObject museumPanel;
    public GameObject tradeupsPanel;
    public GameObject questsPanel;
    public GameObject upgradesPanel;
    public GameObject minigamesPanel;
    public GameObject statsPanel;
    public GameObject settingsPanel;
    public GameObject debugPanel;

    [Header("Startup")]
    public GameObject startupPanel;

    private void Start()
    {
        ShowOnly(startupPanel != null ? startupPanel : caseShopPanel);
    }

    public void ShowCaseShop() => ShowOnly(caseShopPanel);
    public void ShowInventoryMode() => ShowOnly(inventoryModePanel);
    public void ShowSkinInventory() => ShowOnly(skinInventoryPanel);
    public void ShowCaseInventory() => ShowOnly(caseInventoryPanel);
    public void ShowMuseum() => ShowOnly(museumPanel);
    public void ShowTradeups() => ShowOnly(tradeupsPanel);
    public void ShowQuests() => ShowOnly(questsPanel);
    public void ShowUpgrades() => ShowOnly(upgradesPanel);
    public void ShowMinigames() => ShowOnly(minigamesPanel);
    public void ShowStats() => ShowOnly(statsPanel);
    public void ShowSettings() => ShowOnly(settingsPanel);
    public void ShowDebug() => ShowOnly(debugPanel);
    private void ShowOnly(GameObject panelToShow)
    {
        SetPanel(caseShopPanel, panelToShow);
        SetPanel(inventoryModePanel, panelToShow);
        SetPanel(skinInventoryPanel, panelToShow);
        SetPanel(caseInventoryPanel, panelToShow);
        SetPanel(museumPanel, panelToShow);
        SetPanel(tradeupsPanel, panelToShow);
        SetPanel(questsPanel, panelToShow);
        SetPanel(upgradesPanel, panelToShow);
        SetPanel(minigamesPanel, panelToShow);
        SetPanel(statsPanel, panelToShow);
        SetPanel(settingsPanel, panelToShow);
        SetPanel(debugPanel, panelToShow);
    }

    private void SetPanel(GameObject panel, GameObject panelToShow)
    {
        if (panel == null)
            return;

        panel.SetActive(panel == panelToShow);
    }
}