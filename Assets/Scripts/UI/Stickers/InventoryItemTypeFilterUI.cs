using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum InventoryItemTypeFilter
{
    All,
    SkinsOnly,
    StickersOnly
}

/// <summary>
/// Additive filter-panel companion. It keeps stickers and skins in the same
/// capacity/storage inventory while allowing the grid to show all, skins only,
/// or stickers only.
/// </summary>
public sealed class InventoryItemTypeFilterUI : MonoBehaviour
{
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private Transform gridContent;
    [SerializeField] private Button allButton;
    [SerializeField] private Button skinsButton;
    [SerializeField] private Button stickersButton;
    [SerializeField] private TMP_Text filterStatusText;
    [SerializeField] private InventoryItemTypeFilter currentFilter =
        InventoryItemTypeFilter.All;

    private readonly Dictionary<InventoryItemCardUI, CardVisibilityState> states =
        new Dictionary<InventoryItemCardUI, CardVisibilityState>();
    private float nextApplyAt;

    private sealed class CardVisibilityState
    {
        public CanvasGroup canvasGroup;
        public LayoutElement layoutElement;
        public bool hiddenByFilter;
    }

    private void Awake()
    {
        if (inventoryUI == null)
            inventoryUI = GetComponentInParent<InventoryUI>(true);
        if (gridContent == null && inventoryUI != null)
            gridContent = inventoryUI.gridContent;

        SetupButton(allButton, () => SetFilter(InventoryItemTypeFilter.All));
        SetupButton(
            skinsButton,
            () => SetFilter(InventoryItemTypeFilter.SkinsOnly));
        SetupButton(
            stickersButton,
            () => SetFilter(InventoryItemTypeFilter.StickersOnly));
    }

    private void OnEnable()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= ScheduleApply;
            InventoryManager.Instance.OnInventoryChanged += ScheduleApply;
        }

        ScheduleApply();
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= ScheduleApply;

        RestoreAll();
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime >= nextApplyAt)
        {
            nextApplyAt = float.PositiveInfinity;
            ApplyFilter();
        }
    }

    public void SetFilter(InventoryItemTypeFilter filter)
    {
        currentFilter = filter;
        ScheduleApply();
    }

    public void ResetFilter()
    {
        SetFilter(InventoryItemTypeFilter.All);
    }

    private void ScheduleApply()
    {
        nextApplyAt = Time.unscaledTime;
    }

    private void ApplyFilter()
    {
        if (gridContent == null)
            return;

        InventoryItemCardUI[] cards =
            gridContent.GetComponentsInChildren<InventoryItemCardUI>(true);
        int visible = 0;

        for (int i = 0; i < cards.Length; i++)
        {
            InventoryItemCardUI card = cards[i];

            if (card == null)
                continue;

            CardVisibilityState state = GetState(card);
            InventoryItem item = card.CurrentItem;
            bool isSticker = StickerItemUtility.IsSticker(item);
            bool shouldShow = item != null &&
                              (currentFilter == InventoryItemTypeFilter.All ||
                               (currentFilter == InventoryItemTypeFilter.SkinsOnly &&
                                !isSticker) ||
                               (currentFilter == InventoryItemTypeFilter.StickersOnly &&
                                isSticker));

            if (!card.gameObject.activeSelf)
            {
                RestoreState(state);
                continue;
            }

            ApplyState(state, !shouldShow);

            if (shouldShow)
                visible++;
        }

        if (filterStatusText != null)
        {
            switch (currentFilter)
            {
                case InventoryItemTypeFilter.SkinsOnly:
                    filterStatusText.text = $"SKINS ONLY • {visible:N0}";
                    break;
                case InventoryItemTypeFilter.StickersOnly:
                    filterStatusText.text = $"STICKERS ONLY • {visible:N0}";
                    break;
                default:
                    filterStatusText.text = $"ALL ITEMS • {visible:N0}";
                    break;
            }
        }
    }

    private CardVisibilityState GetState(InventoryItemCardUI card)
    {
        if (states.TryGetValue(card, out CardVisibilityState state))
            return state;

        CanvasGroup canvas = card.GetComponent<CanvasGroup>();

        if (canvas == null)
            canvas = card.gameObject.AddComponent<CanvasGroup>();

        LayoutElement layout = card.GetComponent<LayoutElement>();

        if (layout == null)
            layout = card.gameObject.AddComponent<LayoutElement>();

        state = new CardVisibilityState
        {
            canvasGroup = canvas,
            layoutElement = layout
        };
        states.Add(card, state);
        return state;
    }

    private static void ApplyState(CardVisibilityState state, bool hidden)
    {
        if (state == null)
            return;

        state.hiddenByFilter = hidden;

        if (state.canvasGroup != null)
        {
            state.canvasGroup.alpha = hidden ? 0f : 1f;
            state.canvasGroup.interactable = !hidden;
            state.canvasGroup.blocksRaycasts = !hidden;
        }

        if (state.layoutElement != null)
            state.layoutElement.ignoreLayout = hidden;
    }

    private void RestoreAll()
    {
        foreach (CardVisibilityState state in states.Values)
            RestoreState(state);
    }

    private static void RestoreState(CardVisibilityState state)
    {
        if (state == null || !state.hiddenByFilter)
            return;

        ApplyState(state, false);
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
}
