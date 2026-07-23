using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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

    [Header("Legacy Cleanup")]
    [Tooltip(
        "Disables any remaining MainTabController component at runtime so two " +
        "navigation systems cannot fight over panels and button colours.")]
    public bool disableLegacyMainTabController = true;

    private readonly List<GameObject> allPanels = new List<GameObject>();

    private void Awake()
    {
        DisableLegacyNavigationControllers();
        BuildPanelList();
        SetupButtonListeners();
        PrepareButtonVisuals();
    }

    private void Start()
    {
        GameObject firstPanel = startupPanel != null
            ? startupPanel
            : caseShopPanel;

        ShowPanel(firstPanel, GetSidebarButtonForPanel(firstPanel));
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
    }

    private void DisableLegacyNavigationControllers()
    {
        if (!disableLegacyMainTabController)
            return;

        MonoBehaviour[] behaviours =
            FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour == null || behaviour == this)
                continue;

            if (behaviour.GetType().Name != "MainTabController")
                continue;

            behaviour.enabled = false;
            Debug.LogWarning(
                "MainPanelController disabled the legacy MainTabController. " +
                "Remove that component from MainContent after confirming the " +
                "new navigation works.",
                behaviour);
        }
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

    private void PrepareButtonVisuals()
    {
        PrepareButton(caseShopButton);
        PrepareButton(inventoryButton);
        PrepareButton(museumButton);
        PrepareButton(minigamesButton);
        PrepareButton(questsButton);
        PrepareButton(upgradesButton);
        PrepareButton(statsButton);
        PrepareButton(settingsButton);
        PrepareButton(debugButton);

        SetSelectedSidebarButton(null);
    }

    private static void PrepareButton(Button button)
    {
        if (button == null)
            return;

        // The controller directly sets the authored Image colour. Disabling the
        // Selectable transition prevents Unity's EventSystem focus state from
        // tinting the last clicked button green/black after another tab opens.
        button.transition = Selectable.Transition.None;
    }

    public void ShowCaseShop() =>
        ShowPanel(caseShopPanel, caseShopButton);

    public void ShowInventoryMode() =>
        ShowPanel(inventoryModePanel, inventoryButton);

    public void ShowSkinInventory() =>
        ShowPanel(skinInventoryPanel, inventoryButton);

    public void ShowCaseInventory() =>
        ShowPanel(caseInventoryPanel, inventoryButton);

    public void ShowMuseum() =>
        ShowPanel(museumPanel, museumButton);

    public void ShowTradeups() =>
        ShowPanel(tradeupsPanel, inventoryButton);

    public void ShowQuests() =>
        ShowPanel(questsPanel, questsButton);

    public void ShowUpgrades() =>
        ShowPanel(upgradesPanel, upgradesButton);

    public void ShowMinigames() =>
        ShowPanel(minigamesPanel, minigamesButton);

    public void ShowStats() =>
        ShowPanel(statsPanel, statsButton);

    public void ShowSettings() =>
        ShowPanel(settingsPanel, settingsButton);

    public void ShowDebug() =>
        ShowPanel(debugPanel, debugButton);

    private void ShowPanel(GameObject panelToShow, Button sidebarButton)
    {
        ShowOnly(panelToShow);
        SetSelectedSidebarButton(sidebarButton);

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    public void ShowOnly(GameObject panelToShow)
    {
        BuildPanelList();

        for (int i = 0; i < allPanels.Count; i++)
        {
            GameObject panel = allPanels[i];

            if (panel != null)
                panel.SetActive(panel == panelToShow);
        }

        // A wrong Inspector reference is easier to diagnose with an explicit
        // error than with two overlapping panels.
        if (debugPanel != null &&
            panelToShow != debugPanel &&
            debugPanel.activeSelf)
        {
            debugPanel.SetActive(false);
            Debug.LogError(
                "MainPanelController had to force-hide Debug Panel. Check that " +
                "Debug Panel references Panel_Debug itself, not its child " +
                "MuseumDebugTester.",
                this);
        }
    }

    private Button GetSidebarButtonForPanel(GameObject panel)
    {
        if (panel == caseShopPanel)
            return caseShopButton;

        if (panel == inventoryModePanel ||
            panel == skinInventoryPanel ||
            panel == caseInventoryPanel ||
            panel == tradeupsPanel)
        {
            return inventoryButton;
        }

        if (panel == museumPanel)
            return museumButton;

        if (panel == minigamesPanel)
            return minigamesButton;

        if (panel == questsPanel)
            return questsButton;

        if (panel == upgradesPanel)
            return upgradesButton;

        if (panel == statsPanel)
            return statsButton;

        if (panel == settingsPanel)
            return settingsButton;

        if (panel == debugPanel)
            return debugButton;

        return null;
    }

    private void SetSelectedSidebarButton(Button selected)
    {
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
        if (button == null || button.targetGraphic == null)
            return;

        button.targetGraphic.color = selected
            ? selectedButtonColor
            : normalButtonColor;
    }
}
