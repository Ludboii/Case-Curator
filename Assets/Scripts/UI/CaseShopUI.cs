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

    [Header("Rank Divider Layout")]
    public int fallbackCardsPerRow = 3;
    public Vector2 fallbackCardCellSize = new Vector2(500f, 160f);
    public Vector2 fallbackCardSpacing = new Vector2(10f, 10f);
    public float rankDividerHeight = 80f;
    public float rankSectionSpacing = 10f;

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

    private Vector2 cardCellSize;
    private Vector2 cardSpacing;
    private RectOffset cardGridPadding;
    private int cardsPerRow;

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
        Invoke(nameof(RefreshCards), 0.1f);
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
        if (buy1Button != null)
        {
            buy1Button.onClick.RemoveAllListeners();
            buy1Button.onClick.AddListener(SetBuyAmount1);
        }

        if (buy5Button != null)
        {
            buy5Button.onClick.RemoveAllListeners();
            buy5Button.onClick.AddListener(SetBuyAmount5);
        }

        if (buy10Button != null)
        {
            buy10Button.onClick.RemoveAllListeners();
            buy10Button.onClick.AddListener(SetBuyAmount10);
        }

        if (buy50Button != null)
        {
            buy50Button.onClick.RemoveAllListeners();
            buy50Button.onClick.AddListener(SetBuyAmount50);
        }

        if (buyMaxButton != null)
        {
            buyMaxButton.onClick.RemoveAllListeners();
            buyMaxButton.onClick.AddListener(SetBuyAmountMax);
        }
    }

    private void SetupCategoryButtons()
    {
        if (previousCategoryButton != null)
        {
            previousCategoryButton.onClick.RemoveAllListeners();
            previousCategoryButton.onClick.AddListener(PreviousCategory);
        }

        if (nextCategoryButton != null)
        {
            nextCategoryButton.onClick.RemoveAllListeners();
            nextCategoryButton.onClick.AddListener(NextCategory);
        }
    }

    public void RefreshShop()
    {
        ClearSpawnedObjects();
        SetupFullWidthRankSectionLayout();
        SpawnCurrentCategory();
        RefreshCards();
        RefreshQuantityButtons();
        RefreshCategoryText();
        RebuildShopLayout();
    }

    private void SetupFullWidthRankSectionLayout()
    {
        if (caseGridContent == null)
            return;

        RectTransform contentRect = caseGridContent as RectTransform;

        if (contentRect != null)
        {
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(0f, contentRect.offsetMin.y);
            contentRect.offsetMax = new Vector2(0f, contentRect.offsetMax.y);
        }

        cardCellSize = fallbackCardCellSize;
        cardSpacing = fallbackCardSpacing;
        cardGridPadding = new RectOffset(30, 30, 30, 30);
        cardsPerRow = Mathf.Max(1, fallbackCardsPerRow);

        GridLayoutGroup oldGridLayout = caseGridContent.GetComponent<GridLayoutGroup>();

        if (oldGridLayout != null)
        {
            cardCellSize = oldGridLayout.cellSize;
            cardSpacing = oldGridLayout.spacing;
            cardGridPadding = new RectOffset(
                oldGridLayout.padding.left,
                oldGridLayout.padding.right,
                oldGridLayout.padding.top,
                oldGridLayout.padding.bottom);

            if (oldGridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
                cardsPerRow = Mathf.Max(1, oldGridLayout.constraintCount);

            oldGridLayout.enabled = false;
        }

        VerticalLayoutGroup verticalLayout = caseGridContent.GetComponent<VerticalLayoutGroup>();

        if (verticalLayout == null)
            verticalLayout = caseGridContent.gameObject.AddComponent<VerticalLayoutGroup>();

        verticalLayout.enabled = true;
        verticalLayout.childAlignment = TextAnchor.UpperLeft;
        verticalLayout.childControlWidth = true;
        verticalLayout.childControlHeight = true;
        verticalLayout.childForceExpandWidth = true;
        verticalLayout.childForceExpandHeight = false;
        verticalLayout.spacing = rankSectionSpacing;
        verticalLayout.padding = new RectOffset(0, 0, 0, 0);

        ContentSizeFitter fitter = caseGridContent.GetComponent<ContentSizeFitter>();

        if (fitter == null)
            fitter = caseGridContent.gameObject.AddComponent<ContentSizeFitter>();

        fitter.enabled = true;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void RebuildShopLayout()
    {
        if (caseGridContent == null)
            return;

        Canvas.ForceUpdateCanvases();

        RectTransform rectTransform = caseGridContent as RectTransform;
        if (rectTransform != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
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
        Transform currentCardsGrid = null;
        int cardsInCurrentSection = 0;
        LayoutElement currentGridLayoutElement = null;
        LayoutElement currentSectionLayoutElement = null;

        foreach (CaseData caseData in casesToShow)
        {
            if (caseData == null)
                continue;

            if (caseData.requiredRank != lastRank || currentCardsGrid == null)
            {
                CreateRankSection(
                    caseData.requiredRank,
                    out currentCardsGrid,
                    out currentGridLayoutElement,
                    out currentSectionLayoutElement);

                lastRank = caseData.requiredRank;
                cardsInCurrentSection = 0;
            }

            SpawnCaseCard(caseData, currentCardsGrid);
            cardsInCurrentSection++;

            UpdateSectionHeight(
                cardsInCurrentSection,
                currentGridLayoutElement,
                currentSectionLayoutElement);
        }
    }

    private List<CaseData> GetSortedCasesForCurrentCategory()
    {
        List<CaseData> cases = new List<CaseData>();

        foreach (CaseData caseData in database.allCases)
        {
            if (caseData == null)
                continue;

            if (caseData.shopCategory != currentCategory)
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

    private void CreateRankSection(
        PlayerRank requiredRank,
        out Transform cardsGridTransform,
        out LayoutElement gridLayoutElement,
        out LayoutElement sectionLayoutElement)
    {
        GameObject sectionObject = new GameObject($"Rank Section - {requiredRank}", typeof(RectTransform));
        sectionObject.transform.SetParent(caseGridContent, false);

        RectTransform sectionRect = sectionObject.GetComponent<RectTransform>();
        StretchFullWidth(sectionRect);

        VerticalLayoutGroup sectionLayout = sectionObject.AddComponent<VerticalLayoutGroup>();
        sectionLayout.childAlignment = TextAnchor.UpperLeft;
        sectionLayout.childControlWidth = true;
        sectionLayout.childControlHeight = true;
        sectionLayout.childForceExpandWidth = true;
        sectionLayout.childForceExpandHeight = false;
        sectionLayout.spacing = 0f;
        sectionLayout.padding = new RectOffset(0, 0, 0, 0);

        sectionLayoutElement = sectionObject.AddComponent<LayoutElement>();
        sectionLayoutElement.flexibleWidth = 1f;

        spawnedObjects.Add(sectionObject);

        if (rankDividerPrefab != null)
        {
            CaseShopRankDividerUI divider = Instantiate(rankDividerPrefab, sectionObject.transform);
            divider.gameObject.SetActive(true);
            divider.Setup(requiredRank);

            RectTransform dividerRect = divider.transform as RectTransform;
            StretchFullWidth(dividerRect);

            LayoutElement dividerLayout = divider.gameObject.GetComponent<LayoutElement>();

            if (dividerLayout == null)
                dividerLayout = divider.gameObject.AddComponent<LayoutElement>();

            dividerLayout.preferredHeight = rankDividerHeight;
            dividerLayout.minHeight = rankDividerHeight;
            dividerLayout.flexibleWidth = 1f;
        }

        GameObject gridObject = new GameObject("Cards Grid", typeof(RectTransform));
        gridObject.transform.SetParent(sectionObject.transform, false);

        RectTransform gridRect = gridObject.GetComponent<RectTransform>();
        StretchFullWidth(gridRect);

        GridLayoutGroup gridLayout = gridObject.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = cardCellSize;
        gridLayout.spacing = cardSpacing;
        gridLayout.padding = new RectOffset(
            cardGridPadding.left,
            cardGridPadding.right,
            cardGridPadding.top,
            cardGridPadding.bottom);
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.UpperLeft;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = Mathf.Max(1, cardsPerRow);

        gridLayoutElement = gridObject.AddComponent<LayoutElement>();
        gridLayoutElement.flexibleWidth = 1f;
        gridLayoutElement.preferredHeight = GetGridHeight(0);

        cardsGridTransform = gridObject.transform;

        UpdateSectionHeight(0, gridLayoutElement, sectionLayoutElement);
    }

    private void SpawnCaseCard(CaseData caseData, Transform parent)
    {
        if (parent == null)
            parent = caseGridContent;

        CaseShopCardUI card = Instantiate(caseCardPrefab, parent);

        card.gameObject.SetActive(true);
        card.Setup(caseData, this);

        spawnedCards.Add(card);
    }

    private void UpdateSectionHeight(
        int cardCount,
        LayoutElement gridLayoutElement,
        LayoutElement sectionLayoutElement)
    {
        float gridHeight = GetGridHeight(cardCount);

        if (gridLayoutElement != null)
            gridLayoutElement.preferredHeight = gridHeight;

        if (sectionLayoutElement != null)
        {
            float dividerHeight = rankDividerPrefab != null ? rankDividerHeight : 0f;
            sectionLayoutElement.preferredHeight = dividerHeight + gridHeight;
            sectionLayoutElement.minHeight = sectionLayoutElement.preferredHeight;
        }
    }

    private float GetGridHeight(int cardCount)
    {
        if (cardCount <= 0)
            return cardGridPadding.top + cardGridPadding.bottom;

        int columns = Mathf.Max(1, cardsPerRow);
        int rows = Mathf.CeilToInt(cardCount / (float)columns);

        return
            cardGridPadding.top +
            cardGridPadding.bottom +
            rows * cardCellSize.y +
            Mathf.Max(0, rows - 1) * cardSpacing.y;
    }

    private void StretchFullWidth(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.offsetMin = new Vector2(0f, rectTransform.offsetMin.y);
        rectTransform.offsetMax = new Vector2(0f, rectTransform.offsetMax.y);
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

        if (maxQuantitySelected)
            return GetMaxAffordableAmount(caseData);

        return selectedBuyAmount;
    }

    public int GetMaxAffordableAmount(CaseData caseData)
    {
        if (caseData == null)
            return 0;

        if (SaveManager.Instance == null)
            return 0;

        if (caseData.priceInGold <= 0f)
            return 0;

        int maxByGold = Mathf.FloorToInt(SaveManager.Instance.Gold / caseData.priceInGold);
        return Mathf.Max(0, maxByGold);
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

        PlayerRank playerRank = SaveManager.Instance.CurrentRank;

        if (playerRank < caseData.requiredRank)
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

        float totalCost = caseData.priceInGold * amount;

        if (SaveManager.Instance.Gold < totalCost)
        {
            failReason = "Not enough gold";
            return false;
        }

        return true;
    }

    public void TryBuyCase(CaseData caseData)
    {
        string failReason;

        if (!CanBuyCase(caseData, out failReason))
        {
            Debug.Log($"CaseShopUI: Cannot buy case. Reason: {failReason}");
            RefreshCards();
            return;
        }

        int amount = GetRequestedBuyAmount(caseData);

        if (amount <= 0)
            return;

        float totalCost = caseData.priceInGold * amount;

        if (totalCost > 0f)
        {
            bool spent = SaveManager.Instance.SpendGold(totalCost);

            if (!spent)
            {
                Debug.LogWarning("CaseShopUI: Failed to spend gold.");
                RefreshCards();
                return;
            }
        }

        CaseInventoryManager.Instance.AddCases(caseData, amount);
        SaveManager.Instance.SaveGame();

        Debug.Log($"Bought {amount}x {caseData.caseName} for {totalCost:0.00} gold.");

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

    public void SetBuyAmount1()
    {
        selectedBuyAmount = 1;
        maxQuantitySelected = false;
        RefreshQuantityButtons();
        RefreshCards();
    }

    public void SetBuyAmount5()
    {
        selectedBuyAmount = 5;
        maxQuantitySelected = false;
        RefreshQuantityButtons();
        RefreshCards();
    }

    public void SetBuyAmount10()
    {
        selectedBuyAmount = 10;
        maxQuantitySelected = false;
        RefreshQuantityButtons();
        RefreshCards();
    }

    public void SetBuyAmount50()
    {
        selectedBuyAmount = 50;
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
        int categoryCount = System.Enum.GetValues(typeof(CaseShopCategory)).Length;
        int currentIndex = (int)currentCategory;
        currentIndex--;

        if (currentIndex < 0)
            currentIndex = categoryCount - 1;

        currentCategory = (CaseShopCategory)currentIndex;
        RefreshShop();
    }

    public void NextCategory()
    {
        int categoryCount = System.Enum.GetValues(typeof(CaseShopCategory)).Length;
        int currentIndex = (int)currentCategory;
        currentIndex++;

        if (currentIndex >= categoryCount)
            currentIndex = 0;

        currentCategory = (CaseShopCategory)currentIndex;
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
