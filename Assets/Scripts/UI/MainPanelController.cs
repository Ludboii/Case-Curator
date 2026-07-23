using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Single authority for Case Curator's main-panel navigation.
///
/// This replaces the overlapping responsibilities previously split between
/// MainPanelController and MainTabController. It owns all main panel visibility,
/// all sidebar/inventory-mode button listeners, and selected-button visuals.
/// </summary>
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

    [Header("Left Sidebar Buttons")]
    public Button caseShopButton;
    public Button inventoryButton;
    public Button museumButton;
    public Button minigamesButton;
    public Button questsButton;
    public Button upgradesButton;
    public Button statsButton;
    public Button settingsButton;
    public Button debugButton;

    [Header("Inventory Mode Buttons")]
    public Button skinsInventoryButton;
    public Button casesInventoryButton;

    [Header("Button Visuals")]
    public Color selectedButtonColor = new Color(0.25f, 0.75f, 0.35f, 1f);
    public Color normalButtonColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Header("Startup")]
    public GameObject startupPanel;

    private readonly List<GameObject> allPanels = new List<GameObject>();
    private Button selectedSidebarButton;

    private void Awake()
    {
        BuildPanelList();
        SetupButtonListeners();
    }

    private void Start()
    {
        GameObject firstPanel = startupPanel != null
            ? startupPanel
            : caseShopPanel;

        ShowOnly(firstPanel);
        RefreshSidebarSelectionForPanel(firstPanel);
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
    }

    private void BuildPanelList()
    {
        allPanels.Clear();

        AddPanel(caseShopPanel);
        AddPanel(inventoryModePanel);
        AddPanel(skinInventoryPanel);
        AddPanel(caseInventoryPanel);
        AddPanel(museumPanel);
        AddPanel(tradeupsPanel);
        AddPanel(questsPanel);
        AddPanel(upgradesPanel);
        AddPanel(minigamesPanel);
        AddPanel(statsPanel);
        AddPanel(settingsPanel);
        AddPanel(debugPanel);
    }

    private void AddPanel(GameObject panel)
    {
        if (panel != null && !allPanels.Contains(panel))
            allPanels.Add(panel);
    }

    private void SetupButtonListeners()
    {
        SetupButton(caseShopButton, ShowCaseShop);
        SetupButton(inventoryButton, ShowInventoryMode);
        SetupButton(museumButton, ShowMuseum);
        SetupButton(minigamesButton, ShowMinigames);
        SetupButton(questsButton, ShowQuests);
        SetupButton(upgradesButton, ShowUpgrades);
        SetupButton(statsButton, ShowStats);
        SetupButton(settingsButton, ShowSettings);
        SetupButton(debugButton, ShowDebug);

        SetupButton(skinsInventoryButton, ShowSkinInventory);
        SetupButton(casesInventoryButton, ShowCaseInventory);
    }

    private void RemoveButtonListeners()
    {
        RemoveButton(caseShopButton, ShowCaseShop);
        RemoveButton(inventoryButton, ShowInventoryMode);
        RemoveButton(museumButton, ShowMuseum);
        RemoveButton(minigamesButton, ShowMinigames);
        RemoveButton(questsButton, ShowQuests);
        RemoveButton(upgradesButton, ShowUpgrades);
        RemoveButton(statsButton, ShowStats);
        RemoveButton(settingsButton, ShowSettings);
        RemoveButton(debugButton, ShowDebug);

        RemoveButton(skinsInventoryButton, ShowSkinInventory);
        RemoveButton(casesInventoryButton, ShowCaseInventory);
    }

    private static void SetupButton(Button button, UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void RemoveButton(Button button, UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }

    public void ShowCaseShop()
    {
        ShowOnly(caseShopPanel);
        SetSelectedSidebarButton(caseShopButton);
    }

    public void ShowInventoryMode()
    {
        ShowOnly(inventoryModePanel);
        SetSelectedSidebarButton(inventoryButton);
    }

    public void ShowSkinInventory()
    {
        ShowOnly(skinInventoryPanel);
        SetSelectedSidebarButton(inventoryButton);
    }

    public void ShowCaseInventory()
    {
        ShowOnly(caseInventoryPanel);
        SetSelectedSidebarButton(inventoryButton);
    }

    public void ShowMuseum()
    {
        ShowOnly(museumPanel);
        SetSelectedSidebarButton(museumButton);
    }

    public void ShowTradeups()
    {
        ShowOnly(tradeupsPanel);

        // Tradeups currently opens from inventory rather than a dedicated
        // sidebar tab, so Inventory remains the selected sidebar section.
        SetSelectedSidebarButton(inventoryButton);
    }

    public void ShowQuests()
    {
        ShowOnly(questsPanel);
        SetSelectedSidebarButton(questsButton);
    }

    public void ShowUpgrades()
    {
        ShowOnly(upgradesPanel);
        SetSelectedSidebarButton(upgradesButton);
    }

    public void ShowMinigames()
    {
        ShowOnly(minigamesPanel);
        SetSelectedSidebarButton(minigamesButton);
    }

    public void ShowStats()
    {
        ShowOnly(statsPanel);
        SetSelectedSidebarButton(statsButton);
    }

    public void ShowSettings()
    {
        ShowOnly(settingsPanel);
        SetSelectedSidebarButton(settingsButton);
    }

    public void ShowDebug()
    {
        ShowOnly(debugPanel);
        SetSelectedSidebarButton(debugButton);
    }

    public void ShowOnly(GameObject panelToShow)
    {
        // Rebuild in case a panel reference was assigned after Awake while
        // editing the scene or prefab instance.
        BuildPanelList();

        for (int i = 0; i < allPanels.Count; i++)
        {
            GameObject panel = allPanels[i];

            if (panel != null)
                panel.SetActive(panel == panelToShow);
        }
    }

    private void RefreshSidebarSelectionForPanel(GameObject panel)
    {
        if (panel == caseShopPanel)
            SetSelectedSidebarButton(caseShopButton);
        else if (panel == inventoryModePanel ||
                 panel == skinInventoryPanel ||
                 panel == caseInventoryPanel ||
                 panel == tradeupsPanel)
            SetSelectedSidebarButton(inventoryButton);
        else if (panel == museumPanel)
            SetSelectedSidebarButton(museumButton);
        else if (panel == minigamesPanel)
            SetSelectedSidebarButton(minigamesButton);
        else if (panel == questsPanel)
            SetSelectedSidebarButton(questsButton);
        else if (panel == upgradesPanel)
            SetSelectedSidebarButton(upgradesButton);
        else if (panel == statsPanel)
            SetSelectedSidebarButton(statsButton);
        else if (panel == settingsPanel)
            SetSelectedSidebarButton(settingsButton);
        else if (panel == debugPanel)
            SetSelectedSidebarButton(debugButton);
        else
            SetSelectedSidebarButton(null);
    }

    private void SetSelectedSidebarButton(Button selected)
    {
        selectedSidebarButton = selected;

        ApplyButtonVisual(caseShopButton, selected == caseShopButton);
        ApplyButtonVisual(inventoryButton, selected == inventoryButton);
        ApplyButtonVisual(museumButton, selected == museumButton);
        ApplyButtonVisual(minigamesButton, selected == minigamesButton);
        ApplyButtonVisual(questsButton, selected == questsButton);
        ApplyButtonVisual(upgradesButton, selected == upgradesButton);
        ApplyButtonVisual(statsButton, selected == statsButton);
        ApplyButtonVisual(settingsButton, selected == settingsButton);
        ApplyButtonVisual(debugButton, selected == debugButton);
    }

    private void ApplyButtonVisual(Button button, bool selected)
    {
        if (button == null)
            return;

        Color target = selected
            ? selectedButtonColor
            : normalButtonColor;

        Graphic targetGraphic = button.targetGraphic;

        if (targetGraphic != null)
            targetGraphic.color = target;

        // Keep every Selectable state visually consistent with the controller's
        // current selected/unselected colour. This prevents EventSystem focus
        // from leaving a stale highlighted tab behind.
        ColorBlock colors = button.colors;
        colors.normalColor = target;
        colors.highlightedColor = target;
        colors.pressedColor = target * 0.9f;
        colors.selectedColor = target;
        colors.disabledColor = target;
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }
}
