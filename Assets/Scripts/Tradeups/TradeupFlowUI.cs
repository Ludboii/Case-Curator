using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TradeupFlowUI : MonoBehaviour
{
    [Header("Main Navigation")]
    [SerializeField] private MainPanelController mainPanelController;

    [Header("Tradeup Views")]
    [SerializeField] private GameObject tradeupSelectionView;
    [SerializeField] private GameObject tradeupResultView;

    [Header("Buttons")]
    [SerializeField] private Button inventoryTradeupButton;
    [SerializeField] private Button reviewContractButton;
    [SerializeField] private Button resultContinueButton;
    [SerializeField] private Button resultBackToInventoryButton;

    [Header("Selection Information")]
    [SerializeField] private TMP_Text selectedCountText;
    [SerializeField] private TMP_Text validationText;

    [Header("Result Card")]
    [SerializeField] private Transform resultCardParent;
    [SerializeField] private InventoryItemCardUI inventoryItemCardPrefab;

    [Header("Tradeup Contract")]
    [SerializeField] private TradeupContractUI tradeupContractUI;

    [Header("Exit")]
    [SerializeField] private Button selectionExitButton;

    private readonly List<InventoryItem> selectedInputs =
        new List<InventoryItem>();

    private InventoryItemCardUI spawnedResultCard;

    public event Action<IReadOnlyList<InventoryItem>> OnSelectionChanged;

    public IReadOnlyList<InventoryItem> SelectedInputs =>
        selectedInputs;

    private void Awake()
    {
        SetupButtons();

        if (tradeupResultView != null)
            tradeupResultView.SetActive(false);

        RefreshSelectionState();
    }

    private void SetupButtons()
    {
        if (inventoryTradeupButton != null)
        {
            inventoryTradeupButton.onClick.RemoveListener(
                OpenTradeupSelection);

            inventoryTradeupButton.onClick.AddListener(
                OpenTradeupSelection);
        }

        if (reviewContractButton != null)
        {
            reviewContractButton.onClick.RemoveListener(
                ReviewContract);

            reviewContractButton.onClick.AddListener(
                ReviewContract);
        }

        if (resultContinueButton != null)
        {
            resultContinueButton.onClick.RemoveListener(
                ReturnToTradeupSelection);

            resultContinueButton.onClick.AddListener(
                ReturnToTradeupSelection);
        }

        if (selectionExitButton != null)
        {
            selectionExitButton.onClick.RemoveListener(ReturnToInventory);
            selectionExitButton.onClick.AddListener(ReturnToInventory);
        }

        if (resultBackToInventoryButton != null)
        {
            resultBackToInventoryButton.onClick.RemoveListener(
                ReturnToInventory);

            resultBackToInventoryButton.onClick.AddListener(
                ReturnToInventory);
        }
    }

    public void OpenTradeupSelection()
    {
        Debug.Log("TradeupFlowUI: OpenTradeupSelection called.");

        if (mainPanelController == null)
        {
            Debug.LogError(
                "TradeupFlowUI: MainPanelController is not assigned.");

            return;
        }

        if (mainPanelController.tradeupsPanel == null)
        {
            Debug.LogError(
                "TradeupFlowUI: Tradeups Panel is not assigned " +
                "on MainPanelController.");

            return;
        }

        ClearSelection();

        mainPanelController.ShowTradeups();

        if (tradeupSelectionView != null)
        {
            tradeupSelectionView.SetActive(true);
        }
        else
        {
            Debug.LogError(
                "TradeupFlowUI: Tradeup Selection View is not assigned.");
        }

        if (tradeupResultView != null)
            tradeupResultView.SetActive(false);

        ClearResultCard();
        RefreshSelectionState();

        Debug.Log(
            "TradeupFlowUI: Showing panel " +
            mainPanelController.tradeupsPanel.name);
    }

    public bool AddInput(InventoryItem item)
    {
        if (item == null || item.skin == null)
            return false;

        if (selectedInputs.Contains(item))
            return false;

        if (!IsBasicTradeupCandidate(item))
            return false;

        int requiredCount = GetRequiredInputCount(item);

        if (selectedInputs.Count >= requiredCount)
            return false;

        if (selectedInputs.Count > 0)
        {
            InventoryItem first = selectedInputs[0];

            if (first == null || first.skin == null)
                return false;

            if (item.skin.rarity != first.skin.rarity)
                return false;

            if (item.statTrak != first.statTrak)
                return false;
        }

        selectedInputs.Add(item);
        RefreshSelectionState();
        return true;
    }

    public bool RemoveInput(InventoryItem item)
    {
        if (item == null)
            return false;

        bool removed = selectedInputs.Remove(item);

        if (removed)
            RefreshSelectionState();

        return removed;
    }

    public void ToggleInput(InventoryItem item)
    {
        if (selectedInputs.Contains(item))
            RemoveInput(item);
        else
            AddInput(item);
    }

    public bool IsSelected(InventoryItem item)
    {
        return item != null &&
               selectedInputs.Contains(item);
    }

    public void ClearSelection()
    {
        selectedInputs.Clear();
        RefreshSelectionState();
    }

    public void ReviewContract()
    {
        if (TradeupResolver.Instance == null)
        {
            SetValidationText("Tradeup Resolver is missing.");
            return;
        }

        if (tradeupContractUI == null)
        {
            SetValidationText("Tradeup Contract UI is missing.");
            return;
        }

        List<InventoryItem> contractInputs =
            new List<InventoryItem>(selectedInputs);

        TradeupPreview preview =
            TradeupResolver.Instance.BuildPreview(contractInputs);

        if (preview == null ||
            preview.validation == null ||
            !preview.validation.isValid)
        {
            string reason =
                preview != null &&
                preview.validation != null
                    ? preview.validation.errorMessage
                    : "Invalid tradeup.";

            SetValidationText(reason);
            RefreshSelectionState();
            return;
        }

        bool opened =
            tradeupContractUI.OpenContract(contractInputs);

        if (!opened)
        {
            SetValidationText(
                "The tradeup contract could not be opened.");
        }
    }

    private void ShowTradeupResult(
        InventoryItem outputItem)
    {
        if (outputItem == null ||
            outputItem.skin == null)
        {
            SetValidationText(
                "Tradeup produced no output item.");
            ReturnToTradeupSelection();
            return;
        }

        ClearResultCard();

        if (tradeupSelectionView != null)
            tradeupSelectionView.SetActive(false);

        if (tradeupResultView != null)
            tradeupResultView.SetActive(true);

        if (inventoryItemCardPrefab == null ||
            resultCardParent == null)
        {
            Debug.LogError(
                "TradeupFlowUI: Result card prefab or " +
                "parent is not assigned.");

            return;
        }

        spawnedResultCard = Instantiate(
            inventoryItemCardPrefab,
            resultCardParent);

        spawnedResultCard.gameObject.SetActive(true);
        spawnedResultCard.Setup(outputItem);

        // Result screen is presentation-only for now.
        if (spawnedResultCard.button != null)
            spawnedResultCard.button.interactable = false;

        if (spawnedResultCard.selectedOverlay != null)
            spawnedResultCard.selectedOverlay.SetActive(false);
    }

    public void ReturnToTradeupSelection()
    {
        ClearResultCard();
        ClearSelection();

        if (tradeupResultView != null)
            tradeupResultView.SetActive(false);

        if (tradeupSelectionView != null)
            tradeupSelectionView.SetActive(true);

        RefreshSelectionState();
    }

    public void ReturnToInventory()
    {
        ClearResultCard();
        ClearSelection();

        if (tradeupResultView != null)
            tradeupResultView.SetActive(false);

        if (tradeupSelectionView != null)
            tradeupSelectionView.SetActive(false);

        if (mainPanelController != null)
            mainPanelController.ShowSkinInventory();
    }

    public void ShowResultFromContract()
    {
        if (tradeupContractUI == null)
            return;

        InventoryItem outputItem =
            tradeupContractUI.OutputItem;

        if (outputItem == null)
        {
            SetValidationText(
                "The contract has no tradeup output.");
            return;
        }

        selectedInputs.Clear();
        RefreshSelectionState();
        ShowTradeupResult(outputItem);
    }

    private void RefreshSelectionState()
    {
        int requiredCount = GetCurrentRequiredCount();

        if (selectedCountText != null)
        {
            selectedCountText.text =
                $"SELECTED: {selectedInputs.Count} / " +
                $"{requiredCount}";
        }

        bool valid = false;
        string message = "";

        if (selectedInputs.Count == 0)
        {
            message = "Select tradeup items.";
        }
        else if (TradeupResolver.Instance == null)
        {
            message = "Tradeup Resolver is missing.";
        }
        else
        {
            TradeupPreview preview =
                TradeupResolver.Instance.BuildPreview(
                    selectedInputs);

            if (preview != null &&
                preview.validation != null)
            {
                valid = preview.validation.isValid;
                message = valid
                    ? BuildReadyMessage(preview)
                    : preview.validation.errorMessage;
            }
        }

        if (reviewContractButton != null)
            reviewContractButton.interactable = valid;

        SetValidationText(message);
        OnSelectionChanged?.Invoke(selectedInputs);
    }

    private string BuildReadyMessage(
        TradeupPreview preview)
    {
        if (preview == null)
            return "Tradeup ready.";

        string variant =
            preview.isStatTrak
                ? "StatTrak "
                : "";

        return
            $"{variant}{preview.inputRarity} → " +
            $"{preview.outputRarity}";
    }

    private int GetCurrentRequiredCount()
    {
        if (selectedInputs.Count == 0 ||
            selectedInputs[0] == null ||
            selectedInputs[0].skin == null)
        {
            return 10;
        }

        return GetRequiredInputCount(
            selectedInputs[0]);
    }

    private int GetRequiredInputCount(
        InventoryItem item)
    {
        if (item != null &&
            item.skin != null &&
            item.skin.rarity == Rarity.Covert)
        {
            return 5;
        }

        return 10;
    }

    public bool IsBasicTradeupCandidate(
        InventoryItem item)
    {
        if (item == null || item.skin == null)
            return false;

        if (item.favorite)
            return false;

        if (item.souvenir)
            return false;

        if (item.isVanilla || item.skin.isVanilla)
            return false;

        if (item.skin.rarity == Rarity.RareSpecial)
            return false;

        if (item.skin.collectionData == null)
            return false;

        return true;
    }

    private void SetValidationText(
        string message)
    {
        if (validationText != null)
            validationText.text = message ?? "";
    }

    private void ClearResultCard()
    {
        if (spawnedResultCard == null)
            return;

        Destroy(spawnedResultCard.gameObject);
        spawnedResultCard = null;
    }
}
