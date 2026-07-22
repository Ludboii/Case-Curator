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

    [Header("Rank Section Layout")]
    [Min(1)] public int fallbackCardsPerRow = 3;
    public Vector2 fallbackCardCellSize = new Vector2(500f, 160f);
    public Vector2 fallbackCardSpacing = new Vector2(12f, 12f);
    [Min(1f)] public float rankDividerHeight = 95f;
    [Min(0f)] public float rankSectionSpacing = 12f;

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
    private GridSettings gridSettings;
    private bool layoutPrepared;

    public bool IsMaxQuantitySelected => maxQuantitySelected;

    private sealed class GridSettings
    {
        public Vector2 cellSize;
        public Vector2 spacing;
        public GridLayoutGroup.Corner startCorner;
        public GridLayoutGroup.Axis startAxis;
        public TextAnchor childAlignment;
        public int constraintCount;
        public RectOffset contentPadding;
    }

    private void Awake()
    {
        PrepareRankSectionLayout();
        SetupBuyAmountButtons();
        SetupCategoryButtons();
    }

    private void OnEnable()
    {
        SubscribeToEvents();
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

    private static void SetupButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    public void RefreshShop()
    {
        if (!layoutPrepared)
            PrepareRankSectionLayout();

        ClearSpawnedObjects();
        SpawnCurrentCategory();
        RefreshCards();
        RefreshQuantityButtons();
        RefreshCategoryText();
        RebuildLayout();
    }

    private void PrepareRankSectionLayout()
    {
        if (caseGridContent == null)
        {
            Debug.LogWarning("CaseShopUI: caseGridContent is not assigned.", this);
            return;
        }

        GridLayoutGroup oldGrid =
            caseGridContent.GetComponent<GridLayoutGroup>();

        CaptureGridSettings(oldGrid);

        // Unity allows only one LayoutGroup on a GameObject. The previous
        // implementation disabled GridLayoutGroup and then tried to add a
        // VerticalLayoutGroup, which Unity rejects. Removing the old component
        // first is required because the outer content is now a vertical list of
        // divider + per-rank grid sections.
        if (oldGrid != null)
        {
            if (Application.isPlaying)
                DestroyImmediate(oldGrid);
            else
                DestroyImmediate(oldGrid, true);
        }

        VerticalLayoutGroup verticalLayout =
            caseGridContent.GetComponent<VerticalLayoutGroup>();

        if (verticalLayout == null)
            verticalLayout = caseGridContent.gameObject.AddComponent<VerticalLayoutGroup>();

        if (verticalLayout == null)
        {
            Debug.LogError(
                "CaseShopUI: Could not create the VerticalLayoutGroup used by rank sections.",
                this);
            return;
        }

        verticalLayout.enabled = true;
        verticalLayout.padding = CopyPadding(gridSettings.contentPadding);
        verticalLayout.spacing = rankSectionSpacing;
        verticalLayout.childAlignment = TextAnchor.UpperLeft;
        verticalLayout.childControlWidth = true;
        verticalLayout.childControlHeight = true;
        verticalLayout.childForceExpandWidth = true;
        verticalLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter =
            caseGridContent.GetComponent<ContentSizeFitter>();

        if (fitter == null)
            fitter = caseGridContent.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        layoutPrepared = true;
    }

    private void CaptureGridSettings(GridLayoutGroup existingGrid)
    {
        gridSettings = new GridSettings
        {
            cellSize = existingGrid != null
                ? existingGrid.cellSize
                : fallbackCardCellSize,
            spacing = existingGrid != null
                ? existingGrid.spacing
                : fallbackCardSpacing,
            startCorner = existingGrid != null
                ? existingGrid.startCorner
                : GridLayoutGroup.Corner.UpperLeft,
            startAxis = existingGrid != null
                ? existingGrid.startAxis
                : GridLayoutGroup.Axis.Horizontal,
            childAlignment = existingGrid != null
                ? existingGrid.childAlignment
                : TextAnchor.UpperLeft,
            constraintCount = existingGrid != null &&
                              existingGrid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
                ? Mathf.Max(1, existingGrid.constraintCount)
                : Mathf.Max(1, fallbackCardsPerRow),
            contentPadding = existingGrid != null
                ? CopyPadding(existingGrid.padding)
                : new RectOffset()
        };
    }

    private void RebuildLayout()
    {
        if (caseGridContent == null)
            return;

        Canvas.ForceUpdateCanvases();

        if (caseGridContent is RectTransform rect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private void SpawnCurrentCategory()
    {
        if (database == null || caseGridContent == null || caseCardPrefab == null)
        {
            Debug.LogWarning(
                "CaseShopUI: Database, content, or case-card prefab is missing.",
                this);
            return;
        }

        List<CaseData> casesToShow = GetSortedCasesForCurrentCategory();
        int index = 0;

        while (index < casesToShow.Count)
        {
            CaseData firstCase = casesToShow[index];

            if (firstCase == null)
            {
                index++;
                continue;
            }

            PlayerRank rank = firstCase.requiredRank;
            int sectionStart = index;

            while (index < casesToShow.Count &&
                   casesToShow[index] != null &&
                   casesToShow[index].requiredRank == rank)
            {
                index++;
            }

            int sectionCount = index - sectionStart;
            SpawnRankDivider(rank);
            Transform rankGrid = CreateRankCardGrid(rank, sectionCount);

            for (int i = sectionStart; i < index; i++)
                SpawnCaseCard(casesToShow[i], rankGrid);
        }
    }

    private List<CaseData> GetSortedCasesForCurrentCategory()
    {
        List<CaseData> cases = new List<CaseData>();

        if (database == null || database.allCases == null)
            return cases;

        foreach (CaseData caseData in database.allCases)
        {
            if (caseData != null && caseData.shopCategory == currentCategory)
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

        CaseShopRankDividerUI divider = Instantiate(
            rankDividerPrefab,
            caseGridContent);

        divider.gameObject.SetActive(true);
        divider.Setup(requiredRank, currentCategory);

        if (divider.transform is RectTransform dividerRect)
        {
            dividerRect.anchorMin = new Vector2(0f, 1f);
            dividerRect.anchorMax = new Vector2(1f, 1f);
            dividerRect.pivot = new Vector2(0.5f, 1f);
            dividerRect.sizeDelta = new Vector2(0f, rankDividerHeight);
        }

        LayoutElement layoutElement = divider.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = divider.gameObject.AddComponent<LayoutElement>();

        layoutElement.minHeight = rankDividerHeight;
        layoutElement.preferredHeight = rankDividerHeight;
        layoutElement.flexibleWidth = 1f;
        layoutElement.flexibleHeight = 0f;
        spawnedObjects.Add(divider.gameObject);
    }

    private Transform CreateRankCardGrid(PlayerRank rank, int cardCount)
    {
        GameObject gridObject = new GameObject(
            $"RankCards_{rank}",
            typeof(RectTransform),
            typeof(GridLayoutGroup),
            typeof(LayoutElement));

        gridObject.transform.SetParent(caseGridContent, false);

        RectTransform rect = gridObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = Vector2.zero;

        GridLayoutGroup grid = gridObject.GetComponent<GridLayoutGroup>();
        grid.cellSize = gridSettings.cellSize;
        grid.spacing = gridSettings.spacing;
        grid.startCorner = gridSettings.startCorner;
        grid.startAxis = gridSettings.startAxis;
        grid.childAlignment = gridSettings.childAlignment;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, gridSettings.constraintCount);
        grid.padding = new RectOffset();

        int rows = Mathf.Max(
            1,
            Mathf.CeilToInt(cardCount / (float)grid.constraintCount));

        float preferredHeight =
            rows * grid.cellSize.y +
            Mathf.Max(0, rows - 1) * grid.spacing.y;

        LayoutElement layout = gridObject.GetComponent<LayoutElement>();
        layout.minHeight = preferredHeight;
        layout.preferredHeight = preferredHeight;
        layout.flexibleWidth = 1f;
        layout.flexibleHeight = 0f;

        spawnedObjects.Add(gridObject);
        return gridObject.transform;
    }

    private void SpawnCaseCard(CaseData caseData, Transform parent)
    {
        if (caseData == null || parent == null)
            return;

        CaseShopCardUI card = Instantiate(caseCardPrefab, parent);
        card.gameObject.SetActive(true);
        card.Setup(caseData, this);
        spawnedCards.Add(card);
    }

    private void ClearSpawnedObjects()
    {
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj == null)
                continue;

            obj.SetActive(false);
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

        return maxQuantitySelected
            ? GetMaxAffordableAmount(caseData)
            : selectedBuyAmount;
    }

    public int GetMaxAffordableAmount(CaseData caseData)
    {
        if (caseData == null ||
            SaveManager.Instance == null ||
            caseData.priceInGold <= 0f)
        {
            return 0;
        }

        return Mathf.Max(
            0,
            Mathf.FloorToInt(SaveManager.Instance.Gold / caseData.priceInGold));
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
            failReason =
                $"Rank: {PlayerProgressUtility.GetRankDisplayName(caseData.requiredRank)}";
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

    private static RectOffset CopyPadding(RectOffset source)
    {
        if (source == null)
            return new RectOffset();

        return new RectOffset(
            source.left,
            source.right,
            source.top,
            source.bottom);
    }
}
