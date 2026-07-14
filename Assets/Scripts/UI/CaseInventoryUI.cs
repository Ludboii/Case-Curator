using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CaseInventoryUI : MonoBehaviour
{
    [Header("Card Spawning")]
    public Transform caseGridContent;
    public CaseInventoryCardUI caseCardPrefab;

    [Header("Text")]
    public TMP_Text caseCountText;

    [Header("Open Amount Buttons")]
    public Button open1Button;
    public Button open5Button;
    public Button open10Button;
    public Button open50Button;
    public Button openMaxButton;

    [Header("Open Amount Button Colors")]
    public Color selectedAmountColor = new Color(0.2f, 0.85f, 0.35f, 1f);
    public Color normalAmountColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    private readonly List<CaseInventoryCardUI> cardPool =
        new List<CaseInventoryCardUI>();

    private int activeCardCount;
    private int selectedOpenAmount = 1;
    private bool maxAmountSelected;

    public bool IsMaxAmountSelected => maxAmountSelected;

    private void Awake()
    {
        SetupAmountButtons();
    }

    private void OnEnable()
    {
        SubscribeToEvents();
        Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (CaseInventoryManager.Instance != null)
        {
            CaseInventoryManager.Instance.OnCaseInventoryChanged -= Refresh;
            CaseInventoryManager.Instance.OnCaseInventoryChanged += Refresh;
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= RefreshCards;
            InventoryManager.Instance.OnInventoryChanged += RefreshCards;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (CaseInventoryManager.Instance != null)
            CaseInventoryManager.Instance.OnCaseInventoryChanged -= Refresh;

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshCards;
    }

    private void SetupAmountButtons()
    {
        SetupButton(open1Button, SetOpenAmount1);
        SetupButton(open5Button, SetOpenAmount5);
        SetupButton(open10Button, SetOpenAmount10);
        SetupButton(open50Button, SetOpenAmount50);
        SetupButton(openMaxButton, SetOpenAmountMax);
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

    public void Refresh()
    {
        if (CaseInventoryManager.Instance == null)
        {
            UpdateCaseCountText();
            RefreshAmountButtons();
            SetActiveCardCount(0);
            return;
        }

        IReadOnlyList<CaseInventoryEntry> entries =
            CaseInventoryManager.Instance.Cases;

        int validEntryCount = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            CaseInventoryEntry entry = entries[i];

            if (entry == null || entry.caseData == null || entry.amount <= 0)
                continue;

            EnsurePoolSize(validEntryCount + 1);

            CaseInventoryCardUI card = cardPool[validEntryCount];
            card.gameObject.SetActive(true);
            card.Setup(entry, this);
            validEntryCount++;
        }

        SetActiveCardCount(validEntryCount);
        UpdateCaseCountText();
        RefreshAmountButtons();
        RefreshCards();
    }

    private void EnsurePoolSize(int requiredCount)
    {
        while (cardPool.Count < requiredCount)
        {
            CaseInventoryCardUI card =
                Instantiate(caseCardPrefab, caseGridContent);

            card.gameObject.SetActive(false);
            cardPool.Add(card);
        }
    }

    private void SetActiveCardCount(int count)
    {
        activeCardCount = Mathf.Clamp(count, 0, cardPool.Count);

        for (int i = activeCardCount; i < cardPool.Count; i++)
        {
            if (cardPool[i] != null)
                cardPool[i].gameObject.SetActive(false);
        }
    }

    public void RefreshCards()
    {
        for (int i = 0; i < activeCardCount; i++)
        {
            CaseInventoryCardUI card = cardPool[i];

            if (card != null)
                card.RefreshState();
        }

        UpdateCaseCountText();
        RefreshAmountButtons();
    }

    private void UpdateCaseCountText()
    {
        if (caseCountText == null)
            return;

        int totalCases = CaseInventoryManager.Instance != null
            ? CaseInventoryManager.Instance.TotalCaseCount
            : 0;

        caseCountText.text = $"Cases: {totalCases}";
    }

    public int GetRequestedOpenAmount(CaseInventoryEntry entry)
    {
        if (entry == null)
            return 0;

        return maxAmountSelected
            ? GetMaxOpenAmount(entry)
            : Mathf.Min(selectedOpenAmount, entry.amount);
    }

    public int GetMaxOpenAmount(CaseInventoryEntry entry)
    {
        if (entry == null || InventoryManager.Instance == null)
            return 0;

        int availableSkinSpace =
            InventoryManager.Instance.TotalCapacity -
            InventoryManager.Instance.Count;

        return Mathf.Clamp(entry.amount, 0, availableSkinSpace);
    }

    public bool CanOpenCases(
        CaseInventoryEntry entry,
        out string failReason)
    {
        failReason = "";

        if (entry == null || entry.caseData == null)
        {
            failReason = "Missing case";
            return false;
        }

        if (CaseInventoryManager.Instance == null)
        {
            failReason = "No case inventory";
            return false;
        }

        if (InventoryManager.Instance == null)
        {
            failReason = "No skin inventory";
            return false;
        }

        if (SaveManager.Instance == null)
        {
            failReason = "No save";
            return false;
        }

        int amount = GetRequestedOpenAmount(entry);

        if (amount <= 0)
        {
            failReason = "No space";
            return false;
        }

        if (entry.amount < amount)
        {
            failReason = "Not enough cases";
            return false;
        }

        int availableSkinSpace =
            InventoryManager.Instance.TotalCapacity -
            InventoryManager.Instance.Count;

        if (availableSkinSpace < amount)
        {
            failReason = "Skin inventory full";
            return false;
        }

        return true;
    }

    public void TryOpenCases(CaseInventoryEntry entry)
    {
        if (!CanOpenCases(entry, out string failReason))
        {
            Debug.LogWarning(
                $"CaseInventoryUI: Cannot open case. Reason: {failReason}");
            RefreshCards();
            return;
        }

        int amount = GetRequestedOpenAmount(entry);

        if (amount <= 0)
            return;

        CaseData caseData = entry.caseData;

        if (!CaseInventoryManager.Instance.RemoveCases(caseData, amount))
        {
            Debug.LogWarning(
                $"CaseInventoryUI: Failed to remove {amount}x " +
                $"{caseData.caseName} before opening.");
            RefreshCards();
            return;
        }

        List<InventoryItem> openedItems =
            new List<InventoryItem>(amount);

        for (int i = 0; i < amount; i++)
        {
            InventoryItem item = CaseOpener.OpenCase(caseData);

            if (item != null)
                openedItems.Add(item);
        }

        int addedCount = InventoryManager.Instance.AddItems(openedItems);
        bool progressChanged = false;

        for (int i = 0; i < addedCount; i++)
        {
            if (ContainerProgressManager.Instance == null)
                continue;

            ContainerProgressManager.Instance.RecordContainerOpened(
                caseData,
                openedItems[i],
                caseData.priceInGold,
                false);

            progressChanged = true;
        }

        if (progressChanged)
            ContainerProgressManager.Instance.SaveProgress();

        int totalXPGained =
            Mathf.Max(0, caseData.xpRewardOnOpen) * addedCount;

        if (totalXPGained > 0)
            SaveManager.Instance.AddXP(totalXPGained);

        SaveManager.Instance.SaveGame();

        Debug.Log(
            $"Opened {addedCount}x {caseData.caseName}. " +
            $"Gained {totalXPGained} XP.");

        // Both inventory managers already raised their change events. Only the
        // lightweight card state needs a final refresh here.
        RefreshCards();
    }

    public void OpenCaseInspect(CaseData caseData)
    {
        if (caseData == null)
            return;

        if (CaseInspectUI.Instance == null)
        {
            Debug.LogWarning(
                "CaseInventoryUI: No CaseInspectUI found in scene.");
            return;
        }

        CaseInspectUI.Instance.Open(caseData);
    }

    private void RefreshAmountButtons()
    {
        ApplyAmountButtonVisual(
            open1Button,
            !maxAmountSelected && selectedOpenAmount == 1);
        ApplyAmountButtonVisual(
            open5Button,
            !maxAmountSelected && selectedOpenAmount == 5);
        ApplyAmountButtonVisual(
            open10Button,
            !maxAmountSelected && selectedOpenAmount == 10);
        ApplyAmountButtonVisual(
            open50Button,
            !maxAmountSelected && selectedOpenAmount == 50);
        ApplyAmountButtonVisual(openMaxButton, maxAmountSelected);
    }

    private static void ApplyAmountButtonVisual(
        Button button,
        bool selected,
        Color selectedColor,
        Color normalColor)
    {
        if (button == null)
            return;

        Color targetColor = selected ? selectedColor : normalColor;
        ColorBlock colors = button.colors;
        colors.normalColor = targetColor;
        colors.highlightedColor = targetColor;
        colors.pressedColor = targetColor * 0.9f;
        colors.selectedColor = targetColor;
        colors.disabledColor = targetColor;
        colors.colorMultiplier = 1f;
        button.colors = colors;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>();

        if (text != null)
        {
            text.color = Color.white;
            text.raycastTarget = false;
        }
    }

    private void ApplyAmountButtonVisual(Button button, bool selected)
    {
        ApplyAmountButtonVisual(
            button,
            selected,
            selectedAmountColor,
            normalAmountColor);
    }

    public void SetOpenAmount1()
    {
        SetOpenAmount(1, false);
    }

    public void SetOpenAmount5()
    {
        SetOpenAmount(5, false);
    }

    public void SetOpenAmount10()
    {
        SetOpenAmount(10, false);
    }

    public void SetOpenAmount50()
    {
        SetOpenAmount(50, false);
    }

    public void SetOpenAmountMax()
    {
        SetOpenAmount(selectedOpenAmount, true);
    }

    private void SetOpenAmount(int amount, bool useMax)
    {
        selectedOpenAmount = Mathf.Max(1, amount);
        maxAmountSelected = useMax;
        RefreshAmountButtons();
        RefreshCards();
    }
}
