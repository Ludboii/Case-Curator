using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps the rarity filter button visuals synchronized with InventoryUI's
/// multi-select Only/Hide behaviour. Add this to the rarity-filter panel root.
/// </summary>
[DisallowMultipleComponent]
public class InventoryRarityFilterVisualController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private Button resetButton;

    [Header("Discovery")]
    [SerializeField] private bool findButtonsInChildren = true;
    [SerializeField] private List<InventoryRarityFilterButtonUI> filterButtons =
        new List<InventoryRarityFilterButtonUI>();

    private readonly Dictionary<InventoryRarityFilterButtonUI, UnityEngine.Events.UnityAction>
        listeners = new Dictionary<InventoryRarityFilterButtonUI, UnityEngine.Events.UnityAction>();

    private void Awake()
    {
        if (inventoryUI == null)
            inventoryUI = GetComponentInParent<InventoryUI>(true);

        if (findButtonsInChildren)
        {
            filterButtons.Clear();
            filterButtons.AddRange(
                GetComponentsInChildren<InventoryRarityFilterButtonUI>(true));
        }

        HookButtons();

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetVisualState);

        RefreshAllVisuals();
    }

    private void OnDestroy()
    {
        foreach (KeyValuePair<InventoryRarityFilterButtonUI, UnityEngine.Events.UnityAction> pair
                 in listeners)
        {
            if (pair.Key != null && pair.Key.Button != null)
                pair.Key.Button.onClick.RemoveListener(pair.Value);
        }

        listeners.Clear();

        if (resetButton != null)
            resetButton.onClick.RemoveListener(ResetVisualState);
    }

    private void HookButtons()
    {
        listeners.Clear();

        for (int i = 0; i < filterButtons.Count; i++)
        {
            InventoryRarityFilterButtonUI filterButton = filterButtons[i];

            if (filterButton == null || filterButton.Button == null)
                continue;

            InventoryRarityFilterButtonUI captured = filterButton;
            UnityEngine.Events.UnityAction action = () => HandleFilterClicked(captured);
            captured.Button.onClick.AddListener(action);
            listeners.Add(captured, action);
        }
    }

    private void HandleFilterClicked(InventoryRarityFilterButtonUI clicked)
    {
        if (clicked == null || inventoryUI == null)
            return;

        bool nextActive = !clicked.IsActive;

        if (nextActive)
        {
            // The same rarity cannot be both Only and Hide. Other rarities in
            // the same column remain active, preserving multi-select filters.
            for (int i = 0; i < filterButtons.Count; i++)
            {
                InventoryRarityFilterButtonUI other = filterButtons[i];

                if (other == null ||
                    other == clicked ||
                    other.rarity != clicked.rarity ||
                    other.mode == clicked.mode)
                {
                    continue;
                }

                other.SetActive(false);
            }
        }

        clicked.SetActive(nextActive);
        InvokeInventoryFilter(clicked);
    }

    private void InvokeInventoryFilter(InventoryRarityFilterButtonUI filterButton)
    {
        if (filterButton.mode == InventoryRarityFilterButtonMode.Only)
        {
            switch (filterButton.rarity)
            {
                case Rarity.Consumer: inventoryUI.OnlyConsumer(); break;
                case Rarity.Industrial: inventoryUI.OnlyIndustrial(); break;
                case Rarity.MilSpec: inventoryUI.OnlyMilSpec(); break;
                case Rarity.Restricted: inventoryUI.OnlyRestricted(); break;
                case Rarity.Classified: inventoryUI.OnlyClassified(); break;
                case Rarity.Covert: inventoryUI.OnlyCovert(); break;
                case Rarity.RareSpecial: inventoryUI.OnlyRareSpecial(); break;
            }
        }
        else
        {
            switch (filterButton.rarity)
            {
                case Rarity.Consumer: inventoryUI.HideConsumer(); break;
                case Rarity.Industrial: inventoryUI.HideIndustrial(); break;
                case Rarity.MilSpec: inventoryUI.HideMilSpec(); break;
                case Rarity.Restricted: inventoryUI.HideRestricted(); break;
                case Rarity.Classified: inventoryUI.HideClassified(); break;
                case Rarity.Covert: inventoryUI.HideCovert(); break;
                case Rarity.RareSpecial: inventoryUI.HideRareSpecial(); break;
            }
        }
    }

    public void ResetVisualState()
    {
        for (int i = 0; i < filterButtons.Count; i++)
        {
            if (filterButtons[i] != null)
                filterButtons[i].SetActive(false);
        }
    }

    public void RefreshAllVisuals()
    {
        for (int i = 0; i < filterButtons.Count; i++)
        {
            InventoryRarityFilterButtonUI filterButton = filterButtons[i];

            if (filterButton != null)
                filterButton.SetActive(filterButton.IsActive);
        }
    }
}
