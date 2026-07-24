using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MuseumCategoryCardUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private MuseumProgressBarUI progressBar;
    [SerializeField] private GameObject lockedRoot;
    [SerializeField] private TMP_Text lockedText;

    private MuseumCategoryEntry entry;
    private MuseumPanelUI owner;

    private void Awake()
    {
        ResolveProgressBar();
    }

    private void Reset()
    {
        ResolveProgressBar();
    }

    private void OnValidate()
    {
        ResolveProgressBar();
    }

    public void Setup(MuseumCategoryEntry museumEntry, MuseumPanelUI panel)
    {
        ResolveProgressBar();

        entry = museumEntry;
        owner = panel;

        MuseumCategoryConfig config = entry != null ? entry.config : null;

        if (titleText != null)
            titleText.text = entry != null ? entry.DisplayName : "Category";

        if (descriptionText != null)
        {
            descriptionText.text = config != null
                ? config.description
                : "";
        }

        if (iconImage != null)
        {
            // Respect a deliberately assigned category icon. When no icon has
            // been configured, use the first available skin icon from the
            // first weapon in this category so generated categories are never
            // left as empty black cards.
            Sprite icon = config != null && config.icon != null
                ? config.icon
                : GetRepresentativeIcon(entry);

            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.preserveAspect = true;
        }

        if (progressBar != null)
        {
            progressBar.SetProgress(
                entry != null ? entry.donatedSlots : 0,
                entry != null ? entry.totalSlots : 0);
        }

        bool unlocked = config == null ||
                        config.unlockDefinition == null ||
                        UnlockEvaluator.IsUnlocked(config.unlockDefinition);

        if (lockedRoot != null)
            lockedRoot.SetActive(!unlocked);

        if (lockedText != null)
            lockedText.text = unlocked ? "" : "Locked";

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
            button.interactable = unlocked && entry != null;
        }
    }

    private static Sprite GetRepresentativeIcon(MuseumCategoryEntry category)
    {
        if (category == null || category.weapons == null)
            return null;

        for (int weaponIndex = 0;
             weaponIndex < category.weapons.Count;
             weaponIndex++)
        {
            MuseumWeaponEntry weapon = category.weapons[weaponIndex];

            if (weapon == null || weapon.skins == null)
                continue;

            for (int skinIndex = 0;
                 skinIndex < weapon.skins.Count;
                 skinIndex++)
            {
                SkinData skin = weapon.skins[skinIndex] != null
                    ? weapon.skins[skinIndex].skin
                    : null;

                if (skin != null && skin.icon != null)
                    return skin.icon;
            }
        }

        return null;
    }

    private void ResolveProgressBar()
    {
        if (progressBar == null)
            progressBar = GetComponentInChildren<MuseumProgressBarUI>(true);
    }

    private void HandleClicked()
    {
        if (owner != null && entry != null)
            owner.OpenCategory(entry);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }
}
