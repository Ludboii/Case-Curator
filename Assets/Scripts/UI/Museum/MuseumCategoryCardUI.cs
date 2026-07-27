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
    [SerializeField] private MuseumCompletionClaimOverlayUI completionClaimOverlay;

    [Header("Donation Indicator Colors")]
    [SerializeField] private Color completedTextColor =
        new Color(0.35f, 1f, 0.55f, 1f);
    [SerializeField] private Color readyTextColor =
        new Color(1f, 0.9f, 0.25f, 1f);
    [SerializeField] private Color protectedTextColor =
        new Color(1f, 0.65f, 0.25f, 1f);

    private MuseumCategoryEntry entry;
    private MuseumPanelUI owner;
    private MuseumCompletionRewardPreview completionPreview;

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
        bool unlocked = MuseumUnlockProgressionUtility.IsCategoryUnlocked(
            entry,
            GetDatabase(),
            out string lockedReason);

        completionPreview =
            MuseumCompletionRewardService.BuildCategoryPreview(
                entry,
                owner != null ? owner.Service : null);
        bool completed = completionPreview != null &&
                         completionPreview.completed;

        if (titleText != null)
            titleText.text = entry != null ? entry.DisplayName : "Category";

        string description = config != null ? config.description : "";
        int readyCount = 0;
        int protectedCount = 0;

        if (unlocked && !completed)
        {
            MuseumDonationAvailabilityUtility.Count(
                entry,
                owner != null ? owner.Service : null,
                out readyCount,
                out protectedCount);
        }

        string donationStatus = completed
            ? "COMPLETED"
            : unlocked
                ? MuseumDonationAvailabilityUtility.GetStatusText(
                    readyCount,
                    protectedCount)
                : "";

        bool sharedProgressText =
            progressBar != null &&
            donationStateText != null &&
            donationStateText == progressBar.ProgressText;

        if (descriptionText != null)
        {
            if (!unlocked && lockedText == null)
            {
                descriptionText.text = AppendLine(description, lockedReason);
            }
            else
            {
                descriptionText.text = donationStateText == null &&
                                       !string.IsNullOrWhiteSpace(donationStatus)
                    ? AppendLine(description, donationStatus)
                    : description;
            }
        }

        if (!sharedProgressText)
        {
            ApplyDonationIndicator(
                donationStatus,
                completed,
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

        if (completionClaimOverlay != null)
        {
            completionClaimOverlay.Setup(
                completionPreview,
                HandleCompletionRewardClaim);
        }

        MuseumLockVisualUtility.Apply(gameObject, unlocked);
    }

    private void ApplyDonationIndicator(
        string status,
        bool completed,
        int readyCount,
        int protectedCount)
    {
        if (donationStateText == null)
            return;

        donationStateText.text = status;
        donationStateText.gameObject.SetActive(
            !string.IsNullOrWhiteSpace(status));

        if (completed)
            donationStateText.color = completedTextColor;
        else if (readyCount > 0)
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

        if (completionClaimOverlay == null)
        {
            completionClaimOverlay =
                GetComponentInChildren<MuseumCompletionClaimOverlayUI>(true);
        }

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

    private static string AppendLine(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first))
            return second ?? "";

        if (string.IsNullOrWhiteSpace(second))
            return first;

        return first + "\n" + second;
    }

    private void HandleCompletionRewardClaim()
    {
        if (!MuseumCompletionRewardService.TryClaim(
                completionPreview,
                out MuseumCompletionRewardClaimResult result))
        {
            if (owner != null && result != null)
                owner.ShowMuseumMessage(result.message);
            return;
        }

        float duration = MuseumCompletionRewardService.Balance != null
            ? MuseumCompletionRewardService.Balance.claimNotificationSeconds
            : 2.75f;

        MuseumCompletionRewardToastBridge.Show(
            owner,
            result.message,
            duration);

        Setup(entry, owner);
    }

    private void HandleClicked()
    {
        if (owner == null || entry == null)
            return;

        if (!MuseumUnlockProgressionUtility.IsCategoryUnlocked(
                entry,
                GetDatabase(),
                out string lockedReason))
        {
            owner.ShowMuseumMessage(lockedReason);
            return;
        }

        owner.OpenCategory(entry);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }
}
