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
    [SerializeField] private GameObject lockedRoot;
    [SerializeField] private TMP_Text lockedText;

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

        bool unlocked = MuseumUnlockProgressionUtility.IsWeaponUnlocked(
            entry,
            GetDatabase(),
            out string lockedReason);

        if (titleText != null)
            titleText.text = entry != null ? entry.weaponName : "Weapon";

        int skinCount = entry != null && entry.skins != null
            ? entry.skins.Count
            : 0;
        int readyCount = 0;
        int protectedCount = 0;

        if (unlocked)
        {
            MuseumDonationAvailabilityUtility.Count(
                entry,
                owner != null ? owner.Service : null,
                out readyCount,
                out protectedCount);
        }

        string donationStatus = unlocked
            ? MuseumDonationAvailabilityUtility.GetStatusText(
                readyCount,
                protectedCount)
            : "";

        bool sharedProgressText =
            progressBar != null &&
            donationStateText != null &&
            donationStateText == progressBar.ProgressText;

        if (skinCountText != null)
        {
            string baseText = $"{skinCount} skins";

            if (!unlocked && lockedText == null)
                skinCountText.text = baseText + "\n" + lockedReason;
            else if (donationStateText == null &&
                     !string.IsNullOrWhiteSpace(donationStatus))
                skinCountText.text = baseText + "\n" + donationStatus;
            else
                skinCountText.text = baseText;
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
                sharedProgressText && unlocked ? donationStatus : null);
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

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];

            if (text == null)
                continue;

            string objectName = text.gameObject.name.ToLowerInvariant();

            if (donationStateText == null &&
                (objectName.Contains("donation") ||
                 objectName.Contains("ready") ||
                 objectName.Contains("indicator")))
            {
                donationStateText = text;
            }

            if (lockedText == null &&
                (objectName.Contains("locked") ||
                 objectName.Contains("unlock")))
            {
                lockedText = text;
            }
        }

        if (lockedRoot == null && lockedText != null)
            lockedRoot = lockedText.gameObject;
    }

    private GameDatabase GetDatabase()
    {
        return owner != null && owner.Service != null
            ? owner.Service.Database
            : SaveManager.Instance != null
                ? SaveManager.Instance.database
                : null;
    }

    private void HandleClicked()
    {
        if (owner == null || entry == null)
            return;

        if (!MuseumUnlockProgressionUtility.IsWeaponUnlocked(
                entry,
                GetDatabase(),
                out string lockedReason))
        {
            owner.ShowMuseumMessage(lockedReason);
            return;
        }

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
