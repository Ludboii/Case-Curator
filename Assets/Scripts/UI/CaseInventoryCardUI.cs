using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CaseInventoryCardUI : MonoBehaviour
{
    [Header("Images")]
    public Image caseImage;
    public Image backgroundImage;
    public Image openButtonImage;

    [Header("Text")]
    public TMP_Text caseNameText;
    public TMP_Text ownedAmountText;
    public TMP_Text openButtonText;
    public TMP_Text warningText;

    [Header("Buttons")]
    public Button openButton;
    public Button inspectButton;

    [Header("Button Colors")]
    public Color availableOpenColor = new Color(0.2f, 0.85f, 0.35f, 1f);
    public Color unavailableOpenColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Header("Background")]
    [Range(0f, 1f)] public float backgroundAlpha = 0.85f;
    [Range(0f, 1f)] public float backgroundDarkenAmount = 0.25f;

    private CaseInventoryEntry currentEntry;
    private CaseInventoryUI ownerUI;

    private void Awake()
    {
        DisableNonInteractiveRaycasts();

        if (openButton != null)
        {
            openButton.onClick.RemoveAllListeners();
            openButton.onClick.AddListener(OpenCases);
        }

        if (inspectButton != null)
        {
            inspectButton.onClick.RemoveAllListeners();
            inspectButton.onClick.AddListener(OpenInspect);
        }
    }

    public void Setup(CaseInventoryEntry entry, CaseInventoryUI owner)
    {
        currentEntry = entry;
        ownerUI = owner;

        if (currentEntry == null || currentEntry.caseData == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        CaseData caseData = currentEntry.caseData;

        if (caseImage != null)
        {
            caseImage.sprite = caseData.icon;
            caseImage.enabled = caseData.icon != null;
            caseImage.preserveAspect = true;
        }

        if (caseNameText != null)
            caseNameText.text = caseData.caseName;

        ApplyCaseQualityBackground(caseData);
        RefreshState();
    }

    public void RefreshState()
    {
        if (currentEntry == null ||
            currentEntry.caseData == null ||
            ownerUI == null)
        {
            return;
        }

        int owned = currentEntry.amount;
        int requestedAmount = ownerUI.GetRequestedOpenAmount(currentEntry);
        bool canOpen = ownerUI.CanOpenCases(
            currentEntry,
            out string failReason);

        if (ownedAmountText != null)
            ownedAmountText.text = $"Owned: {owned}";

        if (openButtonText != null)
        {
            if (requestedAmount <= 0)
                openButtonText.text = "OPEN\nx0";
            else if (ownerUI.IsMaxAmountSelected)
                openButtonText.text = $"OPEN MAX\nx{requestedAmount}";
            else
                openButtonText.text = $"OPEN\nx{requestedAmount}";

            openButtonText.color = Color.white;
        }

        if (openButton != null)
            openButton.interactable = canOpen;

        ApplyOpenButtonColor(canOpen);

        if (warningText != null)
        {
            bool showWarning =
                !canOpen && !string.IsNullOrWhiteSpace(failReason);

            warningText.gameObject.SetActive(showWarning);
            warningText.text = showWarning ? failReason : "";
        }
    }

    private void OpenCases()
    {
        if (ownerUI != null && currentEntry != null)
            ownerUI.TryOpenCases(currentEntry);
    }

    private void OpenInspect()
    {
        if (ownerUI == null ||
            currentEntry == null ||
            currentEntry.caseData == null)
        {
            return;
        }

        ownerUI.OpenCaseInspect(currentEntry.caseData);
    }

    private void ApplyOpenButtonColor(bool canOpen)
    {
        Color targetColor = canOpen
            ? availableOpenColor
            : unavailableOpenColor;

        if (openButtonImage != null)
            openButtonImage.color = targetColor;

        if (openButton == null)
            return;

        ColorBlock colors = openButton.colors;
        colors.normalColor = targetColor;
        colors.highlightedColor = targetColor;
        colors.pressedColor = targetColor * 0.9f;
        colors.selectedColor = targetColor;
        colors.disabledColor = targetColor;
        colors.colorMultiplier = 1f;
        openButton.colors = colors;
    }

    private void ApplyCaseQualityBackground(CaseData caseData)
    {
        if (backgroundImage == null || caseData == null)
            return;

        Color qualityColor = CaseQualityUtility.GetColor(caseData.quality);
        float multiplier = 1f - Mathf.Clamp01(backgroundDarkenAmount);

        Color backgroundColor = new Color(
            qualityColor.r * multiplier,
            qualityColor.g * multiplier,
            qualityColor.b * multiplier,
            backgroundAlpha);

        backgroundImage.color = backgroundColor;
    }

    private void DisableNonInteractiveRaycasts()
    {
        Graphic openTarget = openButton != null
            ? openButton.targetGraphic
            : null;

        Graphic inspectTarget = inspectButton != null
            ? inspectButton.targetGraphic
            : null;

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];

            if (graphic == null ||
                graphic == openTarget ||
                graphic == inspectTarget)
            {
                continue;
            }

            graphic.raycastTarget = false;
        }
    }
}
