using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CaseShopUI : MonoBehaviour
{
    [Header("Database")]
    public GameDatabase database;

    [Header("Card Spawning")]
    public Transform caseGridContent;
    public CaseShopCardUI caseCardPrefab;
    public CaseShopRankDividerUI rankDividerPrefab;

    [Header("Category UI")]
    public TMP_Text categoryText;
    public Button previousCategoryButton;
    public Button nextCategoryButton;

    [Header("Buy Amount Buttons")]
    public Button buy1Button;
    public Button buy5Button;
    public Button buy10Button;
    public Button buy50Button;
    public Button buyMaxButton;

    [Header("Buy Amount Button Colors")]
    public Color selectedQuantityColor = new Color(0.2f, 0.85f, 0.35f, 1f);
    public Color normalQuantityColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private readonly List<CaseShopCardUI> spawnedCards = new List<CaseShopCardUI>();

    private int selectedBuyAmount = 1;
    private bool maxQuantitySelected;
    private CaseShopCategory currentCategory = CaseShopCategory.Cases;

    public bool IsMaxQuantitySelected => maxQuantitySelected;

    private void Awake()
    {
        SetupBuyAmountButtons();
        SetupCategoryButtons();
    }

    private void OnEnable()
    {
        SubscribeToEvents();
        RefreshShop();
    }

    private void Start()
    {
        RefreshShop();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnCurrencyChanged -= RefreshCards;
            SaveManager.Instance.OnCurrencyChanged += RefreshCards;

            SaveManager.Instance.OnProgressChanged -= RefreshCards;
            SaveManager.Instance.OnProgressChanged += RefreshCards;
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= RefreshCards;
            InventoryManager.Instance.OnInventoryChanged += RefreshCards;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnCurrencyChanged -= RefreshCards;
            SaveManager.Instance.OnProgressChanged -= RefreshCards;
        }

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshCards;
    }

    private void SetupBuyAmountButtons()
    {
        SetupButton(buy1Button, SetBuyAmount1);
        SetupButton(buy5Button, SetBuyAmount5);
        SetupButton(buy10Button, SetBuyAmount10);
        SetupButton(buy50Button, SetBuyAmount50);
        SetupButton(buyMaxButton, SetBuyAmountMax);
    }

    private void SetupCategoryButtons()
    {
        SetupButton(previousCategoryButton, PreviousCategory);
        SetupButton(nextCategoryButton, NextCategory);
    }

    private void SetupButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    public void RefreshShop()
    {
        ClearSpawnedObjects();
        EnsureStableGridLayout();
        SpawnCurrentCategory();
        RefreshCards();
        RefreshQuantityButtons();
        RefreshCategoryText();
        RebuildLayout();
    }

    private void EnsureStableGridLayout()
    {
        if (caseGridContent == null)
            return;

        VerticalLayoutGroup verticalLayout = caseGridContent.GetComponent<VerticalLayoutGroup>();
        if (verticalLayout != null)
            verticalLayout.enabled = false;

        GridLayoutGroup gridLayout = caseGridContent.GetComponent<GridLayoutGroup>();
        if (gridLayout != null)
            gridLayout.enabled = true;

        ContentSizeFitter fitter = caseGridContent.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            fitter.enabled = true;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    private void RebuildLayout()
    {
        if (caseGridContent == null)
            return;

        Canvas.ForceUpdateCanvases();

        RectTransform rect = caseGridContent as RectTransform;
        if (rect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private void SpawnCurrentCategory()
    {
        if (database == null)
        {
            Debug.LogWarning("CaseShopUI: No GameDatabase assigned.");
            return;
        }

        if (caseGridContent == null)
        {
            Debug.LogWarning("CaseShopUI: No caseGridContent assigned.");
            return;
        }

        if (caseCardPrefab == null)
        {
            Debug.LogWarning("CaseShopUI: No caseCardPrefab assigned.");
            return;
        }

        List<CaseData> casesToShow = GetSortedCasesForCurrentCategory();
        PlayerRank lastRank = (PlayerRank)(-1);

        foreach (CaseData caseData in casesToShow)
        {
            if (caseData == null)
                continue;

            if (caseData.requiredRank != lastRank)
            {
                SpawnRankDivider(caseData.requiredRank);
                lastRank = caseData.requiredRank;
            }

            SpawnCaseCard(caseData);
        }
    }

    private List<CaseData> GetSortedCasesForCurrentCategory()
    {
        List<CaseData> cases = new List<CaseData>();

        if (database == null || database.allCases == null)
            return cases;

        foreach (CaseData caseData in database.allCases)
        {
            if (caseData == null || caseData.shopCategory != currentCategory)
                continue;

            cases.Add(caseData);
        }

        cases.Sort((a, b) =>
        {
            int rankCompare = a.requiredRank.CompareTo(b.requiredRank);
            if (rankCompare != 0)
                return rankCompare;

            int priceCompare = a.priceInGold.CompareTo(b.priceInGold);
            if (priceCompare != 0)
                return priceCompare;

            return string.Compare(a.caseName, b.caseName);
        });

        return cases;
    }

    private void SpawnRankDivider(PlayerRank requiredRank)
    {
        if (rankDividerPrefab == null || caseGridContent == null)
            return;

        CaseShopRankDividerUI divider = Instantiate(rankDividerPrefab, caseGridContent);
        divider.gameObject.SetActive(true);
        divider.Setup(requiredRank);
        spawnedObjects.Add(divider.gameObject);
    }

    private void SpawnCaseCard(CaseData caseData)
    {
        CaseShopCardUI card = Instantiate(caseCardPrefab, caseGridContent);
        card.gameObject.SetActive(true);
        card.Setup(caseData, this);

        spawnedCards.Add(card);
        spawnedObjects.Add(card.gameObject);
    }

    private void ClearSpawnedObjects()
    {
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null)
                Destroy(obj);
        }

        spawnedObjects.Clear();
        spawnedCards.Clear();
    }

    public void RefreshCards()
    {
        foreach (CaseShopCardUI card in spawnedCards)
        {
            if (card != null)
                card.RefreshState();
        }
    }

    private void RefreshQuantityButtons()
    {
        ApplyQuantityButtonVisual(buy1Button, !maxQuantitySelected && selectedBuyAmount == 1);
        ApplyQuantityButtonVisual(buy5Button, !maxQuantitySelected && selectedBuyAmount == 5);
        ApplyQuantityButtonVisual(buy10Button, !maxQuantitySelected && selectedBuyAmount == 10);
        ApplyQuantityButtonVisual(buy50Button, !maxQuantitySelected && selectedBuyAmount == 50);
        ApplyQuantityButtonVisual(buyMaxButton, maxQuantitySelected);
    }

    private void ApplyQuantityButtonVisual(Button button, bool selected)
    {
        if (button == null)
            return;

        Color targetColor = selected ? selectedQuantityColor : normalQuantityColor;
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

    public int GetRequestedBuyAmount(CaseData caseData)
    {
        if (caseData == null)
            return 0;

        return maxQuantitySelected ? GetMaxAffordableAmount(caseData) : selectedBuyAmount;
    }

    public int GetMaxAffordableAmount(CaseData caseData)
    {
        if (caseData == null || SaveManager.Instance == null || caseData.priceInGold <= 0f)
            return 0;

        return Mathf.Max(0, Mathf.FloorToInt(SaveManager.Instance.Gold / caseData.priceInGold));
    }

    public bool CanBuyCase(CaseData caseData, out string failReason)
    {
        failReason = "";

        if (caseData == null)
        {
            failReason = "Missing case";
            return false;
        }

        if (SaveManager.Instance == null)
        {
            failReason = "No save";
            return false;
        }

        if (CaseInventoryManager.Instance == null)
        {
            failReason = "No case inventory";
            return false;
        }

        if (SaveManager.Instance.CurrentRank < caseData.requiredRank)
        {
            failReason = $"Rank: {PlayerProgressUtility.GetRankDisplayName(caseData.requiredRank)}";
            return false;
        }

        int amount = GetRequestedBuyAmount(caseData);
        if (amount <= 0)
        {
            failReason = "Amount 0";
            return false;
        }

        if (SaveManager.Instance.Gold < caseData.priceInGold * amount)
        {
            failReason = "Not enough gold";
            return false;
        }

        return true;
    }

    public void TryBuyCase(CaseData caseData)
    {
        if (!CanBuyCase(caseData, out string failReason))
        {
            Debug.Log($"CaseShopUI: Cannot buy case. Reason: {failReason}");
            RefreshCards();
            return;
        }

        int amount = GetRequestedBuyAmount(caseData);
        float totalCost = caseData.priceInGold * amount;

        if (totalCost > 0f && !SaveManager.Instance.SpendGold(totalCost))
        {
            Debug.LogWarning("CaseShopUI: Failed to spend gold.");
            RefreshCards();
            return;
        }

        CaseInventoryManager.Instance.AddCases(caseData, amount);
        SaveManager.Instance.SaveGame();
        RefreshCards();
    }

    public void OpenCaseInspect(CaseData caseData)
    {
        if (caseData == null)
            return;

        if (CaseInspectUI.Instance == null)
        {
            Debug.LogWarning("CaseShopUI: No CaseInspectUI found in scene.");
            return;
        }

        CaseInspectUI.Instance.Open(caseData);
    }

    public void SetBuyAmount1() => SetBuyAmount(1);
    public void SetBuyAmount5() => SetBuyAmount(5);
    public void SetBuyAmount10() => SetBuyAmount(10);
    public void SetBuyAmount50() => SetBuyAmount(50);

    private void SetBuyAmount(int amount)
    {
        selectedBuyAmount = amount;
        maxQuantitySelected = false;
        RefreshQuantityButtons();
        RefreshCards();
    }

    public void SetBuyAmountMax()
    {
        maxQuantitySelected = true;
        RefreshQuantityButtons();
        RefreshCards();
    }

    public void PreviousCategory()
    {
        int count = System.Enum.GetValues(typeof(CaseShopCategory)).Length;
        int index = (int)currentCategory - 1;
        if (index < 0)
            index = count - 1;

        currentCategory = (CaseShopCategory)index;
        RefreshShop();
    }

    public void NextCategory()
    {
        int count = System.Enum.GetValues(typeof(CaseShopCategory)).Length;
        int index = (int)currentCategory + 1;
        if (index >= count)
            index = 0;

        currentCategory = (CaseShopCategory)index;
        RefreshShop();
    }

    private void RefreshCategoryText()
    {
        if (categoryText == null)
            return;

        switch (currentCategory)
        {
            case CaseShopCategory.Cases:
                categoryText.text = "Cases";
                break;
            case CaseShopCategory.Collections:
                categoryText.text = "Collections";
                break;
            case CaseShopCategory.SouvenirCollections:
                categoryText.text = "Souvenir";
                break;
            case CaseShopCategory.CustomCases:
                categoryText.text = "Custom";
                break;
            default:
                categoryText.text = currentCategory.ToString();
                break;
        }
    }
}
