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
        DisableTextRaycasts();
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

        gameObject.SetActive(true);
        DisableTextRaycasts();

        CaseData caseData = currentEntry.caseData;

        if (caseImage != null)
        {
            caseImage.sprite = caseData.icon;
            caseImage.enabled = caseData.icon != null;
            caseImage.preserveAspect = true;
            caseImage.raycastTarget = false;
        }

        if (caseNameText != null)
            caseNameText.text = caseData.caseName;

        ApplyCaseQualityBackground(caseData);

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

        RefreshState();
    }

    public void RefreshState()
    {
        if (currentEntry == null || currentEntry.caseData == null || ownerUI == null)
            return;

        int owned = currentEntry.amount;
        int requestedAmount = ownerUI.GetRequestedOpenAmount(currentEntry);

        bool canOpen = ownerUI.CanOpenCases(currentEntry, out string failReason);

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
            bool showWarning = !canOpen && !string.IsNullOrWhiteSpace(failReason);

            warningText.gameObject.SetActive(showWarning);
            warningText.text = failReason;
        }
    }

    private void OpenCases()
    {
        if (ownerUI == null || currentEntry == null)
            return;

        ownerUI.TryOpenCases(currentEntry);
    }

    private void OpenInspect()
    {
        if (ownerUI == null || currentEntry == null || currentEntry.caseData == null)
            return;

        ownerUI.OpenCaseInspect(currentEntry.caseData);
    }

    private void ApplyOpenButtonColor(bool canOpen)
    {
        Color targetColor = canOpen ? availableOpenColor : unavailableOpenColor;

        if (openButtonImage != null)
            openButtonImage.color = targetColor;

        if (openButton != null)
        {
            ColorBlock colors = openButton.colors;
            colors.normalColor = targetColor;
            colors.highlightedColor = targetColor;
            colors.pressedColor = targetColor * 0.9f;
            colors.selectedColor = targetColor;
            colors.disabledColor = targetColor;
            colors.colorMultiplier = 1f;

            openButton.colors = colors;
        }
    }

    private void ApplyCaseQualityBackground(CaseData caseData)
    {
        if (backgroundImage == null || caseData == null)
            return;

        Color qualityColor = CaseQualityUtility.GetColor(caseData.quality);
        Color backgroundColor = DarkenColor(qualityColor, backgroundDarkenAmount);
        backgroundColor.a = backgroundAlpha;

        backgroundImage.color = backgroundColor;
        backgroundImage.raycastTarget = false;
    }

    private Color DarkenColor(Color color, float amount)
    {
        amount = Mathf.Clamp01(amount);

        return new Color(
            color.r * (1f - amount),
            color.g * (1f - amount),
            color.b * (1f - amount),
            color.a);
    }

    private void DisableTextRaycasts()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text != null)
                text.raycastTarget = false;
        }
    }
}