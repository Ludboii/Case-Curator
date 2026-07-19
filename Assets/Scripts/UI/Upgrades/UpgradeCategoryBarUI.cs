using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum UpgradePanelCategory
{
    All = 0,
    Opening = 1,
    Inventory = 2,
    Museum = 3,
    Minigames = 4,
    SingleUse = 5,
    Others = 6
}

/// <summary>
/// Controls the Case Shop-style category header with previous/next arrows.
/// </summary>
[DisallowMultipleComponent]
public class UpgradeCategoryBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text categoryNameText;

    [Header("Display")]
    [SerializeField] private bool showItemCount = true;
    [SerializeField] private UpgradePanelCategory startingCategory =
        UpgradePanelCategory.All;

    public event Action<UpgradePanelCategory> OnCategoryChanged;

    public UpgradePanelCategory CurrentCategory { get; private set; }

    private int currentItemCount;

    private void Awake()
    {
        CurrentCategory = startingCategory;

        if (previousButton != null)
            previousButton.onClick.AddListener(ShowPreviousCategory);

        if (nextButton != null)
            nextButton.onClick.AddListener(ShowNextCategory);

        RefreshLabel();
    }

    private void OnDestroy()
    {
        if (previousButton != null)
            previousButton.onClick.RemoveListener(ShowPreviousCategory);

        if (nextButton != null)
            nextButton.onClick.RemoveListener(ShowNextCategory);
    }

    public void ShowPreviousCategory()
    {
        int categoryCount = Enum.GetValues(
            typeof(UpgradePanelCategory)).Length;

        int nextIndex = ((int)CurrentCategory - 1 + categoryCount) %
                        categoryCount;

        SetCategory((UpgradePanelCategory)nextIndex, true);
    }

    public void ShowNextCategory()
    {
        int categoryCount = Enum.GetValues(
            typeof(UpgradePanelCategory)).Length;

        int nextIndex = ((int)CurrentCategory + 1) % categoryCount;
        SetCategory((UpgradePanelCategory)nextIndex, true);
    }

    public void SetCategory(
        UpgradePanelCategory category,
        bool notify = false)
    {
        bool changed = CurrentCategory != category;
        CurrentCategory = category;
        RefreshLabel();

        if (notify && changed)
            OnCategoryChanged?.Invoke(CurrentCategory);
    }

    public void SetItemCount(int count)
    {
        currentItemCount = Mathf.Max(0, count);
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (categoryNameText == null)
            return;

        string label = GetDisplayName(CurrentCategory);

        categoryNameText.text = showItemCount
            ? $"{label} ({currentItemCount})"
            : label;
    }

    public static string GetDisplayName(
        UpgradePanelCategory category)
    {
        switch (category)
        {
            case UpgradePanelCategory.SingleUse:
                return "SINGLE USE";

            default:
                return category.ToString().ToUpperInvariant();
        }
    }
}
