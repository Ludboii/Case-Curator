using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MuseumWingCardUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private MuseumProgressBarUI progressBar;
    [SerializeField] private GameObject lockedRoot;
    [SerializeField] private TMP_Text lockedText;

    private MuseumWingEntry entry;
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

    public void Setup(MuseumWingEntry museumEntry, MuseumPanelUI panel)
    {
        ResolveProgressBar();

        entry = museumEntry;
        owner = panel;

        MuseumWingConfig config = entry != null ? entry.config : null;
        bool unlocked = MuseumUnlockProgressionUtility.IsWingUnlocked(
            entry,
            GetDatabase(),
            out string lockedReason);

        if (titleText != null)
            titleText.text = entry != null ? entry.DisplayName : "Museum Wing";

        if (descriptionText != null)
        {
            string description = config != null
                ? config.description
                : "";

            descriptionText.text = !unlocked && lockedText == null
                ? AppendLine(description, lockedReason)
                : description;
        }

        if (iconImage != null)
        {
            // Use an explicitly configured wing icon when one exists. Otherwise
            // derive a representative image from the first available category,
            // weapon and skin contained by the generated wing.
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

        if (lockedRoot != null)
            lockedRoot.SetActive(!unlocked);

        if (lockedText != null)
            lockedText.text = unlocked ? "" : lockedReason;

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
            button.interactable = unlocked && entry != null;
        }

        MuseumLockVisualUtility.Apply(gameObject, unlocked);
    }

    private static Sprite GetRepresentativeIcon(MuseumWingEntry wing)
    {
        if (wing == null || wing.categories == null)
            return null;

        for (int categoryIndex = 0;
             categoryIndex < wing.categories.Count;
             categoryIndex++)
        {
            MuseumCategoryEntry category = wing.categories[categoryIndex];

            if (category == null || category.weapons == null)
                continue;

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
        }

        return null;
    }

    private void ResolveProgressBar()
    {
        if (progressBar == null)
            progressBar = GetComponentInChildren<MuseumProgressBarUI>(true);
    }

    private GameDatabase GetDatabase()
    {
        return owner != null && owner.Service != null
            ? owner.Service.Database
            : SaveManager.Instance != null
                ? SaveManager.Instance.database
                : null;
    }

    private static string AppendLine(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first))
            return second ?? "";

        if (string.IsNullOrWhiteSpace(second))
            return first;

        return first + "\n" + second;
    }

    private void HandleClicked()
    {
        if (owner == null || entry == null)
            return;

        if (!MuseumUnlockProgressionUtility.IsWingUnlocked(
                entry,
                GetDatabase(),
                out string lockedReason))
        {
            owner.ShowMuseumMessage(lockedReason);
            return;
        }

        owner.OpenWing(entry);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }
}
