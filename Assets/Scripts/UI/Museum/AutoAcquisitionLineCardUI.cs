using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AutoAcquisitionLineCardUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text budgetText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Dropdown containerDropdown;
    [SerializeField] private TMP_InputField depositInput;
    [SerializeField] private Button depositButton;
    [SerializeField] private Button withdrawButton;
    [SerializeField] private Button startStopButton;
    [SerializeField] private TMP_Text startStopButtonText;
    [SerializeField] private Button acknowledgeAlertButton;

    private readonly List<AutoAcquisitionContainerData> dropdownEntries =
        new List<AutoAcquisitionContainerData>();

    private int lineIndex;
    private AutomatedAcquisitionsPanelUI owner;
    private AutoAcquisitionService service;
    private bool suppressDropdown;

    public void Setup(
        int index,
        AutomatedAcquisitionsPanelUI panel,
        AutoAcquisitionService acquisitionService)
    {
        lineIndex = index;
        owner = panel;
        service = acquisitionService;

        SetupButton(depositButton, HandleDeposit);
        SetupButton(withdrawButton, HandleWithdraw);
        SetupButton(startStopButton, HandleStartStop);
        SetupButton(acknowledgeAlertButton, HandleAcknowledge);

        if (containerDropdown != null)
        {
            containerDropdown.onValueChanged.RemoveAllListeners();
            containerDropdown.onValueChanged.AddListener(HandleContainerChanged);
        }

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

        RebuildDropdown(line.selectedContainerId);
    }

    private void RebuildDropdown(string selectedContainerId)
    {
        if (containerDropdown == null || service == null || service.Catalog == null)
            return;

        dropdownEntries.Clear();
        List<TMP_Dropdown.OptionData> options =
            new List<TMP_Dropdown.OptionData>();

        if (service.Catalog.containers != null)
        {
            for (int i = 0; i < service.Catalog.containers.Count; i++)
            {
                AutoAcquisitionContainerData entry =
                    service.Catalog.containers[i];

                if (entry == null ||
                    entry.container == null ||
                    !service.IsContainerResearched(entry.containerId))
                {
                    continue;
                }

                dropdownEntries.Add(entry);
                options.Add(new TMP_Dropdown.OptionData(entry.ContainerName));
            }
        }

        suppressDropdown = true;
        containerDropdown.ClearOptions();
        containerDropdown.AddOptions(options);

        int selectedIndex = 0;

        for (int i = 0; i < dropdownEntries.Count; i++)
        {
            if (string.Equals(
                    dropdownEntries[i].containerId,
                    selectedContainerId,
                    StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = i;
                break;
            }
        }

        containerDropdown.value = Mathf.Clamp(
            selectedIndex,
            0,
            Mathf.Max(0, dropdownEntries.Count - 1));
        containerDropdown.RefreshShownValue();
        containerDropdown.interactable = dropdownEntries.Count > 0;
        suppressDropdown = false;
    }

    private void HandleContainerChanged(int index)
    {
        if (suppressDropdown ||
            service == null ||
            index < 0 ||
            index >= dropdownEntries.Count)
        {
            return;
        }

        AutoAcquisitionActionResult result = service.SelectLineTarget(
            lineIndex,
            dropdownEntries[index].containerId);

        if (owner != null)
            owner.HandleActionResult(result);
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
