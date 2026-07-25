using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays Museum visitor income, its M5.1 upgrade effects and explicit claim
/// controls. Currency mutation remains owned by MuseumIdleIncomeService.
/// </summary>
public sealed class MuseumIdleIncomePopupUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Summary")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text nodeText;
    [SerializeField] private TMP_Text offlineText;
    [SerializeField] private TMP_Text upgradeText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text resultText;

    [Header("Gold")]
    [SerializeField] private TMP_Text goldRateText;
    [SerializeField] private TMP_Text goldStoredText;
    [SerializeField] private Button claimGoldButton;
    [SerializeField] private TMP_Text claimGoldButtonText;

    [Header("Diamonds")]
    [SerializeField] private TMP_Text diamondRateText;
    [SerializeField] private TMP_Text diamondStoredText;
    [SerializeField] private Button claimDiamondButton;
    [SerializeField] private TMP_Text claimDiamondButtonText;

    [Header("Actions")]
    [SerializeField] private Button claimAllButton;
    [SerializeField] private TMP_Text claimAllButtonText;
    [SerializeField] private Button closeButton;

    private MuseumIdleIncomeService service;
    private bool subscribed;
    private float nextRefreshTime;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        BindButton(claimGoldButton, ClaimGold);
        BindButton(claimDiamondButton, ClaimDiamonds);
        BindButton(claimAllButton, ClaimAll);
        BindButton(closeButton, Close);
    }

    private void OnEnable()
    {
        ResolveService();
        Subscribe();
        Refresh();
    }

    private void Update()
    {
        if (!IsOpen || Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + 1f;
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        UnbindButton(claimGoldButton, ClaimGold);
        UnbindButton(claimDiamondButton, ClaimDiamonds);
        UnbindButton(claimAllButton, ClaimAll);
        UnbindButton(closeButton, Close);
    }

    public void Open()
    {
        ResolveService();

        if (root == null)
            root = gameObject;

        root.SetActive(true);
        Subscribe();
        SetResult("");

        if (service != null)
            service.ProcessElapsedTimeNow(true);

        Refresh();
    }

    public void Close()
    {
        Unsubscribe();

        if (root != null)
            root.SetActive(false);
    }

    public void Refresh()
    {
        ResolveService();

        if (service == null)
        {
            SetUnavailable();
            return;
        }

        MuseumIdleIncomeSnapshot snapshot = service.GetSnapshot(true);

        if (titleText != null)
            titleText.text = "Museum Visitor Income";

        if (nodeText != null)
        {
            nodeText.text = snapshot.goldUnlocked
                ? $"Income nodes: {snapshot.claimedGoldNodeCount:N0} " +
                  $"(rate weight x{snapshot.claimedGoldNodeWeight:0.##})"
                : "Claim a passive-income Staircase milestone to begin.";
        }

        if (offlineText != null)
        {
            offlineText.text = snapshot.maximumOfflineHours > 0d
                ? $"Offline cap: {FormatHours(snapshot.maximumOfflineHours)}"
                : "Offline cap: unlimited";
        }

        if (upgradeText != null)
        {
            upgradeText.text =
                $"Income upgrade: x{snapshot.incomeMultiplier:0.##}\n" +
                $"Offline upgrade: +{snapshot.offlineHoursUpgradeBonus:0.#}h\n" +
                $"Gold cap upgrade: x{snapshot.goldCapacityMultiplier:0.##}\n" +
                $"Diamond cap upgrade: x{snapshot.diamondCapacityMultiplier:0.##}";
        }

        if (goldRateText != null)
        {
            goldRateText.text = snapshot.goldUnlocked
                ? $"{snapshot.goldPerHour:N2} Gold per hour"
                : "Visitor Gold locked";
        }

        if (goldStoredText != null)
        {
            goldStoredText.text = snapshot.goldCapacity > 0d
                ? $"{snapshot.unclaimedGold:N2} / " +
                  $"{snapshot.goldCapacity:N0} Gold stored"
                : $"{snapshot.unclaimedGold:N2} Gold stored";
        }

        if (diamondRateText != null)
        {
            diamondRateText.text = snapshot.diamondsUnlocked
                ? $"{snapshot.diamondsPerHour:0.###} Diamonds per hour"
                : "Diamond Endowment unlocks at Step 75";
        }

        if (diamondStoredText != null)
        {
            diamondStoredText.text = snapshot.diamondCapacity > 0d
                ? $"{snapshot.unclaimedDiamonds:0.###} / " +
                  $"{snapshot.diamondCapacity:0.###} Diamonds stored"
                : $"{snapshot.unclaimedDiamonds:0.###} Diamonds stored";
        }

        SetButton(
            claimGoldButton,
            claimGoldButtonText,
            snapshot.CanClaimGold,
            snapshot.CanClaimGold
                ? $"CLAIM {snapshot.unclaimedGold:N2} GOLD"
                : snapshot.goldUnlocked ? "NO GOLD READY" : "LOCKED");

        SetButton(
            claimDiamondButton,
            claimDiamondButtonText,
            snapshot.CanClaimDiamonds,
            snapshot.CanClaimDiamonds
                ? $"CLAIM {snapshot.ClaimableWholeDiamonds:N0} DIAMONDS"
                : snapshot.diamondsUnlocked
                    ? "NO WHOLE DIAMONDS READY"
                    : "LOCKED");

        SetButton(
            claimAllButton,
            claimAllButtonText,
            snapshot.CanClaimAnything,
            snapshot.CanClaimAnything ? "CLAIM ALL" : "NOTHING READY");

        if (statusText != null)
            statusText.text = GetStatus(snapshot);
    }

    private void ClaimGold()
    {
        Claim(service != null
            ? service.ClaimGold()
            : MuseumIdleIncomeClaimResult.Empty(
                "Museum income is unavailable."));
    }

    private void ClaimDiamonds()
    {
        Claim(service != null
            ? service.ClaimDiamonds()
            : MuseumIdleIncomeClaimResult.Empty(
                "Museum income is unavailable."));
    }

    private void ClaimAll()
    {
        Claim(service != null
            ? service.ClaimAll()
            : MuseumIdleIncomeClaimResult.Empty(
                "Museum income is unavailable."));
    }

    private void Claim(MuseumIdleIncomeClaimResult result)
    {
        SetResult(result != null ? result.message : "Claim failed.");
        Refresh();
    }

    private void ResolveService()
    {
        if (service == null && SaveManager.Instance != null)
            service = MuseumIdleIncomeService.GetOrCreate();
    }

    private void Subscribe()
    {
        if (service == null || subscribed)
            return;

        service.OnIdleIncomeChanged += Refresh;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (service != null && subscribed)
            service.OnIdleIncomeChanged -= Refresh;

        subscribed = false;
    }

    private void SetUnavailable()
    {
        if (titleText != null)
            titleText.text = "Museum Visitor Income";

        if (statusText != null)
            statusText.text = "Museum income service is unavailable.";

        if (upgradeText != null)
            upgradeText.text = "Museum upgrades unavailable";

        SetButton(
            claimGoldButton,
            claimGoldButtonText,
            false,
            "UNAVAILABLE");

        SetButton(
            claimDiamondButton,
            claimDiamondButtonText,
            false,
            "UNAVAILABLE");

        SetButton(
            claimAllButton,
            claimAllButtonText,
            false,
            "UNAVAILABLE");
    }

    private void SetResult(string message)
    {
        if (resultText == null)
            return;

        resultText.text = message ?? "";
        resultText.gameObject.SetActive(
            !string.IsNullOrWhiteSpace(resultText.text));
    }

    private static string GetStatus(MuseumIdleIncomeSnapshot snapshot)
    {
        if (snapshot.goldAtCapacity && snapshot.diamondsAtCapacity)
        {
            return "Gold and Diamond storage are full. " +
                   "Claim to resume generation.";
        }

        if (snapshot.goldAtCapacity)
        {
            return "Visitor Gold storage is full. " +
                   "Claim to resume generation.";
        }

        if (snapshot.diamondsAtCapacity)
        {
            return "Diamond storage is full. " +
                   "Claim to resume generation.";
        }

        if (!snapshot.goldUnlocked)
            return "Passive Museum income has not been unlocked.";

        return "Income continues while the game is closed.";
    }

    private static string FormatHours(double hours)
    {
        double rounded = Math.Round(hours, 1);
        string unit = Math.Abs(rounded - 1d) < 0.0001d
            ? "hour"
            : "hours";

        return $"{rounded:0.#} {unit}";
    }

    private static void SetButton(
        Button button,
        TMP_Text label,
        bool interactable,
        string text)
    {
        if (button != null)
            button.interactable = interactable;

        if (label != null)
            label.text = text ?? "";
    }

    private static void BindButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void UnbindButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }
}
