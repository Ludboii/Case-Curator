using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orchestrates the complete Museum Present opening. The underlying
/// MuseumPresentService still owns balances and currency rolls; this layer adds
/// the required openable-container reward and exposes one result event that a
/// later case-opening animation can consume.
/// </summary>
public class MuseumPresentOpeningService : MonoBehaviour
{
    public static MuseumPresentOpeningService Instance { get; private set; }

    [SerializeField] private bool persistBetweenScenes = true;
    [SerializeField] private bool verboseLogging;

    public event Action<MuseumPresentOpenResult> OnPresentOpened;

    public static MuseumPresentOpeningService GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        MuseumPresentOpeningService existing =
            FindFirstObjectByType<MuseumPresentOpeningService>();

        if (existing != null)
            return existing;

        GameObject go = new GameObject("MuseumPresentOpeningService");
        return go.AddComponent<MuseumPresentOpeningService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistBetweenScenes)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public MuseumPresentOpenResult OpenPresent(MuseumPresentTier tier)
    {
        MuseumPresentService presentService =
            MuseumPresentService.GetOrCreate();

        if (presentService == null)
        {
            return MuseumPresentOpenResult.Failed(
                tier,
                "Museum Present service is unavailable.");
        }

        if (presentService.GetPresents(tier) <= 0)
        {
            return MuseumPresentOpenResult.Failed(
                tier,
                $"You do not own a {MuseumPresentUtility.GetTierDisplayName(tier)} Present.");
        }

        if (CaseInventoryManager.Instance == null)
        {
            return MuseumPresentOpenResult.Failed(
                tier,
                "Case inventory is unavailable, so the Present was not consumed.");
        }

        MuseumPresentTierConfig tierConfig =
            presentService.GetTierConfig(tier);
        IList<MuseumPresentContainerDrop> pool =
            ResolvePool(tier, tierConfig);
        MuseumPresentContainerDrop rolledDrop =
            MuseumPresentDropPoolUtility.Roll(pool);

        if (rolledDrop == null || rolledDrop.container == null)
        {
            return MuseumPresentOpenResult.Failed(
                tier,
                "This Present tier has no valid container drops. Populate its drop pool before opening it.");
        }

        // Roll the container before consuming the Present. The core service then
        // performs the authoritative balance mutation and currency/XP roll.
        int amount = UnityEngine.Random.Range(
            rolledDrop.minimumAmount,
            rolledDrop.maximumAmount + 1);

        MuseumPresentOpenResult result = presentService.OpenPresent(tier);

        if (result == null || !result.success)
            return result;

        CaseInventoryManager.Instance.AddCases(
            rolledDrop.container,
            amount);

        result.containerReward = rolledDrop.container;
        result.containerAmount = amount;
        result.message = BuildMessage(result);

        OnPresentOpened?.Invoke(result);

        if (verboseLogging)
            Debug.Log(result.message, this);

        return result;
    }

    public IReadOnlyList<MuseumPresentContainerDrop> GetResolvedPool(
        MuseumPresentTier tier)
    {
        MuseumPresentService service = MuseumPresentService.GetOrCreate();
        MuseumPresentTierConfig tierConfig = service.GetTierConfig(tier);
        IList<MuseumPresentContainerDrop> resolved = ResolvePool(tier, tierConfig);

        if (resolved is IReadOnlyList<MuseumPresentContainerDrop> readOnly)
            return readOnly;

        return new List<MuseumPresentContainerDrop>(resolved);
    }

    private static IList<MuseumPresentContainerDrop> ResolvePool(
        MuseumPresentTier tier,
        MuseumPresentTierConfig tierConfig)
    {
        if (tierConfig != null && tierConfig.ValidContainerDropCount > 0)
            return tierConfig.containerDrops;

        GameDatabase database =
            SaveManager.Instance != null
                ? SaveManager.Instance.database
                : null;

        return MuseumPresentDropPoolUtility.BuildDefaultPool(
            tier,
            database != null ? database.allCases : null);
    }

    private static string BuildMessage(MuseumPresentOpenResult result)
    {
        if (result == null || !result.success)
            return "Museum Present opening failed.";

        string tierName = MuseumPresentUtility.GetTierDisplayName(result.tier);
        string containerName = result.containerReward != null
            ? result.containerReward.caseName
            : "Unknown Container";
        string amountPrefix = result.containerAmount > 1
            ? $"{result.containerAmount}x "
            : "";
        string diamondLine = result.diamonds > 0
            ? $"\n+{result.diamonds:N0} Diamonds"
            : "";

        return
            $"Opened {tierName} Present\n" +
            $"Container drop: {amountPrefix}{containerName}\n" +
            $"+{result.gold:0.##} Gold\n" +
            $"+{result.xp:N0} XP" +
            diamondLine;
    }
}
