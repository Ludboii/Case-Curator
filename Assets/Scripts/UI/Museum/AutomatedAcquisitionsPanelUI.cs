using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum AutomatedAcquisitionsPage
{
    ReceivingDock,
    ProcessingFloor,
    IntakeVault,
    CuratorReports
}

/// <summary>
/// Complete v1 department UI: Receiving Dock licences/research, Procurement and
/// Processing Lines, Uncatalogued Intake Vault, Calibration readout and Curator
/// Reports. The service remains the only gameplay authority.
/// </summary>
public class AutomatedAcquisitionsPanelUI : MonoBehaviour
{
    [Header("Service")]
    [SerializeField] private AutoAcquisitionService service;

    [Header("Root and Header")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button refreshButton;

    [Header("Wing Lock")]
    [SerializeField] private GameObject lockedRoot;
    [SerializeField] private TMP_Text lockedText;

    [Header("Navigation")]
    [SerializeField] private Button receivingDockButton;
    [SerializeField] private Button processingFloorButton;
    [SerializeField] private Button intakeVaultButton;
    [SerializeField] private Button curatorReportsButton;

    [Header("Views")]
    [SerializeField] private GameObject receivingDockView;
    [SerializeField] private GameObject processingFloorView;
    [SerializeField] private GameObject intakeVaultView;
    [SerializeField] private GameObject curatorReportsView;

    [Header("Receiving Dock")]
    [SerializeField] private Transform categoryContent;
    [SerializeField] private AutoAcquisitionCategoryCardUI categoryCardPrefab;
    [SerializeField] private TMP_Text selectedArchiveText;
    [SerializeField] private Transform containerContent;
    [SerializeField] private AutoAcquisitionContainerCardUI containerCardPrefab;

    [Header("Processing Floor")]
    [SerializeField] private TMP_Text calibrationText;
    [SerializeField] private TMP_Text processingRulesText;
    [SerializeField] private Transform lineContent;
    [SerializeField] private AutoAcquisitionLineCardUI lineCardPrefab;

    [Header("Intake Vault")]
    [SerializeField] private TMP_Text intakeCapacityText;
    [SerializeField] private Button claimAllButton;
    [SerializeField] private Transform intakeContent;
    [SerializeField] private AutoAcquisitionIntakeItemCardUI intakeItemCardPrefab;
    [SerializeField] private GameObject intakeEmptyRoot;

    [Header("Curator Reports")]
    [SerializeField] private TMP_Text reportsText;

    [Header("Status Notification")]
    [SerializeField] private GameObject statusRoot;
    [SerializeField] private TMP_Text statusText;
    [SerializeField, Min(0.5f)] private float statusSeconds = 3f;

    private readonly List<GameObject> spawnedCategoryCards =
        new List<GameObject>();
    private readonly List<GameObject> spawnedContainerCards =
        new List<GameObject>();
    private readonly List<GameObject> spawnedLineCards =
        new List<GameObject>();
    private readonly List<AutoAcquisitionLineCardUI> liveLineCards =
        new List<AutoAcquisitionLineCardUI>();
    private readonly List<GameObject> spawnedIntakeCards =
        new List<GameObject>();

    private AutomatedAcquisitionsPage currentPage =
        AutomatedAcquisitionsPage.ReceivingDock;
    private string selectedCategoryId;
    private float hideStatusAt;
    private float nextLiveRefresh;
    private bool subscribed;

    private void Awake()
    {
        ResolveService();
        SetupButton(closeButton, Close);
        SetupButton(refreshButton, RefreshAll);
        SetupButton(receivingDockButton, ShowReceivingDock);
        SetupButton(processingFloorButton, ShowProcessingFloor);
        SetupButton(intakeVaultButton, ShowIntakeVault);
        SetupButton(curatorReportsButton, ShowCuratorReports);
        SetupButton(claimAllButton, ClaimAll);

        if (statusRoot != null)
            statusRoot.SetActive(false);
    }

    private void OnEnable()
    {
        ResolveService();
        Subscribe();
        RefreshAll();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearAllCards();
    }

    private void Update()
    {
        if (statusRoot != null &&
            statusRoot.activeSelf &&
            Time.unscaledTime >= hideStatusAt)
        {
            statusRoot.SetActive(false);
        }

        if (Time.unscaledTime < nextLiveRefresh)
            return;

        nextLiveRefresh = Time.unscaledTime + 0.5f;
        RefreshHeaderOnly();

        for (int i = 0; i < liveLineCards.Count; i++)
        {
            if (liveLineCards[i] != null)
                liveLineCards[i].RefreshState();
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    public void Open()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);
        else
            gameObject.SetActive(true);

        RefreshAll();
    }

    public void Close()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    public void SelectCategory(string categoryId)
    {
        selectedCategoryId = categoryId;
        currentPage = AutomatedAcquisitionsPage.ReceivingDock;
        ApplyPage();
        RebuildContainerCards();
    }

    public void HandleActionResult(AutoAcquisitionActionResult result)
    {
        if (result == null)
            return;

        ShowStatus(result.message, !result.success);
        RefreshAll();
    }

    public void ShowStatus(string message, bool error)
    {
        if (statusText != null)
        {
            statusText.text = message ?? "";
            statusText.color = error
                ? new Color(1f, 0.45f, 0.35f)
                : new Color(0.45f, 1f, 0.55f);
        }

        if (statusRoot != null)
            statusRoot.SetActive(true);

        hideStatusAt = Time.unscaledTime + Mathf.Max(0.5f, statusSeconds);
    }

    public void RefreshAll()
    {
        ResolveService();

        if (service == null)
        {
            ShowStatus("AutoAcquisitionService is unavailable.", true);
            return;
        }

        service.ProcessNow();
        AutoAcquisitionWingSnapshot snapshot = service.GetSnapshot(false);
        ApplyLock(snapshot);
        RefreshHeader(snapshot);
        EnsureSelectedCategory();
        RebuildCategoryCards();
        RebuildContainerCards();
        RebuildLineCards(snapshot.lineCount);
        RebuildIntakeCards();
        RefreshReports(snapshot);
        ApplyPage();
    }

    private void ShowReceivingDock()
    {
        currentPage = AutomatedAcquisitionsPage.ReceivingDock;
        ApplyPage();
    }

    private void ShowProcessingFloor()
    {
        currentPage = AutomatedAcquisitionsPage.ProcessingFloor;
        ApplyPage();
    }

    private void ShowIntakeVault()
    {
        currentPage = AutomatedAcquisitionsPage.IntakeVault;
        ApplyPage();
    }

    private void ShowCuratorReports()
    {
        currentPage = AutomatedAcquisitionsPage.CuratorReports;
        ApplyPage();
    }

    private void ClaimAll()
    {
        if (service == null)
            return;

        HandleActionResult(service.ClaimAll());
    }

    private void ApplyLock(AutoAcquisitionWingSnapshot snapshot)
    {
        bool unlocked = snapshot != null && snapshot.unlocked;

        if (lockedRoot != null)
            lockedRoot.SetActive(!unlocked);

        if (lockedText != null)
        {
            lockedText.text = unlocked
                ? ""
                : snapshot != null
                    ? snapshot.lockedReason
                    : "Automated Acquisitions is locked.";
        }

        if (receivingDockButton != null)
            receivingDockButton.interactable = unlocked;
        if (processingFloorButton != null)
            processingFloorButton.interactable = unlocked;
        if (intakeVaultButton != null)
            intakeVaultButton.interactable = unlocked;
        if (curatorReportsButton != null)
            curatorReportsButton.interactable = unlocked;
    }

    private void RefreshHeaderOnly()
    {
        if (service == null)
            return;

        AutoAcquisitionWingSnapshot snapshot = service.GetSnapshot(false);
        RefreshHeader(snapshot);
        RefreshReports(snapshot);

        if (intakeCapacityText != null)
        {
            intakeCapacityText.text =
                $"INTAKE VAULT: {snapshot.intakeCount:N0} / " +
                $"{snapshot.intakeCapacity:N0}";
        }
    }

    private void RefreshHeader(AutoAcquisitionWingSnapshot snapshot)
    {
        if (titleText != null)
        {
            titleText.text = service != null && service.Catalog != null
                ? service.Catalog.wingDisplayName
                : "Automated Acquisitions Wing";
        }

        if (summaryText != null && snapshot != null)
        {
            summaryText.text =
                $"Archives {snapshot.ownedCategoryCount:N0} • " +
                $"Research {snapshot.researchedContainerCount:N0} • " +
                $"Lines {snapshot.lineCount:N0} • " +
                $"Intake {snapshot.intakeCount:N0}/{snapshot.intakeCapacity:N0}";
        }

        if (calibrationText != null && snapshot != null)
        {
            calibrationText.text =
                $"MACHINE CALIBRATION: {snapshot.calibrationMultiplier:P0} " +
                $"OF MANUAL ODDS";
        }

        if (processingRulesText != null && snapshot != null)
        {
            processingRulesText.text =
                $"Base processing: {snapshot.processingSeconds:N0}s/item • " +
                $"Budget cap: {snapshot.maximumBudgetPerLine:N0} Gold/line • " +
                $"Offline shift: {snapshot.offlineShiftHours:N0}h • " +
                $"Curator Alert level: {snapshot.curatorAlertLevel:N0}";
        }

        if (intakeCapacityText != null && snapshot != null)
        {
            intakeCapacityText.text =
                $"INTAKE VAULT: {snapshot.intakeCount:N0} / " +
                $"{snapshot.intakeCapacity:N0}";
        }

        if (claimAllButton != null && snapshot != null)
            claimAllButton.interactable = snapshot.intakeCount > 0;
    }

    private void RefreshReports(AutoAcquisitionWingSnapshot snapshot)
    {
        if (reportsText == null || snapshot == null)
            return;

        string bestName = "None";

        if (service != null &&
            service.Database != null &&
            !string.IsNullOrWhiteSpace(snapshot.bestPullSkinApiId))
        {
            SkinData best = service.Database.GetSkinByApiId(
                snapshot.bestPullSkinApiId);

            if (best != null)
                bestName = SkinDisplayUtility.GetDisplayName(best);
        }

        double netValue =
            snapshot.lifetimeValueReceived - snapshot.lifetimeGoldSpent;

        reportsText.text =
            $"LIFETIME PROCESSED\n{snapshot.lifetimeItemsProcessed:N0}\n\n" +
            $"GOLD SPENT\n{snapshot.lifetimeGoldSpent:N2}\n\n" +
            $"VALUE RECEIVED\n{snapshot.lifetimeValueReceived:N2}\n\n" +
            $"VALUE DIFFERENCE\n{netValue:N2}\n\n" +
            $"BEST PULL\n{bestName}\n{snapshot.bestPullMarketValue:N2} Gold";
    }

    private void EnsureSelectedCategory()
    {
        if (service == null || service.Catalog == null)
            return;

        if (!string.IsNullOrWhiteSpace(selectedCategoryId) &&
            service.Catalog.GetCategory(selectedCategoryId) != null)
        {
            return;
        }

        IReadOnlyList<AutoAcquisitionCategoryData> categories =
            service.GetCategories();

        if (categories.Count > 0 && categories[0] != null)
            selectedCategoryId = categories[0].categoryId;
    }

    private void RebuildCategoryCards()
    {
        ClearCards(spawnedCategoryCards);

        if (service == null ||
            categoryContent == null ||
            categoryCardPrefab == null)
        {
            return;
        }

        IReadOnlyList<AutoAcquisitionCategoryData> categories =
            service.GetCategories();

        for (int i = 0; i < categories.Count; i++)
        {
            AutoAcquisitionCategoryData category = categories[i];

            if (category == null)
                continue;

            AutoAcquisitionCategoryCardUI card = Instantiate(
                categoryCardPrefab,
                categoryContent);
            card.gameObject.SetActive(true);
            card.Setup(category, this, service);
            spawnedCategoryCards.Add(card.gameObject);
        }
    }

    private void RebuildContainerCards()
    {
        ClearCards(spawnedContainerCards);

        if (service == null ||
            containerContent == null ||
            containerCardPrefab == null ||
            string.IsNullOrWhiteSpace(selectedCategoryId))
        {
            return;
        }

        AutoAcquisitionCategoryData category =
            service.Catalog != null
                ? service.Catalog.GetCategory(selectedCategoryId)
                : null;

        if (selectedArchiveText != null)
        {
            selectedArchiveText.text = category != null
                ? category.DisplayName
                : "ARCHIVE";
        }

        List<AutoAcquisitionContainerData> entries =
            service.GetContainers(selectedCategoryId);

        for (int i = 0; i < entries.Count; i++)
        {
            AutoAcquisitionContainerData entry = entries[i];

            if (entry == null)
                continue;

            AutoAcquisitionContainerCardUI card = Instantiate(
                containerCardPrefab,
                containerContent);
            card.gameObject.SetActive(true);
            card.Setup(entry, this, service);
            spawnedContainerCards.Add(card.gameObject);
        }
    }

    private void RebuildLineCards(int lineCount)
    {
        ClearCards(spawnedLineCards);
        liveLineCards.Clear();

        if (lineContent == null || lineCardPrefab == null || service == null)
            return;

        for (int i = 0; i < Mathf.Clamp(lineCount, 1, 3); i++)
        {
            AutoAcquisitionLineCardUI card = Instantiate(
                lineCardPrefab,
                lineContent);
            card.gameObject.SetActive(true);
            card.Setup(i, this, service);
            spawnedLineCards.Add(card.gameObject);
            liveLineCards.Add(card);
        }
    }

    private void RebuildIntakeCards()
    {
        ClearCards(spawnedIntakeCards);

        if (service == null || intakeContent == null || intakeItemCardPrefab == null)
            return;

        IReadOnlyList<AutoAcquisitionPendingItemSaveData> pending =
            service.GetIntakeItems(false);

        if (intakeEmptyRoot != null)
            intakeEmptyRoot.SetActive(pending.Count == 0);

        for (int i = pending.Count - 1; i >= 0; i--)
        {
            AutoAcquisitionPendingItemSaveData item = pending[i];

            if (item == null)
                continue;

            AutoAcquisitionIntakeItemCardUI card = Instantiate(
                intakeItemCardPrefab,
                intakeContent);
            card.gameObject.SetActive(true);
            card.Setup(item, this, service);
            spawnedIntakeCards.Add(card.gameObject);
        }
    }

    private void ApplyPage()
    {
        SetView(
            receivingDockView,
            currentPage == AutomatedAcquisitionsPage.ReceivingDock);
        SetView(
            processingFloorView,
            currentPage == AutomatedAcquisitionsPage.ProcessingFloor);
        SetView(
            intakeVaultView,
            currentPage == AutomatedAcquisitionsPage.IntakeVault);
        SetView(
            curatorReportsView,
            currentPage == AutomatedAcquisitionsPage.CuratorReports);
    }

    private void ResolveService()
    {
        if (service == null)
            service = AutoAcquisitionService.GetOrCreate();
    }

    private void Subscribe()
    {
        if (service == null || subscribed)
            return;

        service.OnStateChanged += HandleServiceChanged;
        service.OnItemProcessed += HandleItemProcessed;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (service != null && subscribed)
        {
            service.OnStateChanged -= HandleServiceChanged;
            service.OnItemProcessed -= HandleItemProcessed;
        }

        subscribed = false;
    }

    private void HandleServiceChanged()
    {
        RefreshAll();
    }

    private void HandleItemProcessed(AutoAcquisitionPendingItemSaveData item)
    {
        RefreshAll();

        if (item != null && item.exceptional)
            ShowStatus(item.alertReason, false);
    }

    private void ClearAllCards()
    {
        ClearCards(spawnedCategoryCards);
        ClearCards(spawnedContainerCards);
        ClearCards(spawnedLineCards);
        ClearCards(spawnedIntakeCards);
        liveLineCards.Clear();
    }

    private static void ClearCards(List<GameObject> cards)
    {
        if (cards == null)
            return;

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null)
                Destroy(cards[i]);
        }

        cards.Clear();
    }

    private static void SetView(GameObject view, bool active)
    {
        if (view != null)
            view.SetActive(active);
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
