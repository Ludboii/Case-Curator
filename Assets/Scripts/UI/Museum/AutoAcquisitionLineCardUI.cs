using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AutoAcquisitionLineCardUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text selectedContainerText;
    [SerializeField] private Button selectContainerButton;
    [SerializeField] private TMP_Text selectContainerButtonText;
    [SerializeField]
    private AutoAcquisitionContainerSelectionPopupUI selectionPopup;

    [Header("Legacy Dropdown — leave empty after migration")]
    [SerializeField] private TMP_Dropdown containerDropdown;

    [Header("Budget and Processing")]
    [SerializeField] private TMP_Text budgetText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_InputField depositInput;
    [SerializeField] private Button depositButton;
    [SerializeField] private Button withdrawButton;
    [SerializeField] private Button startStopButton;
    [SerializeField] private TMP_Text startStopButtonText;
    [SerializeField] private Button acknowledgeAlertButton;

    private int lineIndex;
    private AutomatedAcquisitionsPanelUI owner;
    private AutoAcquisitionService service;

    public void Setup(
        int index,
        AutomatedAcquisitionsPanelUI panel,
        AutoAcquisitionService acquisitionService)
    {
        lineIndex = index;
        owner = panel;
        service = acquisitionService;

        SetupButton(selectContainerButton, HandleOpenSelection);
        SetupButton(depositButton, HandleDeposit);
        SetupButton(withdrawButton, HandleWithdraw);
        SetupButton(startStopButton, HandleStartStop);
        SetupButton(acknowledgeAlertButton, HandleAcknowledge);

        if (containerDropdown != null)
            containerDropdown.gameObject.SetActive(false);

        RefreshState();
    }

    public void RefreshState()
    {
        AutoAcquisitionLineSaveData line = service != null
            ? service.GetLine(lineIndex)
            : null;

        if (line == null)
            return;

        if (titleText != null)
            titleText.text = $"PROCESSING LINE {lineIndex + 1}";

        AutoAcquisitionContainerData selected =
            service != null && service.Catalog != null
                ? service.Catalog.GetContainer(line.selectedContainerId)
                : null;

        if (selectedContainerText != null)
        {
            selectedContainerText.text = selected != null
                ? selected.ContainerName
                : "No container selected";
        }

        if (selectContainerButtonText != null)
        {
            selectContainerButtonText.text = selected != null
                ? "CHANGE CONTAINER"
                : "SELECT CONTAINER";
        }

        if (selectContainerButton != null)
            selectContainerButton.interactable = service != null && !line.active;

        float maximumBudget =
            AutoAcquisitionUpgradeUtility.GetMaximumBudgetPerLine();

        if (budgetText != null)
        {
            budgetText.text =
                $"Budget: {line.depositedGold:N2} / {maximumBudget:N0} Gold";
        }

        if (statusText != null)
        {
            statusText.text = !string.IsNullOrWhiteSpace(line.pauseReason)
                ? line.pauseReason
                : line.active ? "Processing." : "Stopped.";
        }

        if (timerText != null)
        {
            timerText.text = line.active && line.nextCompletionUtcTicks > 0
                ? "Next item: " + FormatRemaining(line.nextCompletionUtcTicks)
                : "Next item: --";
        }

        if (startStopButtonText != null)
            startStopButtonText.text = line.active ? "STOP" : "START";

        if (acknowledgeAlertButton != null)
        {
            acknowledgeAlertButton.gameObject.SetActive(
                line.pausedByCuratorAlert);
        }

        if (depositInput != null && string.IsNullOrWhiteSpace(depositInput.text))
            depositInput.text = "1000";
    }

    private void HandleOpenSelection()
    {
        if (selectionPopup == null)
        {
            if (owner != null)
            {
                owner.ShowStatus(
                    "Assign the Container Selection Popup on this line prefab.",
                    true);
            }
            return;
        }

        selectionPopup.Open(lineIndex, owner, service);
    }

    private void HandleDeposit()
    {
        if (service == null)
            return;

        string raw = depositInput != null ? depositInput.text : "0";

        if (!double.TryParse(
                raw,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double amount))
        {
            if (owner != null)
            {
                owner.ShowStatus(
                    "Enter a valid Gold amount using a decimal point.",
                    true);
            }
            return;
        }

        AutoAcquisitionActionResult result =
            service.DepositBudget(lineIndex, amount);

        if (owner != null)
            owner.HandleActionResult(result);
    }

    private void HandleWithdraw()
    {
        if (service == null)
            return;

        AutoAcquisitionActionResult result = service.WithdrawBudget(lineIndex);

        if (owner != null)
            owner.HandleActionResult(result);
    }

    private void HandleStartStop()
    {
        if (service == null)
            return;

        AutoAcquisitionLineSaveData line = service.GetLine(lineIndex);
        AutoAcquisitionActionResult result = service.SetLineActive(
            lineIndex,
            line == null || !line.active);

        if (owner != null)
            owner.HandleActionResult(result);
    }

    private void HandleAcknowledge()
    {
        if (service == null)
            return;

        AutoAcquisitionActionResult result =
            service.AcknowledgeCuratorAlert(lineIndex);

        if (owner != null)
            owner.HandleActionResult(result);
    }

    private static string FormatRemaining(long finishTicks)
    {
        long remaining = Math.Max(0L, finishTicks - DateTime.UtcNow.Ticks);
        TimeSpan time = TimeSpan.FromTicks(remaining);

        if (time.TotalHours >= 1d)
            return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";

        return $"{time.Minutes:00}:{time.Seconds:00}";
    }

    private static void SetupButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }
}
