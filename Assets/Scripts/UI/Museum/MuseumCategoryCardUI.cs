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
    [SerializeField] private TMP_Text donationStateText;
    [SerializeField] private MuseumProgressBarUI progressBar;
    [SerializeField] private GameObject lockedRoot;
    [SerializeField] private TMP_Text lockedText;

    [Header("Donation Indicator Colors")]
    [SerializeField] private Color readyTextColor =
        new Color(1f, 0.9f, 0.25f, 1f);
    [SerializeField] private Color protectedTextColor =
        new Color(1f, 0.65f, 0.25f, 1f);

    private MuseumCategoryEntry entry;
    private MuseumPanelUI owner;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    public void Setup(MuseumCategoryEntry museumEntry, MuseumPanelUI panel)
    {
        ResolveReferences();

        entry = museumEntry;
        owner = panel;

        MuseumCategoryConfig config = entry != null ? entry.config : null;

        if (titleText != null)
            titleText.text = entry != null ? entry.DisplayName : "Category";

        string description = config != null ? config.description : "";
        MuseumDonationAvailabilityUtility.Count(
            entry,
            owner != null ? owner.Service : null,
            out int readyCount,
            out int protectedCount);
        string donationStatus =
            MuseumDonationAvailabilityUtility.GetStatusText(
                readyCount,
                protectedCount);

        bool sharedProgressText =
            progressBar != null &&
            donationStateText != null &&
            donationStateText == progressBar.ProgressText;

        if (descriptionText != null)
        {
            descriptionText.text = donationStateText == null &&
                                   !string.IsNullOrWhiteSpace(donationStatus)
                ? string.IsNullOrWhiteSpace(description)
                    ? donationStatus
                    : description + "\n" + donationStatus
                : description;
        }

        if (!sharedProgressText)
        {
            ApplyDonationIndicator(
                donationStatus,
                readyCount,
                protectedCount);
        }

        if (iconImage != null)
        {
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
                entry != null ? entry.totalSlots : 0,
                sharedProgressText ? donationStatus : null);
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

    private void ApplyDonationIndicator(
        string status,
        int readyCount,
        int protectedCount)
    {
        if (donationStateText == null)
            return;

        donationStateText.text = status;
        donationStateText.gameObject.SetActive(
            !string.IsNullOrWhiteSpace(status));

        if (readyCount > 0)
            donationStateText.color = readyTextColor;
        else if (protectedCount > 0)
            donationStateText.color = protectedTextColor;
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

    private void ResolveReferences()
    {
        if (progressBar == null)
            progressBar = GetComponentInChildren<MuseumProgressBarUI>(true);

        if (donationStateText != null)
            return;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];

            if (text == null)
                continue;

            string objectName = text.gameObject.name.ToLowerInvariant();

            if (objectName.Contains("donation") ||
                objectName.Contains("ready") ||
                objectName.Contains("indicator"))
            {
                donationStateText = text;
                break;
            }
        }
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