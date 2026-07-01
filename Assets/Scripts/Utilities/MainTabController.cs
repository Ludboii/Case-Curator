using UnityEngine;
using UnityEngine.UI;

public class MainTabController : MonoBehaviour
{
    public static MainTabController Instance { get; private set; }

[Header("Main Panels")]
public GameObject caseShopPanel;
public GameObject inventoryModePanel;
public GameObject skinInventoryPanel;
public GameObject caseInventoryPanel;
public GameObject collectionsPanel;
public GameObject minigamesPanel;
public GameObject questsPanel;
public GameObject upgradesPanel;
public GameObject statsPanel;
public GameObject settingsPanel;

   
    [Header("Left Sidebar Buttons")]
public Button caseShopButton;
public Button inventoryButton;
public Button collectionsButton;
public Button minigamesButton;
public Button questsButton;
public Button upgradesButton;
public Button statsButton;
public Button settingsButton;
  
    [Header("Inventory Mode Buttons")]
    public Button skinsInventoryButton;
    public Button casesInventoryButton;

    [Header("Button Visuals")]
    public Color selectedButtonColor = new Color(0.25f, 0.75f, 0.35f, 1f);
    public Color normalButtonColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    private Button currentSelectedLeftButton;
    private Button currentSelectedInventoryModeButton;

    private void Awake()
    {
        Instance = this;
        SetupButtonListeners();
    }

    private void Start()
    {
        ShowCaseShop();
    }

    private void SetupButtonListeners()
    {
        if (caseShopButton != null)
        {
            caseShopButton.onClick.RemoveAllListeners();
            caseShopButton.onClick.AddListener(ShowCaseShop);
        }

        if (inventoryButton != null)
        {
            inventoryButton.onClick.RemoveAllListeners();
            inventoryButton.onClick.AddListener(ShowInventoryMode);
        }

        if (collectionsButton != null)
        {
            collectionsButton.onClick.RemoveAllListeners();
            collectionsButton.onClick.AddListener(ShowCollections);
        }

        if (minigamesButton != null)
        {
            minigamesButton.onClick.RemoveAllListeners();
            minigamesButton.onClick.AddListener(ShowMinigames);
        }

        if (statsButton != null)
        {
            statsButton.onClick.RemoveAllListeners();
            statsButton.onClick.AddListener(ShowStats);
        }
        
        if (questsButton != null)
{
    questsButton.onClick.RemoveAllListeners();
    questsButton.onClick.AddListener(ShowQuests);
}
if (upgradesButton != null)
{
    upgradesButton.onClick.RemoveAllListeners();
    upgradesButton.onClick.AddListener(ShowUpgrades);
}

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(ShowSettings);
        }

        if (skinsInventoryButton != null)
        {
            skinsInventoryButton.onClick.RemoveAllListeners();
            skinsInventoryButton.onClick.AddListener(ShowSkinInventory);
        }

        if (casesInventoryButton != null)
        {
            casesInventoryButton.onClick.RemoveAllListeners();
            casesInventoryButton.onClick.AddListener(ShowCaseInventory);
        }
    }

    public void ShowCaseShop()
    {
        HideAllPanels();

        if (caseShopPanel != null)
            caseShopPanel.SetActive(true);

        SetSelectedLeftButton(caseShopButton);
        SetSelectedInventoryModeButton(null);
    }

    public void ShowInventoryMode()
    {
        HideAllPanels();

        if (inventoryModePanel != null)
            inventoryModePanel.SetActive(true);

        SetSelectedLeftButton(inventoryButton);
        SetSelectedInventoryModeButton(null);
    }

    public void ShowSkinInventory()
    {
        HideAllPanels();

        if (skinInventoryPanel != null)
            skinInventoryPanel.SetActive(true);

        SetSelectedLeftButton(inventoryButton);
        SetSelectedInventoryModeButton(skinsInventoryButton);

        InventoryUI inventoryUI =
            skinInventoryPanel != null
                ? skinInventoryPanel.GetComponentInChildren<InventoryUI>(true)
                : null;

        if (inventoryUI != null)
            inventoryUI.Refresh();
    }

    public void ShowCaseInventory()
    {
        HideAllPanels();

        if (caseInventoryPanel != null)
            caseInventoryPanel.SetActive(true);

        SetSelectedLeftButton(inventoryButton);
        SetSelectedInventoryModeButton(casesInventoryButton);

        CaseInventoryUI caseInventoryUI =
            caseInventoryPanel != null
                ? caseInventoryPanel.GetComponentInChildren<CaseInventoryUI>(true)
                : null;

        if (caseInventoryUI != null)
            caseInventoryUI.Refresh();
    }

    public void ShowCollections()
    {
        HideAllPanels();

        if (collectionsPanel != null)
            collectionsPanel.SetActive(true);

        SetSelectedLeftButton(collectionsButton);
        SetSelectedInventoryModeButton(null);
    }

    public void ShowQuests()
{
    HideAllPanels();

    if (questsPanel != null)
        questsPanel.SetActive(true);

    SetSelectedLeftButton(questsButton);
    SetSelectedInventoryModeButton(null);
}

public void ShowUpgrades()
{
    HideAllPanels();

    if (upgradesPanel != null)
        upgradesPanel.SetActive(true);

    SetSelectedLeftButton(upgradesButton);
    SetSelectedInventoryModeButton(null);
}

    public void ShowMinigames()
    {
        HideAllPanels();

        if (minigamesPanel != null)
            minigamesPanel.SetActive(true);

        SetSelectedLeftButton(minigamesButton);
        SetSelectedInventoryModeButton(null);
    }

    public void ShowStats()
    {
        HideAllPanels();

        if (statsPanel != null)
            statsPanel.SetActive(true);

        SetSelectedLeftButton(statsButton);
        SetSelectedInventoryModeButton(null);
    }

    public void ShowSettings()
    {
        HideAllPanels();

        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        SetSelectedLeftButton(settingsButton);
        SetSelectedInventoryModeButton(null);
    }

    private void HideAllPanels()
    {
        if (caseShopPanel != null)
            caseShopPanel.SetActive(false);

        if (inventoryModePanel != null)
            inventoryModePanel.SetActive(false);

        if (skinInventoryPanel != null)
            skinInventoryPanel.SetActive(false);

        if (caseInventoryPanel != null)
            caseInventoryPanel.SetActive(false);

        if (collectionsPanel != null)
            collectionsPanel.SetActive(false);

if (minigamesPanel != null)
    minigamesPanel.SetActive(false);

if (questsPanel != null)
    questsPanel.SetActive(false);

if (upgradesPanel != null)
    upgradesPanel.SetActive(false);

        if (statsPanel != null)
            statsPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

private void SetSelectedLeftButton(Button selectedButton)
{
    currentSelectedLeftButton = selectedButton;

    ApplyButtonVisual(caseShopButton, caseShopButton == currentSelectedLeftButton);
    ApplyButtonVisual(inventoryButton, inventoryButton == currentSelectedLeftButton);
    ApplyButtonVisual(collectionsButton, collectionsButton == currentSelectedLeftButton);
    ApplyButtonVisual(minigamesButton, minigamesButton == currentSelectedLeftButton);
    ApplyButtonVisual(questsButton, questsButton == currentSelectedLeftButton);
    ApplyButtonVisual(upgradesButton, upgradesButton == currentSelectedLeftButton);
    ApplyButtonVisual(statsButton, statsButton == currentSelectedLeftButton);
    ApplyButtonVisual(settingsButton, settingsButton == currentSelectedLeftButton);
}

    private void SetSelectedInventoryModeButton(Button selectedButton)
    {
        currentSelectedInventoryModeButton = selectedButton;

        ApplyButtonVisual(skinsInventoryButton, skinsInventoryButton == currentSelectedInventoryModeButton);
        ApplyButtonVisual(casesInventoryButton, casesInventoryButton == currentSelectedInventoryModeButton);
    }

    private void ApplyButtonVisual(Button button, bool selected)
    {
        if (button == null)
            return;

        Color targetColor = selected ? selectedButtonColor : normalButtonColor;

        ColorBlock colors = button.colors;
        colors.normalColor = targetColor;
        colors.highlightedColor = targetColor;
        colors.pressedColor = targetColor * 0.9f;
        colors.selectedColor = targetColor;
        colors.disabledColor = targetColor;
        colors.colorMultiplier = 1f;

        button.colors = colors;
    }
}