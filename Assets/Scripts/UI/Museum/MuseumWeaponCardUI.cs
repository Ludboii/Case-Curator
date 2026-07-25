using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MuseumWeaponCardUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text skinCountText;
    [SerializeField] private TMP_Text donationStateText;
    [SerializeField] private MuseumProgressBarUI progressBar;

    [Header("Donation Indicator Colors")]
    [SerializeField] private Color readyTextColor =
        new Color(1f, 0.9f, 0.25f, 1f);
    [SerializeField] private Color protectedTextColor =
        new Color(1f, 0.65f, 0.25f, 1f);

    private MuseumWeaponEntry entry;
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

    public void Setup(MuseumWeaponEntry museumEntry, MuseumPanelUI panel)
    {
        ResolveReferences();

        entry = museumEntry;
        owner = panel;

        if (titleText != null)
            titleText.text = entry != null ? entry.weaponName : "Weapon";

        int skinCount = entry != null && entry.skins != null
            ? entry.skins.Count
            : 0;
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

        if (skinCountText != null)
        {
            string baseText = $"{skinCount} skins";
            skinCountText.text = donationStateText == null &&
                                 !string.IsNullOrWhiteSpace(donationStatus)
                ? baseText + "\n" + donationStatus
                : baseText;
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
            Sprite icon = GetRepresentativeIcon(entry);
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

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
            button.interactable = entry != null;
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
            owner.OpenWeapon(entry);
    }

    private static Sprite GetRepresentativeIcon(MuseumWeaponEntry weapon)
    {
        if (weapon == null || weapon.skins == null)
            return null;

        for (int i = 0; i < weapon.skins.Count; i++)
        {
            SkinData skin = weapon.skins[i] != null
                ? weapon.skins[i].skin
                : null;

            if (skin != null && skin.icon != null)
                return skin.icon;
        }

        return null;
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }
}