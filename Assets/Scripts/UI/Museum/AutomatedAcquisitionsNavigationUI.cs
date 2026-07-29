using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the Automated Acquisitions landing/navigation screen. It deliberately
/// reapplies the selected view in LateUpdate because AutomatedAcquisitionsPanelUI
/// refreshes its internal page state after service events.
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
    [SerializeField] private Button backToNavigationButton;

    private NavigationPage currentPage = NavigationPage.Landing;

    private void Awake()
    {
        SetupButton(receivingDockButton, ShowReceivingDock);
        SetupButton(processingFloorButton, ShowProcessingFloor);
        SetupButton(intakeVaultButton, ShowIntakeVault);
        SetupButton(curatorReportsButton, ShowCuratorReports);
        SetupButton(backToNavigationButton, ShowLanding);
    }

    public void OpenLanding(AutomatedAcquisitionsPanelUI targetPanel = null)
    {
        if (targetPanel != null)
            panel = targetPanel;

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
