using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies the permanent 10% shop-price discount earned by Gold-completing a
/// container. Runtime prices are restored when the service is destroyed so the
/// underlying CaseData assets are never intentionally re-authored.
/// </summary>
[DisallowMultipleComponent]
public sealed class GoldCompletionCaseDiscountService : MonoBehaviour
{
    public const float DiscountFraction = 0.10f;
    public const float PriceMultiplier = 1f - DiscountFraction;

    public static GoldCompletionCaseDiscountService Instance { get; private set; }

    private readonly Dictionary<CaseData, float> basePrices =
        new Dictionary<CaseData, float>();

    private ContainerProgressManager subscribedProgressManager;
    private float nextRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GetOrCreate();
    }

    public static GoldCompletionCaseDiscountService GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        GoldCompletionCaseDiscountService existing =
            FindFirstObjectByType<GoldCompletionCaseDiscountService>();

        if (existing != null)
            return existing;

        GameObject go = new GameObject("GoldCompletionCaseDiscountService");
        return go.AddComponent<GoldCompletionCaseDiscountService>();
    }

    public static bool HasGoldDiscount(CaseData caseData)
    {
        return caseData != null &&
               ContainerProgressManager.Instance != null &&
               ContainerProgressManager.Instance.IsGoldComplete(caseData);
    }

    public static float GetEffectivePrice(CaseData caseData)
    {
        if (caseData == null)
            return 0f;

        GoldCompletionCaseDiscountService service = GetOrCreate();
        float basePrice = service.GetBasePrice(caseData);

        return HasGoldDiscount(caseData)
            ? Mathf.Max(0f, basePrice * PriceMultiplier)
            : Mathf.Max(0f, basePrice);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CacheDatabasePrices();
    }

    private void Start()
    {
        RefreshNow();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh)
            return;

        nextRefresh = Time.unscaledTime + 0.25f;
        RefreshNow();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        RestoreAllBasePrices();

        if (Instance == this)
            Instance = null;
    }

    private void RefreshNow()
    {
        EnsureSubscribed();
        ApplyAllDiscounts();
        RefreshCompletionPresentation();
    }

    private void EnsureSubscribed()
    {
        ContainerProgressManager current = ContainerProgressManager.Instance;

        if (ReferenceEquals(current, subscribedProgressManager))
            return;

        Unsubscribe();
        subscribedProgressManager = current;

        if (subscribedProgressManager == null)
            return;

        subscribedProgressManager.OnContainerProgressChanged -=
            HandleProgressChanged;
        subscribedProgressManager.OnContainerProgressChanged +=
            HandleProgressChanged;
    }

    private void Unsubscribe()
    {
        if (subscribedProgressManager != null)
        {
            subscribedProgressManager.OnContainerProgressChanged -=
                HandleProgressChanged;
        }

        subscribedProgressManager = null;
    }

    private void HandleProgressChanged()
    {
        ApplyAllDiscounts();
        RefreshCompletionPresentation();
    }

    private void CacheDatabasePrices()
    {
        GameDatabase database = SaveManager.Instance != null
            ? SaveManager.Instance.database
            : null;

        if (database == null || database.allCases == null)
            return;

        for (int i = 0; i < database.allCases.Count; i++)
        {
            CaseData caseData = database.allCases[i];

            if (caseData == null || basePrices.ContainsKey(caseData))
                continue;

            float currentPrice = Mathf.Max(0f, caseData.priceInGold);

            // With Enter Play Mode Options enabled, a previous runtime mutation
            // can survive long enough for a recreated service to see the already
            // discounted value. Recover the original base price in that case.
            float basePrice = HasGoldDiscount(caseData) && currentPrice > 0f
                ? currentPrice / PriceMultiplier
                : currentPrice;

            basePrices.Add(caseData, basePrice);
        }
    }

    private float GetBasePrice(CaseData caseData)
    {
        if (caseData == null)
            return 0f;

        if (!basePrices.TryGetValue(caseData, out float price))
        {
            float currentPrice = Mathf.Max(0f, caseData.priceInGold);
            price = HasGoldDiscount(caseData) && currentPrice > 0f
                ? currentPrice / PriceMultiplier
                : currentPrice;
            basePrices.Add(caseData, price);
        }

        return price;
    }

    private void ApplyAllDiscounts()
    {
        CacheDatabasePrices();

        foreach (KeyValuePair<CaseData, float> pair in basePrices)
        {
            if (pair.Key == null)
                continue;

            pair.Key.priceInGold = HasGoldDiscount(pair.Key)
                ? pair.Value * PriceMultiplier
                : pair.Value;
        }
    }

    private static void RefreshCompletionPresentation()
    {
        CaseInspectCompletionPopupUI popup =
            FindFirstObjectByType<CaseInspectCompletionPopupUI>(
                FindObjectsInactive.Include);

        if (popup == null || popup.goldExplanationText == null)
            return;

        const string discountLine =
            "\nPermanent reward: 10% discount on this container in the Case Shop.";
        string text = popup.goldExplanationText.text ?? "";

        if (!text.Contains("10% discount"))
            popup.goldExplanationText.text = text + discountLine;
    }

    private void RestoreAllBasePrices()
    {
        foreach (KeyValuePair<CaseData, float> pair in basePrices)
        {
            if (pair.Key != null)
                pair.Key.priceInGold = pair.Value;
        }
    }
}
