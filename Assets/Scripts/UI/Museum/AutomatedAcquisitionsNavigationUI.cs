using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the Automated Acquisitions landing/navigation screen and separates the
/// two back actions:
/// - department header back returns to the department navigation landing;
/// - landing-page back exits to the Museum entrance.
/// </summary>
public class AutomatedAcquisitionsNavigationUI : MonoBehaviour
{
    public enum NavigationPage
    {
        Landing,
        ReceivingDock,
        ProcessingFloor,
        IntakeVault,
        CuratorReports
    }

    [Header("References")]
    [SerializeField] private AutomatedAcquisitionsPanelUI panel;
    [SerializeField] private MuseumPanelUI museumPanel;
    [SerializeField] private GameObject navigationRoot;
    [SerializeField] private GameObject receivingDockView;
    [SerializeField] private GameObject processingFloorView;
    [SerializeField] private GameObject intakeVaultView;
    [SerializeField] private GameObject curatorReportsView;

    [Header("Navigation Buttons")]
    [SerializeField] private Button receivingDockButton;
    [SerializeField] private Button processingFloorButton;
    [SerializeField] private Button intakeVaultButton;
    [SerializeField] private Button curatorReportsButton;

    [Header("Back Buttons")]
    [Tooltip("Button shown inside department pages. Returns to NavigationRoot.")]
    [SerializeField] private Button headerBackButton;

    [Tooltip("Button shown on NavigationRoot. Returns to the Museum entrance.")]
    [SerializeField] private Button navigationBackButton;

    [Header("Legacy")]
    [Tooltip("Old shared back reference. Leave empty after assigning both new buttons.")]
    [SerializeField] private Button backToNavigationButton;

    private NavigationPage currentPage = NavigationPage.Landing;

    public NavigationPage CurrentPage => currentPage;

    private void Awake()
    {
        ResolveReferences();

        SetupButton(receivingDockButton, ShowReceivingDock);
        SetupButton(processingFloorButton, ShowProcessingFloor);
        SetupButton(intakeVaultButton, ShowIntakeVault);
        SetupButton(curatorReportsButton, ShowCuratorReports);
        SetupButton(headerBackButton, ShowLanding);
        SetupButton(navigationBackButton, ExitToMuseumEntrance);

        // Backwards compatibility for the original single-button setup.
        // It behaves as the department header back button only.
        if (headerBackButton == null)
            SetupButton(backToNavigationButton, ShowLanding);
    }

    public void OpenLanding(AutomatedAcquisitionsPanelUI targetPanel = null)
    {
        if (targetPanel != null)
            panel = targetPanel;

        ResolveReferences();

        if (panel != null)
            panel.Open();

        currentPage = NavigationPage.Landing;
        ApplyPage();
    }

    public void ShowLanding()
    {
        currentPage = NavigationPage.Landing;
        ApplyPage();
    }

    public void ShowReceivingDock()
    {
        currentPage = NavigationPage.ReceivingDock;
        ApplyPage();
    }

    public void ShowProcessingFloor()
    {
        currentPage = NavigationPage.ProcessingFloor;
        ApplyPage();
    }

    public void ShowIntakeVault()
    {
        currentPage = NavigationPage.IntakeVault;
        ApplyPage();
    }

    public void ShowCuratorReports()
    {
        currentPage = NavigationPage.CuratorReports;
        ApplyPage();
    }

    public void ExitToMuseumEntrance()
    {
        ResolveReferences();

        if (panel != null)
            panel.Close();
        else
            gameObject.SetActive(false);

        currentPage = NavigationPage.Landing;

        if (museumPanel != null)
            museumPanel.ShowEntrance();
    }

    private void LateUpdate()
    {
        // Panel refreshes can reset the old internal page to Receiving Dock.
        // Reapplying here keeps the landing page and selected destination stable.
        ApplyPage();
    }

    private void ApplyPage()
    {
        SetActive(navigationRoot, currentPage == NavigationPage.Landing);
        SetActive(
            receivingDockView,
            currentPage == NavigationPage.ReceivingDock);
        SetActive(
            processingFloorView,
            currentPage == NavigationPage.ProcessingFloor);
        SetActive(intakeVaultView, currentPage == NavigationPage.IntakeVault);
        SetActive(
            curatorReportsView,
            currentPage == NavigationPage.CuratorReports);

        if (headerBackButton != null)
        {
            headerBackButton.gameObject.SetActive(
                currentPage != NavigationPage.Landing);
        }

        if (navigationBackButton != null)
        {
            navigationBackButton.gameObject.SetActive(
                currentPage == NavigationPage.Landing);
        }
    }

    private void ResolveReferences()
    {
        if (panel == null)
        {
            panel = GetComponent<AutomatedAcquisitionsPanelUI>();

            if (panel == null)
            {
                panel = FindFirstObjectByType<AutomatedAcquisitionsPanelUI>(
                    FindObjectsInactive.Include);
            }
        }

        if (museumPanel == null)
        {
            museumPanel = FindFirstObjectByType<MuseumPanelUI>(
                FindObjectsInactive.Include);
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private static void SetupButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }
}
