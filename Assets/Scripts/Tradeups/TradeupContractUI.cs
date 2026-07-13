using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TradeupContractUI : MonoBehaviour
{
    [Header("Views")]
    [SerializeField] private GameObject selectionView;
    [SerializeField] private GameObject contractView;

    [Header("Contract Text")]
    [Tooltip("Assign exactly 10 TMP text fields in numerical order.")]
    [SerializeField] private TMP_Text[] inputNameTexts =
        new TMP_Text[10];

    [SerializeField] private TMP_Text receivedGoodText;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text amountText;
    [SerializeField] private TMP_Text dateText;
    [SerializeField] private TMP_Text statusText;

    [Header("Contract Controls")]
    [SerializeField] private Button signContractButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button clearSignatureButton;

    [Header("Result Presentation")]
    [SerializeField] private GameObject approvedStamp;

    [Header("Typewriter")]
    [SerializeField, Min(0f)]
    private float characterDelay = 0.025f;

    [SerializeField, Min(0f)]
    private float delayBetweenInputNames = 0.08f;

    [SerializeField, Min(0f)]
    private float delayBeforeOutputName = 0.35f;

    [SerializeField]
    private bool useUnscaledTime = true;

    [Header("Events")]
    public UnityEvent onContractOpened;
    public UnityEvent onTradeupStarted;
    public UnityEvent onTradeupCompleted;
    public UnityEvent onContractClosed;
    public UnityEvent onClearSignatureRequested;

    private readonly List<InventoryItem> selectedInputs =
        new List<InventoryItem>();

    private Coroutine typewriterCoroutine;
    private TradeupExecutionResult completedResult;

    private bool isSigning;
    private bool sequenceCompleted;

    public bool IsSigning => isSigning;
    public bool SequenceCompleted => sequenceCompleted;

    public InventoryItem OutputItem =>
        completedResult != null
            ? completedResult.outputItem
            : null;

    private void Awake()
    {
        SetupButtons();

        if (approvedStamp != null)
            approvedStamp.SetActive(false);

        ClearGeneratedText();
    }

    private void OnDisable()
    {
        StopTypewriter();
    }

    private void SetupButtons()
    {
        if (signContractButton != null)
        {
            signContractButton.onClick.RemoveListener(
                SignContract);

            signContractButton.onClick.AddListener(
                SignContract);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(
                BackToSelection);

            backButton.onClick.AddListener(
                BackToSelection);
        }

        if (clearSignatureButton != null)
        {
            clearSignatureButton.onClick.RemoveListener(
                RequestClearSignature);

            clearSignatureButton.onClick.AddListener(
                RequestClearSignature);
        }
    }

    /// <summary>
    /// Called by the tradeup selection UI after the player has selected
    /// either 10 standard inputs or 5 Covert inputs.
    /// </summary>
    public bool OpenContract(List<InventoryItem> inputs)
    {
        if (inputs == null || inputs.Count == 0)
        {
            Debug.LogWarning(
                "TradeupContractUI: No inputs were supplied.");

            return false;
        }

        if (TradeupResolver.Instance == null)
        {
            Debug.LogError(
                "TradeupContractUI: TradeupResolver is missing.");

            return false;
        }

        TradeupPreview preview =
            TradeupResolver.Instance.BuildPreview(inputs);

        if (preview == null ||
            preview.validation == null ||
            !preview.validation.isValid)
        {
            string reason =
                preview != null &&
                preview.validation != null
                    ? preview.validation.errorMessage
                    : "Unknown tradeup validation error.";

            Debug.LogWarning(
                $"TradeupContractUI: Cannot open contract. {reason}");

            SetStatus(reason);
            return false;
        }

        StopTypewriter();

        selectedInputs.Clear();
        selectedInputs.AddRange(inputs);

        completedResult = null;
        isSigning = false;
        sequenceCompleted = false;

        ClearGeneratedText();
        SetStaticContractText(inputs.Count);

        if (approvedStamp != null)
            approvedStamp.SetActive(false);

        if (selectionView != null)
            selectionView.SetActive(false);

        if (contractView != null)
            contractView.SetActive(true);

        SetControlsBeforeSigning();

        SetStatus(
            preview.isCovertTradeup
                ? "Rare Special contract ready."
                : "Tradeup contract ready.");

        onContractOpened?.Invoke();
        return true;
    }

    private void SetStaticContractText(int inputCount)
    {
        if (rankText != null)
        {
            rankText.text =
                SaveManager.Instance != null
                    ? PlayerProgressUtility.GetRankDisplayName(
                        SaveManager.Instance.CurrentRank)
                    : "Unknown";
        }

        if (amountText != null)
            amountText.text = inputCount.ToString();

        if (dateText != null)
            dateText.text = System.DateTime.Now.ToString("dd/MM/yyyy");
    }

    /// <summary>
    /// Executes the actual tradeup, then types the consumed input names and
    /// received output name onto the contract.
    /// </summary>
    public void SignContract()
    {
        if (isSigning || sequenceCompleted)
            return;

        if (selectedInputs.Count == 0)
        {
            SetStatus("No tradeup inputs selected.");
            return;
        }

        if (TradeupResolver.Instance == null)
        {
            SetStatus("Tradeup Resolver is missing.");
            return;
        }

        isSigning = true;

        if (signContractButton != null)
            signContractButton.interactable = false;

        if (backButton != null)
            backButton.interactable = false;

        if (clearSignatureButton != null)
            clearSignatureButton.interactable = false;

        SetStatus("Processing contract...");
        onTradeupStarted?.Invoke();

        // Copy the list because the resolver removes these items from
        // InventoryManager after a successful tradeup.
        List<InventoryItem> inputsCopy =
            new List<InventoryItem>(selectedInputs);

        TradeupExecutionResult result =
            TradeupResolver.Instance.ExecuteTradeup(inputsCopy);

        if (result == null || !result.success)
        {
            isSigning = false;

            string reason =
                result != null &&
                !string.IsNullOrWhiteSpace(result.errorMessage)
                    ? result.errorMessage
                    : "Tradeup execution failed.";

            SetStatus(reason);
            SetControlsBeforeSigning();
            return;
        }

        completedResult = result;

        StopTypewriter();

        typewriterCoroutine =
            StartCoroutine(
                PlayContractSequence(
                    inputsCopy,
                    result.outputItem));
    }

    private IEnumerator PlayContractSequence(
        List<InventoryItem> consumedInputs,
        InventoryItem outputItem)
    {
        ClearInputNameTexts();

        if (receivedGoodText != null)
            receivedGoodText.text = "";

        int displayedInputCount = Mathf.Min(
            consumedInputs.Count,
            inputNameTexts != null
                ? inputNameTexts.Length
                : 0);

        for (int i = 0; i < displayedInputCount; i++)
        {
            TMP_Text targetText = inputNameTexts[i];

            if (targetText == null)
                continue;

            string inputName =
                GetContractItemName(consumedInputs[i]);

            yield return TypeText(
                targetText,
                inputName);

            if (delayBetweenInputNames > 0f)
                yield return Wait(delayBetweenInputNames);
        }

        if (delayBeforeOutputName > 0f)
            yield return Wait(delayBeforeOutputName);

        if (receivedGoodText != null)
        {
            string outputName =
                GetContractItemName(outputItem);

            yield return TypeText(
                receivedGoodText,
                outputName);
        }

        if (approvedStamp != null)
            approvedStamp.SetActive(true);

        isSigning = false;
        sequenceCompleted = true;
        typewriterCoroutine = null;

        if (backButton != null)
            backButton.interactable = true;

        SetStatus("CONTRACT APPROVED");

        onTradeupCompleted?.Invoke();
    }

    private IEnumerator TypeText(
        TMP_Text targetText,
        string fullText)
    {
        if (targetText == null)
            yield break;

        if (fullText == null)
            fullText = "";

        targetText.text = "";

        for (int characterIndex = 0;
             characterIndex < fullText.Length;
             characterIndex++)
        {
            targetText.text =
                fullText.Substring(
                    0,
                    characterIndex + 1);

            if (characterDelay > 0f)
                yield return Wait(characterDelay);
        }

        targetText.text = fullText;
    }

    private IEnumerator Wait(float duration)
    {
        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(duration);
        else
            yield return new WaitForSeconds(duration);
    }

    private string GetContractItemName(
        InventoryItem item)
    {
        if (item == null || item.skin == null)
            return "UNKNOWN ITEM";

        string itemName =
            SkinDisplayUtility.GetDisplayName(item.skin);

        if (item.statTrak)
            itemName = "StatTrak " + itemName;

        return itemName;
    }

    public void BackToSelection()
    {
        if (isSigning)
            return;

        StopTypewriter();

        selectedInputs.Clear();
        completedResult = null;
        sequenceCompleted = false;

        ClearGeneratedText();

        if (approvedStamp != null)
            approvedStamp.SetActive(false);

        if (contractView != null)
            contractView.SetActive(false);

        if (selectionView != null)
            selectionView.SetActive(true);

        onContractClosed?.Invoke();
    }

    public void RequestClearSignature()
    {
        if (isSigning)
            return;

        onClearSignatureRequested?.Invoke();
    }

    private void SetControlsBeforeSigning()
    {
        if (signContractButton != null)
            signContractButton.interactable = true;

        if (backButton != null)
            backButton.interactable = true;

        if (clearSignatureButton != null)
            clearSignatureButton.interactable = true;
    }

    private void ClearGeneratedText()
    {
        ClearInputNameTexts();

        if (receivedGoodText != null)
            receivedGoodText.text = "";

        if (rankText != null)
            rankText.text = "";

        if (amountText != null)
            amountText.text = "";

        if (dateText != null)
            dateText.text = "";

        if (statusText != null)
            statusText.text = "";
    }

    private void ClearInputNameTexts()
    {
        if (inputNameTexts == null)
            return;

        for (int i = 0; i < inputNameTexts.Length; i++)
        {
            if (inputNameTexts[i] != null)
                inputNameTexts[i].text = "";
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message ?? "";
    }

    private void StopTypewriter()
    {
        if (typewriterCoroutine == null)
            return;

        StopCoroutine(typewriterCoroutine);
        typewriterCoroutine = null;
    }
}