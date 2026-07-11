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

    private readonly List<CaseInventoryCardUI> spawnedCards =
        new List<CaseInventoryCardUI>();

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

    private void Start()
    {
        Refresh();
        Invoke(nameof(Refresh), 0.1f);
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
        if (open1Button != null)
        {
            open1Button.onClick.RemoveAllListeners();
            open1Button.onClick.AddListener(SetOpenAmount1);
        }

        if (open5Button != null)
        {
            open5Button.onClick.RemoveAllListeners();
            open5Button.onClick.AddListener(SetOpenAmount5);
        }

        if (open10Button != null)
        {
            open10Button.onClick.RemoveAllListeners();
            open10Button.onClick.AddListener(SetOpenAmount10);
        }

        if (open50Button != null)
        {
            open50Button.onClick.RemoveAllListeners();
            open50Button.onClick.AddListener(SetOpenAmount50);
        }

        if (openMaxButton != null)
        {
            openMaxButton.onClick.RemoveAllListeners();
            openMaxButton.onClick.AddListener(SetOpenAmountMax);
        }
    }

    public void Refresh()
    {
        ClearCards();

        if (CaseInventoryManager.Instance == null)
        {
            Debug.LogWarning("CaseInventoryUI: No CaseInventoryManager found.");
            UpdateCaseCountText();
            RefreshAmountButtons();
            return;
        }

        IReadOnlyList<CaseInventoryEntry> entries =
            CaseInventoryManager.Instance.Cases;

        foreach (CaseInventoryEntry entry in entries)
        {
            if (entry == null || entry.caseData == null || entry.amount <= 0)
                continue;

            CaseInventoryCardUI card =
                Instantiate(caseCardPrefab, caseGridContent);

            card.gameObject.SetActive(true);
            card.Setup(entry, this);

            spawnedCards.Add(card);
        }

        UpdateCaseCountText();
        RefreshAmountButtons();
        RefreshCards();
    }

    private void ClearCards()
    {
        foreach (CaseInventoryCardUI card in spawnedCards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }

        spawnedCards.Clear();
    }

    public void RefreshCards()
    {
        foreach (CaseInventoryCardUI card in spawnedCards)
        {
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

        int totalCases = 0;

        if (CaseInventoryManager.Instance != null)
            totalCases = CaseInventoryManager.Instance.TotalCaseCount;

        caseCountText.text = $"Cases: {totalCases}";
    }

    public int GetRequestedOpenAmount(CaseInventoryEntry entry)
    {
        if (entry == null)
            return 0;

        if (maxAmountSelected)
            return GetMaxOpenAmount(entry);

        return Mathf.Min(selectedOpenAmount, entry.amount);
    }

    public int GetMaxOpenAmount(CaseInventoryEntry entry)
    {
        if (entry == null)
            return 0;

        if (InventoryManager.Instance == null)
            return 0;

        int availableSkinSpace =
            InventoryManager.Instance.TotalCapacity -
            InventoryManager.Instance.Count;

        return Mathf.Clamp(entry.amount, 0, availableSkinSpace);
    }

    public bool CanOpenCases(CaseInventoryEntry entry, out string failReason)
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
            Debug.LogWarning($"CaseInventoryUI: Cannot open case. Reason: {failReason}");
            RefreshCards();
            return;
        }

        int amount = GetRequestedOpenAmount(entry);

        if (amount <= 0)
            return;

        CaseData caseData = entry.caseData;

        int openedCount = 0;
        int totalXPGained = 0;

        for (int i = 0; i < amount; i++)
        {
            if (!InventoryManager.Instance.HasSpace())
                break;

            bool removed =
                CaseInventoryManager.Instance.RemoveCases(caseData, 1);

            if (!removed)
                break;

            InventoryItem item = CaseOpener.OpenCase(caseData);

            if (item == null)
            {
                Debug.LogWarning($"CaseInventoryUI: CaseOpener returned null for {caseData.caseName}.");
                continue;
            }

            InventoryManager.Instance.AddItem(item);

            if (ContainerProgressManager.Instance != null)
            {
                ContainerProgressManager.Instance.RecordContainerOpened(
                    caseData,
                    item.skin,
                    caseData.priceInGold,
                    item.marketValue
                );
            }

            if (caseData.xpRewardOnOpen > 0)
            {
                SaveManager.Instance.AddXP(caseData.xpRewardOnOpen);
                totalXPGained += caseData.xpRewardOnOpen;
            }

            openedCount++;
        }

        SaveManager.Instance.SaveGame();

        Debug.Log(
            $"Opened {openedCount}x {caseData.caseName}. " +
            $"Gained {totalXPGained} XP.");

        Refresh();
    }

    public void OpenCaseInspect(CaseData caseData)
    {
        if (caseData == null)
            return;

        if (CaseInspectUI.Instance == null)
        {
            Debug.LogWarning("CaseInventoryUI: No CaseInspectUI found in scene.");
            return;
        }

        CaseInspectUI.Instance.Open(caseData);
    }

    private void RefreshAmountButtons()
    {
        ApplyAmountButtonVisual(open1Button, !maxAmountSelected && selectedOpenAmount == 1);
        ApplyAmountButtonVisual(open5Button, !maxAmountSelected && selectedOpenAmount == 5);
        ApplyAmountButtonVisual(open10Button, !maxAmountSelected && selectedOpenAmount == 10);
        ApplyAmountButtonVisual(open50Button, !maxAmountSelected && selectedOpenAmount == 50);
        ApplyAmountButtonVisual(openMaxButton, maxAmountSelected);
    }

    private void ApplyAmountButtonVisual(Button button, bool selected)
    {
        if (button == null)
            return;

        Color targetColor = selected ? selectedAmountColor : normalAmountColor;

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

    public void SetOpenAmount1()
    {
        selectedOpenAmount = 1;
        maxAmountSelected = false;
        RefreshAmountButtons();
        RefreshCards();
    }

    public void SetOpenAmount5()
    {
        selectedOpenAmount = 5;
        maxAmountSelected = false;
        RefreshAmountButtons();
        RefreshCards();
    }

    public void SetOpenAmount10()
    {
        selectedOpenAmount = 10;
        maxAmountSelected = false;
        RefreshAmountButtons();
        RefreshCards();
    }

    public void SetOpenAmount50()
    {
        selectedOpenAmount = 50;
        maxAmountSelected = false;
        RefreshAmountButtons();
        RefreshCards();
    }

    public void SetOpenAmountMax()
    {
        maxAmountSelected = true;
        RefreshAmountButtons();
        RefreshCards();
    }
}
