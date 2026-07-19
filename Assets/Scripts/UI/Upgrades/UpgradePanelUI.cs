using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Case Shop-style upgrade browser. It filters one shared catalog by the
/// arrow-driven category bar, sorts by purchase state/price, and pools cards.
/// </summary>
[DisallowMultipleComponent]
public class UpgradePanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UpgradeCategoryBarUI categoryBar;
    [SerializeField] private ScrollRect upgradeScrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private UpgradeCardUI upgradeCardPrefab;
    [SerializeField] private TMP_Text purchasedLevelsText;
    [SerializeField] private TMP_Text emptyCategoryText;
    [SerializeField] private GameObject purchaseBlocker;

    [Header("Options")]
    [SerializeField] private bool includeDebugUpgrades;
    [SerializeField] private bool resetScrollOnCategoryChange = true;
    [SerializeField] private bool preserveScrollOnRefresh = true;

    private readonly List<UpgradeCardUI> cardPool =
        new List<UpgradeCardUI>();

    private readonly List<UpgradeListEntry> visibleEntries =
        new List<UpgradeListEntry>();

    private Coroutine restoreScrollCoroutine;
    private bool subscribed;

    private sealed class UpgradeListEntry
    {
        public UpgradeData upgrade;
        public UpgradePurchaseResult evaluation;
        public int stateGroup;
        public float nextCost;
    }

    private void OnEnable()
    {
        Subscribe();

        // Ensures the aggregate statistics cache is subscribed before cards
        // begin evaluating openable-count requirements.
        OpenableStatisticsService.GetTotalOpenedCount();
        Refresh(false);
    }

    private void OnDisable()
    {
        Unsubscribe();

        if (restoreScrollCoroutine != null)
        {
            StopCoroutine(restoreScrollCoroutine);
            restoreScrollCoroutine = null;
        }
    }

    public void Refresh(bool preserveScroll = true)
    {
        UpgradeService service = UpgradeService.Instance;

        if (service == null || content == null || upgradeCardPrefab == null)
        {
            SetEmptyState(
                service == null
                    ? "Upgrade Service is unavailable."
                    : "Upgrade panel references are incomplete.");
            return;
        }

        float previousScroll = upgradeScrollRect != null
            ? upgradeScrollRect.verticalNormalizedPosition
            : 1f;

        BuildVisibleEntries(service);
        EnsureCardPool(visibleEntries.Count);

        for (int i = 0; i < cardPool.Count; i++)
        {
            UpgradeCardUI card = cardPool[i];
            bool active = i < visibleEntries.Count;

            card.gameObject.SetActive(active);

            if (!active)
                continue;

            card.transform.SetSiblingIndex(i);
            card.Bind(
                visibleEntries[i].upgrade,
                HandlePurchaseCompleted);
        }

        UpdatePurchasedLevels(service);

        if (categoryBar != null)
            categoryBar.SetItemCount(visibleEntries.Count);

        SetEmptyState(
            visibleEntries.Count == 0
                ? "No upgrades are available in this category."
                : "");

        bool shouldPreserve = preserveScroll && preserveScrollOnRefresh;

        if (upgradeScrollRect != null)
        {
            if (restoreScrollCoroutine != null)
                StopCoroutine(restoreScrollCoroutine);

            restoreScrollCoroutine = StartCoroutine(
                RestoreScrollNextFrame(
                    shouldPreserve ? previousScroll : 1f));
        }
    }

    public void ShowCategory(UpgradePanelCategory category)
    {
        if (categoryBar != null)
            categoryBar.SetCategory(category, false);

        HandleCategoryChanged(category);
    }

    private void BuildVisibleEntries(UpgradeService service)
    {
        visibleEntries.Clear();
        IReadOnlyList<UpgradeData> upgrades = service.GetAllUpgrades();
        UpgradePanelCategory selectedCategory = categoryBar != null
            ? categoryBar.CurrentCategory
            : UpgradePanelCategory.All;

        for (int i = 0; i < upgrades.Count; i++)
        {
            UpgradeData upgrade = upgrades[i];

            if (upgrade == null)
                continue;

            if (upgrade.category == UpgradeCategory.Debug &&
                !includeDebugUpgrades)
            {
                continue;
            }

            if (!MatchesCategory(upgrade.category, selectedCategory))
                continue;

            if (upgrade.hiddenUntilUnlocked &&
                upgrade.unlockDefinition != null &&
                !UnlockEvaluator.IsUnlocked(upgrade.unlockDefinition))
            {
                continue;
            }

            UpgradePurchaseResult evaluation =
                service.EvaluatePurchase(upgrade);

            visibleEntries.Add(
                new UpgradeListEntry
                {
                    upgrade = upgrade,
                    evaluation = evaluation,
                    stateGroup = GetStateGroup(evaluation),
                    nextCost = GetSortCost(evaluation)
                });
        }

        visibleEntries.Sort(CompareEntries);
    }

    private void EnsureCardPool(int requiredCount)
    {
        while (cardPool.Count < requiredCount)
        {
            UpgradeCardUI card = Instantiate(
                upgradeCardPrefab,
                content);

            cardPool.Add(card);
        }
    }

    private void UpdatePurchasedLevels(UpgradeService service)
    {
        if (purchasedLevelsText == null)
            return;

        IReadOnlyList<UpgradeData> upgrades = service.GetAllUpgrades();
        int purchased = 0;
        int total = 0;

        for (int i = 0; i < upgrades.Count; i++)
        {
            UpgradeData upgrade = upgrades[i];

            if (upgrade == null ||
                (upgrade.category == UpgradeCategory.Debug &&
                 !includeDebugUpgrades))
            {
                continue;
            }

            purchased += Mathf.Clamp(
                service.GetLevel(upgrade),
                0,
                upgrade.MaxLevel);

            total += Mathf.Max(0, upgrade.MaxLevel);
        }

        purchasedLevelsText.text =
            $"BOUGHT: {purchased:N0} / {total:N0}";
    }

    private void HandleCategoryChanged(
        UpgradePanelCategory category)
    {
        Refresh(!resetScrollOnCategoryChange);
    }

    private void HandlePurchaseCompleted(
        UpgradePurchaseResult result)
    {
        if (purchaseBlocker != null)
            purchaseBlocker.SetActive(false);

        Refresh(true);
    }

    private void HandleExternalStateChanged()
    {
        if (isActiveAndEnabled)
            Refresh(true);
    }

    private void HandleUpgradeLevelChanged(
        UpgradeData upgrade,
        int previousLevel,
        int newLevel)
    {
        HandleExternalStateChanged();
    }

    private void Subscribe()
    {
        if (subscribed)
            return;

        subscribed = true;

        if (categoryBar != null)
            categoryBar.OnCategoryChanged += HandleCategoryChanged;

        if (UpgradeService.Instance != null)
        {
            UpgradeService.Instance.OnUpgradeLevelChanged +=
                HandleUpgradeLevelChanged;

            UpgradeService.Instance.OnUpgradeStateChanged +=
                HandleExternalStateChanged;
        }

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnCurrencyChanged +=
                HandleExternalStateChanged;

            SaveManager.Instance.OnProgressChanged +=
                HandleExternalStateChanged;

            SaveManager.Instance.OnTradeupStateChanged +=
                HandleExternalStateChanged;
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged +=
                HandleExternalStateChanged;
        }

        OpenableStatisticsService.OnStatisticsChanged +=
            HandleExternalStateChanged;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        subscribed = false;

        if (categoryBar != null)
            categoryBar.OnCategoryChanged -= HandleCategoryChanged;

        if (UpgradeService.Instance != null)
        {
            UpgradeService.Instance.OnUpgradeLevelChanged -=
                HandleUpgradeLevelChanged;

            UpgradeService.Instance.OnUpgradeStateChanged -=
                HandleExternalStateChanged;
        }

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnCurrencyChanged -=
                HandleExternalStateChanged;

            SaveManager.Instance.OnProgressChanged -=
                HandleExternalStateChanged;

            SaveManager.Instance.OnTradeupStateChanged -=
                HandleExternalStateChanged;
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -=
                HandleExternalStateChanged;
        }

        OpenableStatisticsService.OnStatisticsChanged -=
            HandleExternalStateChanged;
    }

    private IEnumerator RestoreScrollNextFrame(float value)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        if (upgradeScrollRect != null)
        {
            upgradeScrollRect.verticalNormalizedPosition =
                Mathf.Clamp01(value);
        }

        restoreScrollCoroutine = null;
    }

    private void SetEmptyState(string message)
    {
        if (emptyCategoryText == null)
            return;

        bool visible = !string.IsNullOrWhiteSpace(message);
        emptyCategoryText.gameObject.SetActive(visible);
        emptyCategoryText.text = visible ? message : "";
    }

    private static int CompareEntries(
        UpgradeListEntry a,
        UpgradeListEntry b)
    {
        int result = a.stateGroup.CompareTo(b.stateGroup);

        if (result != 0)
            return result;

        result = a.nextCost.CompareTo(b.nextCost);

        if (result != 0)
            return result;

        result = a.upgrade.sortOrder.CompareTo(b.upgrade.sortOrder);

        if (result != 0)
            return result;

        return string.Compare(
            a.upgrade.DisplayName,
            b.upgrade.DisplayName,
            StringComparison.OrdinalIgnoreCase);
    }

    private static int GetStateGroup(
        UpgradePurchaseResult result)
    {
        if (result == null)
            return 4;

        switch (result.status)
        {
            case UpgradePurchaseStatus.Ready:
                return 0;

            case UpgradePurchaseStatus.InsufficientGold:
            case UpgradePurchaseStatus.InsufficientDiamonds:
                return 1;

            case UpgradePurchaseStatus.Locked:
                return 2;

            case UpgradePurchaseStatus.MaximumLevelReached:
                return 3;

            default:
                return 4;
        }
    }

    private static float GetSortCost(
        UpgradePurchaseResult result)
    {
        if (result == null ||
            result.status == UpgradePurchaseStatus.MaximumLevelReached)
        {
            return float.MaxValue;
        }

        return Mathf.Max(0f, result.cost);
    }

    private static bool MatchesCategory(
        UpgradeCategory dataCategory,
        UpgradePanelCategory panelCategory)
    {
        if (panelCategory == UpgradePanelCategory.All)
            return dataCategory != UpgradeCategory.Debug;

        switch (panelCategory)
        {
            case UpgradePanelCategory.Opening:
                return dataCategory == UpgradeCategory.Opening;

            case UpgradePanelCategory.Inventory:
                return dataCategory == UpgradeCategory.Inventory;

            case UpgradePanelCategory.Museum:
                return dataCategory == UpgradeCategory.Museum ||
                       dataCategory == UpgradeCategory.GiftDesk ||
                       dataCategory == UpgradeCategory.TrophyRoom ||
                       dataCategory ==
                           UpgradeCategory.AutomatedAcquisitions;

            case UpgradePanelCategory.Minigames:
                return dataCategory == UpgradeCategory.Minigames;

            case UpgradePanelCategory.SingleUse:
                return dataCategory == UpgradeCategory.SingleUse;

            case UpgradePanelCategory.Others:
                return dataCategory == UpgradeCategory.General ||
                       dataCategory == UpgradeCategory.Tradeups ||
                       dataCategory == UpgradeCategory.Others;

            default:
                return false;
        }
    }
}
