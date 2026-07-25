using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Horizontal free-scroll Trophy Room controller. Arrow buttons move the viewport
/// without snapping, while each unlocked empty pedestal keeps its own add button.
/// </summary>
public sealed class TrophyRoomPanelUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button closeButton;

    [Header("Summary")]
    [SerializeField] private TMP_Text totalPowerText;
    [SerializeField] private TMP_Text activeFocusText;
    [SerializeField] private TMP_Text activeBonusText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Dropdown focusDropdown;

    [Header("Horizontal Pedestals")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform pedestalContent;
    [SerializeField] private TrophyPedestalCardUI pedestalPrefab;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField, Range(0.01f, 1f)] private float arrowScrollStep = 0.12f;

    [Header("Selection")]
    [SerializeField] private TrophySelectionPopupUI selectionPopup;

    private readonly List<TrophyPedestalCardUI> cards =
        new List<TrophyPedestalCardUI>();

    private TrophyRoomService service;
    private bool subscribed;
    private bool suppressFocusCallback;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        BindButton(closeButton, Close);
        BindButton(previousButton, ScrollPrevious);
        BindButton(nextButton, ScrollNext);

        if (focusDropdown != null)
        {
            focusDropdown.onValueChanged.RemoveListener(HandleFocusChanged);
            focusDropdown.onValueChanged.AddListener(HandleFocusChanged);
        }

        BuildFocusOptions();
    }

    private void OnEnable()
    {
        ResolveService();
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        UnbindButton(closeButton, Close);
        UnbindButton(previousButton, ScrollPrevious);
        UnbindButton(nextButton, ScrollNext);

        if (focusDropdown != null)
            focusDropdown.onValueChanged.RemoveListener(HandleFocusChanged);
    }

    public void Open()
    {
        ResolveService();

        if (root == null)
            root = gameObject;

        root.SetActive(true);
        Subscribe();
        SetResult("");
        Refresh();
    }

    public void Close()
    {
        if (selectionPopup != null)
            selectionPopup.Close();

        Unsubscribe();

        if (root != null)
            root.SetActive(false);
    }

    public void Refresh()
    {
        ResolveService();

        if (service == null)
        {
            SetResult("Trophy Room service is unavailable.");
            return;
        }

        TrophyRoomSnapshot snapshot = service.GetSnapshot();
        EnsurePedestalCards();

        for (int i = 0; i < cards.Count; i++)
        {
            TrophyRoomSlotSnapshot slot = i < snapshot.slots.Count
                ? snapshot.slots[i]
                : null;
            cards[i].Bind(slot, this);
        }

        if (totalPowerText != null)
        {
            totalPowerText.text =
                $"TOTAL TROPHY POWER: {snapshot.totalWeightedPower:N0}";
        }

        if (activeFocusText != null)
            activeFocusText.text = GetFocusDisplayName(snapshot.focus);

        if (activeBonusText != null)
        {
            activeBonusText.text = FormatFocusBonus(
                snapshot.focus,
                snapshot.activeBonusFraction);
        }

        if (focusDropdown != null)
        {
            suppressFocusCallback = true;
            focusDropdown.SetValueWithoutNotify((int)snapshot.focus);
            suppressFocusCallback = false;
        }

        if (previousButton != null)
            previousButton.interactable = snapshot.unlockedSlotCount > 0;

        if (nextButton != null)
            nextButton.interactable = snapshot.unlockedSlotCount > 0;
    }

    public void OpenSelection(int zeroBasedSlotIndex)
    {
        ResolveService();

        if (service == null || selectionPopup == null)
            return;

        selectionPopup.Open(zeroBasedSlotIndex, service, this);
    }

    public void RemoveFromPedestal(int zeroBasedSlotIndex)
    {
        ResolveService();

        TrophyRoomOperationResult result = service != null
            ? service.RemoveFromPedestal(zeroBasedSlotIndex)
            : TrophyRoomOperationResult.Failed(
                "Trophy Room service is unavailable.");

        SetResult(result.message);
        Refresh();
    }

    public void NotifySelectionCompleted(TrophyRoomOperationResult result)
    {
        SetResult(result != null ? result.message : "Trophy Room action failed.");
        Refresh();
    }

    private void EnsurePedestalCards()
    {
        if (pedestalContent == null || pedestalPrefab == null)
            return;

        while (cards.Count < TrophyRoomUpgradeUtility.MaximumPedestalCount)
        {
            TrophyPedestalCardUI card = Instantiate(
                pedestalPrefab,
                pedestalContent);
            card.gameObject.SetActive(true);
            cards.Add(card);
        }

        while (cards.Count > TrophyRoomUpgradeUtility.MaximumPedestalCount)
        {
            int last = cards.Count - 1;
            TrophyPedestalCardUI card = cards[last];
            cards.RemoveAt(last);

            if (card != null)
                Destroy(card.gameObject);
        }
    }

    private void BuildFocusOptions()
    {
        if (focusDropdown == null)
            return;

        focusDropdown.ClearOptions();
        focusDropdown.AddOptions(new List<string>
        {
            "Museum Gold Income",
            "Museum Diamond Income",
            "Automated Acquisitions",
            "Gift Retrievals"
        });
    }

    private void HandleFocusChanged(int value)
    {
        if (suppressFocusCallback)
            return;

        ResolveService();

        if (service == null)
            return;

        TrophyRoomFocus focus = (TrophyRoomFocus)Mathf.Clamp(
            value,
            0,
            Enum.GetValues(typeof(TrophyRoomFocus)).Length - 1);

        if (service.SetFocus(focus))
        {
            SetResult($"Trophy focus changed to {GetFocusDisplayName(focus)}.");
        }

        Refresh();
    }

    private void ScrollPrevious()
    {
        ScrollBy(-Mathf.Abs(arrowScrollStep));
    }

    private void ScrollNext()
    {
        ScrollBy(Mathf.Abs(arrowScrollStep));
    }

    private void ScrollBy(float amount)
    {
        if (scrollRect == null)
            return;

        scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(
            scrollRect.horizontalNormalizedPosition + amount);
    }

    private void ResolveService()
    {
        if (service == null && SaveManager.Instance != null)
            service = TrophyRoomService.GetOrCreate();
    }

    private void Subscribe()
    {
        if (service == null || subscribed)
            return;

        service.OnTrophyRoomChanged += Refresh;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (service != null && subscribed)
            service.OnTrophyRoomChanged -= Refresh;

        subscribed = false;
    }

    private void SetResult(string message)
    {
        if (resultText == null)
            return;

        resultText.text = message ?? "";
        resultText.gameObject.SetActive(
            !string.IsNullOrWhiteSpace(resultText.text));
    }

    public static string GetFocusDisplayName(TrophyRoomFocus focus)
    {
        switch (focus)
        {
            case TrophyRoomFocus.MuseumDiamondIncome:
                return "Museum Diamond Income";
            case TrophyRoomFocus.AutomatedAcquisitions:
                return "Automated Acquisitions";
            case TrophyRoomFocus.GiftRetrievals:
                return "Gift Retrievals";
            default:
                return "Museum Gold Income";
        }
    }

    public static string FormatFocusBonus(
        TrophyRoomFocus focus,
        double bonusFraction)
    {
        double percent = Math.Max(0d, bonusFraction) * 100d;

        switch (focus)
        {
            case TrophyRoomFocus.AutomatedAcquisitions:
                return $"{percent:0.##}% faster acquisition time";
            case TrophyRoomFocus.GiftRetrievals:
                return $"{percent:0.##}% shorter retrieval cooldown";
            case TrophyRoomFocus.MuseumDiamondIncome:
                return $"+{percent:0.##}% Museum Diamond income";
            default:
                return $"+{percent:0.##}% Museum Gold income";
        }
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
