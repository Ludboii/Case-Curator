using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Museum browser plus M3 exact-slot donation workflow.
/// </summary>
public class MuseumPanelUI : MonoBehaviour
{
    private enum MuseumBrowserPage
    {
        Wings,
        Categories,
        Weapons,
        Skins
    }

    [Header("Service")]
    [SerializeField] private MuseumService museumService;

    [Header("Header")]
    [SerializeField] private TMP_Text pageTitleText;
    [SerializeField] private TMP_Text breadcrumbText;
    [SerializeField] private TMP_Text museumPointsText;
    [SerializeField] private TMP_Text overallProgressText;
    [SerializeField] private MuseumProgressBarUI overallProgressBar;
    [SerializeField] private Button backButton;
    [SerializeField] private Button homeButton;
    [SerializeField] private Button refreshButton;

    [Header("Views")]
    [SerializeField] private GameObject wingView;
    [SerializeField] private GameObject categoryView;
    [SerializeField] private GameObject weaponView;
    [SerializeField] private GameObject skinView;

    [Header("Content Roots")]
    [SerializeField] private Transform wingContent;
    [SerializeField] private Transform categoryContent;
    [SerializeField] private Transform weaponContent;
    [SerializeField] private Transform skinContent;

    [Header("Card Prefabs")]
    [SerializeField] private MuseumWingCardUI wingCardPrefab;
    [SerializeField] private MuseumCategoryCardUI categoryCardPrefab;
    [SerializeField] private MuseumWeaponCardUI weaponCardPrefab;
    [SerializeField] private MuseumSkinCardUI skinCardPrefab;

    [Header("Empty State")]
    [SerializeField] private GameObject emptyStateRoot;
    [SerializeField] private TMP_Text emptyStateText;

    [Header("Legacy Skin Detail Fallback")]
    [SerializeField] private GameObject skinDetailRoot;
    [SerializeField] private Image detailRarityBackground;
    [SerializeField] private Image detailSkinImage;
    [SerializeField] private TMP_Text detailTitleText;
    [SerializeField] private TMP_Text detailRarityText;
    [SerializeField] private TMP_Text detailProgressText;
    [SerializeField] private TMP_Text detailSlotsText;
    [SerializeField] private Button closeDetailButton;

    [Header("M3 Donation UI")]
    [SerializeField] private MuseumExhibitPopupUI exhibitPopup;
    [SerializeField] private MuseumDonationSelectionUI donationSelection;
    [SerializeField] private MuseumDonationConfirmationUI donationConfirmation;
    [SerializeField] private TMP_Text donationResultText;

    private readonly List<GameObject> spawnedCards = new List<GameObject>();

    private MuseumCatalogSnapshot catalog;
    private MuseumWingEntry currentWing;
    private MuseumCategoryEntry currentCategory;
    private MuseumWeaponEntry currentWeapon;
    private MuseumSkinEntry currentSkin;
    private MuseumBrowserPage currentPage = MuseumBrowserPage.Wings;
    private bool subscribed;

    public MuseumSkinEntry CurrentSkin => currentSkin;
    public MuseumService Service => museumService;

    private void Awake()
    {
        ResolveM3References();
        SetupButton(backButton, Back);
        SetupButton(homeButton, ShowEntrance);
        SetupButton(refreshButton, RefreshCatalog);
        SetupButton(closeDetailButton, CloseSkinDetail);

        CloseAllOverlays();
        ClearResultMessage();
    }

    private void OnEnable()
    {
        ResolveService();
        ResolveM3References();
        Subscribe();
        RefreshCatalog();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearCards();
        CloseAllOverlays();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        RemoveButton(backButton, Back);
        RemoveButton(homeButton, ShowEntrance);
        RemoveButton(refreshButton, RefreshCatalog);
        RemoveButton(closeDetailButton, CloseSkinDetail);
    }

    public void RefreshCatalog()
    {
        ResolveService();

        catalog = museumService != null
            ? museumService.GetCatalogSnapshot(true)
            : null;

        RefreshHeader();
        ShowEntrance();
    }

    public void ShowEntrance()
    {
        CloseAllOverlays();
        ClearResultMessage();
        currentPage = MuseumBrowserPage.Wings;
        currentWing = null;
        currentCategory = null;
        currentWeapon = null;
        currentSkin = null;

        SetActiveView(wingView);
        SetHeader("Museum", "Museum Entrance");
        ClearCards();

        int spawned = 0;

        if (catalog != null && catalog.wings != null && wingCardPrefab != null)
        {
            for (int i = 0; i < catalog.wings.Count; i++)
            {
                MuseumWingEntry entry = catalog.wings[i];

                if (entry == null)
                    continue;

                MuseumWingCardUI card = Instantiate(wingCardPrefab, wingContent);
                card.gameObject.SetActive(true);
                card.Setup(entry, this);
                spawnedCards.Add(card.gameObject);
                spawned++;
            }
        }

        ShowEmptyState(
            spawned == 0,
            "No Museum wings were generated. Check GameDatabase.allSkins and MuseumCatalogConfig.");

        RefreshBackButton();
        RebuildLayout(wingContent);
    }

    public void OpenWing(MuseumWingEntry entry)
    {
        if (entry == null)
            return;

        CloseAllOverlays();
        ClearResultMessage();
        currentPage = MuseumBrowserPage.Categories;
        currentWing = entry;
        currentCategory = null;
        currentWeapon = null;
        currentSkin = null;

        SetActiveView(categoryView);
        SetHeader(entry.DisplayName, $"Museum / {entry.DisplayName}");
        ClearCards();

        int spawned = 0;

        if (entry.categories != null && categoryCardPrefab != null)
        {
            for (int i = 0; i < entry.categories.Count; i++)
            {
                MuseumCategoryEntry category = entry.categories[i];

                if (category == null)
                    continue;

                MuseumCategoryCardUI card =
                    Instantiate(categoryCardPrefab, categoryContent);
                card.gameObject.SetActive(true);
                card.Setup(category, this);
                spawnedCards.Add(card.gameObject);
                spawned++;
            }
        }

        ShowEmptyState(spawned == 0, "This Museum wing has no categories.");
        RefreshBackButton();
        RebuildLayout(categoryContent);
    }

    public void OpenCategory(MuseumCategoryEntry entry)
    {
        if (entry == null)
            return;

        CloseAllOverlays();
        ClearResultMessage();
        currentPage = MuseumBrowserPage.Weapons;
        currentCategory = entry;
        currentWeapon = null;
        currentSkin = null;

        string wingName = currentWing != null
            ? currentWing.DisplayName
            : "Museum";

        SetActiveView(weaponView);
        SetHeader(
            entry.DisplayName,
            $"Museum / {wingName} / {entry.DisplayName}");
        ClearCards();

        int spawned = 0;

        if (entry.weapons != null && weaponCardPrefab != null)
        {
            for (int i = 0; i < entry.weapons.Count; i++)
            {
                MuseumWeaponEntry weapon = entry.weapons[i];

                if (weapon == null)
                    continue;

                MuseumWeaponCardUI card =
                    Instantiate(weaponCardPrefab, weaponContent);
                card.gameObject.SetActive(true);
                card.Setup(weapon, this);
                spawnedCards.Add(card.gameObject);
                spawned++;
            }
        }

        ShowEmptyState(
            spawned == 0,
            "No weapon models matched this category's Museum catalog filter.");

        RefreshBackButton();
        RebuildLayout(weaponContent);
    }

    public void OpenWeapon(MuseumWeaponEntry entry)
    {
        if (entry == null)
            return;

        CloseAllOverlays();
        ClearResultMessage();
        currentPage = MuseumBrowserPage.Skins;
        currentWeapon = entry;
        currentSkin = null;

        string categoryName = currentCategory != null
            ? currentCategory.DisplayName
            : "Museum";

        SetActiveView(skinView);
        SetHeader(
            entry.weaponName,
            $"Museum / {categoryName} / {entry.weaponName}");
        ClearCards();

        int spawned = 0;

        if (entry.skins != null && skinCardPrefab != null)
        {
            for (int i = 0; i < entry.skins.Count; i++)
            {
                MuseumSkinEntry skin = entry.skins[i];

                if (skin == null)
                    continue;

                MuseumSkinCardUI card =
                    Instantiate(skinCardPrefab, skinContent);
                card.gameObject.SetActive(true);
                card.Setup(skin, this);
                spawnedCards.Add(card.gameObject);
                spawned++;
            }
        }

        ShowEmptyState(spawned == 0, "This weapon has no Museum exhibits.");
        RefreshBackButton();
        RebuildLayout(skinContent);
    }

    public void OpenSkin(MuseumSkinEntry entry)
    {
        if (entry == null || entry.skin == null)
            return;

        ResolveService();
        ResolveM3References();
        currentSkin = entry;
        ClearResultMessage();

        if (exhibitPopup != null)
        {
            exhibitPopup.Open(entry, this, museumService);
            return;
        }

        OpenLegacySkinDetail(entry);
    }

    public void OpenDonationSelection(MuseumSlotEntry slot)
    {
        ResolveService();
        ResolveM3References();

        if (slot == null || museumService == null)
            return;

        if (slot.donated)
        {
            MuseumDonationRecordSaveData record =
                museumService.GetDonationRecord(slot.donationKey);

            ShowMuseumMessage(record != null
                ? $"Already collected. Awarded {record.totalMuseumPointsAwarded:0.##} MP."
                : "This Museum slot is already collected.");
            return;
        }

        IReadOnlyList<MuseumDonationCandidate> candidates =
            museumService.GetDonationCandidates(slot);

        if (candidates == null || candidates.Count == 0)
        {
            ShowMuseumMessage(
                "You do not own an item matching this exact skin, wear and variant.");
            return;
        }

        if (donationSelection == null)
        {
            ShowMuseumMessage(
                "MuseumDonationSelectionUI is not assigned on MuseumPanelUI.");
            return;
        }

        donationSelection.Open(slot, this, museumService);
    }

    public void OpenDonationConfirmation(
        MuseumSlotEntry slot,
        MuseumDonationCandidate candidate)
    {
        ResolveService();
        ResolveM3References();

        if (slot == null || candidate == null || donationConfirmation == null)
            return;

        donationConfirmation.Open(slot, candidate, this, museumService);
    }

    public void HandleDonationCompleted(
        MuseumDonationResult result,
        string donationKey)
    {
        if (result == null || !result.success)
            return;

        if (donationSelection != null)
            donationSelection.Close();

        RefreshCurrentLocationAfterCatalogChange(donationKey);
        ShowMuseumMessage($"+{result.museumPointsAwarded:0.##} Museum Points");
    }

    public void ShowMuseumMessage(string message)
    {
        if (donationResultText != null)
        {
            donationResultText.gameObject.SetActive(true);
            donationResultText.text = message ?? "";
        }
        else
        {
            Debug.Log(message, this);
        }
    }

    public void CloseSkinDetail()
    {
        if (donationConfirmation != null && donationConfirmation.IsOpen)
        {
            donationConfirmation.Close();
            return;
        }

        if (donationSelection != null && donationSelection.IsOpen)
        {
            donationSelection.Close();
            return;
        }

        if (exhibitPopup != null && exhibitPopup.IsOpen)
            exhibitPopup.Close();

        if (skinDetailRoot != null)
            skinDetailRoot.SetActive(false);

        currentSkin = null;
    }

    public void Back()
    {
        if (donationConfirmation != null && donationConfirmation.IsOpen)
        {
            donationConfirmation.Close();
            return;
        }

        if (donationSelection != null && donationSelection.IsOpen)
        {
            donationSelection.Close();
            return;
        }

        if (exhibitPopup != null && exhibitPopup.IsOpen)
        {
            exhibitPopup.Close();
            currentSkin = null;
            return;
        }

        if (skinDetailRoot != null && skinDetailRoot.activeSelf)
        {
            CloseSkinDetail();
            return;
        }

        switch (currentPage)
        {
            case MuseumBrowserPage.Skins:
                OpenCategory(currentCategory);
                break;

            case MuseumBrowserPage.Weapons:
                OpenWing(currentWing);
                break;

            case MuseumBrowserPage.Categories:
                ShowEntrance();
                break;
        }
    }

    private void OpenLegacySkinDetail(MuseumSkinEntry entry)
    {
        SkinData skin = entry.skin;

        if (skinDetailRoot != null)
            skinDetailRoot.SetActive(true);

        if (detailRarityBackground != null)
            detailRarityBackground.color = RarityColorUtility.GetColor(skin.rarity);

        if (detailSkinImage != null)
        {
            detailSkinImage.sprite = skin.icon;
            detailSkinImage.enabled = skin.icon != null;
            detailSkinImage.preserveAspect = true;
        }

        if (detailTitleText != null)
            detailTitleText.text = SkinDisplayUtility.GetDisplayName(skin);

        if (detailRarityText != null)
            detailRarityText.text = skin.rarity.ToString();

        if (detailProgressText != null)
        {
            float percentage = entry.TotalSlots > 0
                ? entry.DonatedSlots * 100f / entry.TotalSlots
                : 0f;

            detailProgressText.text =
                $"Museum slots: {entry.DonatedSlots} / {entry.TotalSlots} " +
                $"({percentage:0.#}%)";
        }

        if (detailSlotsText != null)
        {
            detailSlotsText.richText = true;
            detailSlotsText.text = BuildSlotSummary(entry);
        }
    }

    private void HandleMuseumChanged()
    {
        string donationKey = currentSkin != null && currentSkin.slots != null
            ? FindFirstDonationKey(currentSkin)
            : "";

        RefreshCurrentLocationAfterCatalogChange(donationKey);
    }

    private void RefreshCurrentLocationAfterCatalogChange(string preferredDonationKey)
    {
        ResolveService();

        string wingId = currentWing != null ? currentWing.WingId : "";
        string categoryId = currentCategory != null ? currentCategory.CategoryId : "";
        string weaponName = currentWeapon != null ? currentWeapon.weaponName : "";
        string skinApiId = currentSkin != null && currentSkin.skin != null
            ? currentSkin.skin.apiId
            : "";
        MuseumBrowserPage previousPage = currentPage;
        bool reopenSkin = !string.IsNullOrWhiteSpace(skinApiId);

        catalog = museumService != null
            ? museumService.GetCatalogSnapshot(true)
            : null;

        RefreshHeader();

        MuseumWingEntry refreshedWing = FindWing(wingId);
        MuseumCategoryEntry refreshedCategory =
            FindCategory(refreshedWing, categoryId);
        MuseumWeaponEntry refreshedWeapon =
            FindWeapon(refreshedCategory, weaponName);
        MuseumSkinEntry refreshedSkin =
            FindSkin(refreshedWeapon, skinApiId, preferredDonationKey);

        if (previousPage == MuseumBrowserPage.Wings || refreshedWing == null)
        {
            ShowEntrance();
            return;
        }

        currentWing = refreshedWing;

        if (previousPage == MuseumBrowserPage.Categories || refreshedCategory == null)
        {
            OpenWing(refreshedWing);
            return;
        }

        currentCategory = refreshedCategory;

        if (previousPage == MuseumBrowserPage.Weapons || refreshedWeapon == null)
        {
            OpenCategory(refreshedCategory);
            return;
        }

        currentWeapon = refreshedWeapon;
        OpenWeapon(refreshedWeapon);

        if (reopenSkin && refreshedSkin != null)
            OpenSkin(refreshedSkin);
    }

    private MuseumWingEntry FindWing(string wingId)
    {
        if (catalog == null || catalog.wings == null)
            return null;

        for (int i = 0; i < catalog.wings.Count; i++)
        {
            MuseumWingEntry wing = catalog.wings[i];

            if (wing != null &&
                string.Equals(
                    wing.WingId,
                    wingId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return wing;
            }
        }

        return null;
    }

    private static MuseumCategoryEntry FindCategory(
        MuseumWingEntry wing,
        string categoryId)
    {
        if (wing == null || wing.categories == null)
            return null;

        for (int i = 0; i < wing.categories.Count; i++)
        {
            MuseumCategoryEntry category = wing.categories[i];

            if (category != null &&
                string.Equals(
                    category.CategoryId,
                    categoryId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return category;
            }
        }

        return null;
    }

    private static MuseumWeaponEntry FindWeapon(
        MuseumCategoryEntry category,
        string weaponName)
    {
        if (category == null || category.weapons == null)
            return null;

        for (int i = 0; i < category.weapons.Count; i++)
        {
            MuseumWeaponEntry weapon = category.weapons[i];

            if (weapon != null &&
                string.Equals(
                    weapon.weaponName,
                    weaponName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return weapon;
            }
        }

        return null;
    }

    private static MuseumSkinEntry FindSkin(
        MuseumWeaponEntry weapon,
        string skinApiId,
        string preferredDonationKey)
    {
        if (weapon == null || weapon.skins == null)
            return null;

        for (int i = 0; i < weapon.skins.Count; i++)
        {
            MuseumSkinEntry skin = weapon.skins[i];

            if (skin == null || skin.skin == null)
                continue;

            if (!string.IsNullOrWhiteSpace(skinApiId) &&
                string.Equals(
                    skin.skin.apiId,
                    skinApiId,
                    StringComparison.Ordinal))
            {
                return skin;
            }

            if (!string.IsNullOrWhiteSpace(preferredDonationKey) &&
                ContainsSlot(skin, preferredDonationKey))
            {
                return skin;
            }
        }

        return null;
    }

    private static bool ContainsSlot(MuseumSkinEntry skin, string donationKey)
    {
        if (skin == null || skin.slots == null)
            return false;

        for (int i = 0; i < skin.slots.Count; i++)
        {
            MuseumSlotEntry slot = skin.slots[i];

            if (slot != null &&
                string.Equals(
                    slot.donationKey,
                    donationKey,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string FindFirstDonationKey(MuseumSkinEntry skin)
    {
        if (skin == null || skin.slots == null)
            return "";

        for (int i = 0; i < skin.slots.Count; i++)
        {
            if (skin.slots[i] != null)
                return skin.slots[i].donationKey;
        }

        return "";
    }

    private void RefreshHeader()
    {
        if (museumPointsText != null)
        {
            double points = museumService != null
                ? museumService.MuseumPoints
                : 0d;
            museumPointsText.text = $"{points:0.##} MP";
        }

        int donated = catalog != null ? catalog.donatedSlots : 0;
        int total = catalog != null ? catalog.totalSlots : 0;

        if (overallProgressText != null)
            overallProgressText.text = $"Museum: {donated} / {total}";

        if (overallProgressBar != null)
            overallProgressBar.SetProgress(donated, total);
    }

    private void SetHeader(string title, string breadcrumb)
    {
        RefreshHeader();

        if (pageTitleText != null)
            pageTitleText.text = title ?? "Museum";

        if (breadcrumbText != null)
            breadcrumbText.text = breadcrumb ?? "Museum";
    }

    private void SetActiveView(GameObject activeView)
    {
        SetView(wingView, activeView);
        SetView(categoryView, activeView);
        SetView(weaponView, activeView);
        SetView(skinView, activeView);
    }

    private static void SetView(GameObject view, GameObject activeView)
    {
        if (view != null)
            view.SetActive(view == activeView);
    }

    private void ShowEmptyState(bool visible, string message)
    {
        if (emptyStateRoot != null)
            emptyStateRoot.SetActive(visible);

        if (emptyStateText != null)
            emptyStateText.text = visible ? message : "";
    }

    private void RefreshBackButton()
    {
        if (backButton != null)
            backButton.gameObject.SetActive(currentPage != MuseumBrowserPage.Wings);
    }

    private void ClearCards()
    {
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            GameObject card = spawnedCards[i];

            if (card == null)
                continue;

            card.SetActive(false);
            Destroy(card);
        }

        spawnedCards.Clear();
    }

    private void ResolveService()
    {
        if (museumService == null)
            museumService = MuseumService.Instance;
    }

    private void ResolveM3References()
    {
        if (exhibitPopup == null)
            exhibitPopup = GetComponentInChildren<MuseumExhibitPopupUI>(true);

        if (donationSelection == null)
            donationSelection = GetComponentInChildren<MuseumDonationSelectionUI>(true);

        if (donationConfirmation == null)
            donationConfirmation = GetComponentInChildren<MuseumDonationConfirmationUI>(true);
    }

    private void Subscribe()
    {
        ResolveService();

        if (museumService == null || subscribed)
            return;

        museumService.OnMuseumChanged += HandleMuseumChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (museumService != null && subscribed)
            museumService.OnMuseumChanged -= HandleMuseumChanged;

        subscribed = false;
    }

    private void CloseAllOverlays()
    {
        if (donationConfirmation != null)
            donationConfirmation.Close();

        if (donationSelection != null)
            donationSelection.Close();

        if (exhibitPopup != null)
            exhibitPopup.Close();

        if (skinDetailRoot != null)
            skinDetailRoot.SetActive(false);
    }

    private void ClearResultMessage()
    {
        if (donationResultText != null)
        {
            donationResultText.text = "";
            donationResultText.gameObject.SetActive(false);
        }
    }

    private static void SetupButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void RemoveButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }

    private static void RebuildLayout(Transform content)
    {
        if (!(content is RectTransform rect))
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private static string BuildSlotSummary(MuseumSkinEntry entry)
    {
        if (entry == null || entry.slots == null || entry.slots.Count == 0)
            return "No Museum slots are available for this skin.";

        StringBuilder builder = new StringBuilder();
        int[] wearOrder = { -1, 0, 1, 2, 3, 4 };

        for (int wearIndex = 0; wearIndex < wearOrder.Length; wearIndex++)
        {
            int wear = wearOrder[wearIndex];

            if (!HasSlotForWear(entry, wear))
                continue;

            builder.Append(wear < 0 ? "Vanilla" : GetWearAbbreviation(wear));
            builder.Append(": ");
            AppendVariant(builder, entry, wear, MuseumDonationVariant.Normal, "N");
            AppendVariant(builder, entry, wear, MuseumDonationVariant.StatTrak, "ST");
            AppendVariant(builder, entry, wear, MuseumDonationVariant.Souvenir, "SV");
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static bool HasSlotForWear(MuseumSkinEntry entry, int wearIndex)
    {
        for (int i = 0; i < entry.slots.Count; i++)
        {
            MuseumSlotEntry slot = entry.slots[i];

            if (slot != null && slot.wearIndex == wearIndex)
                return true;
        }

        return false;
    }

    private static void AppendVariant(
        StringBuilder builder,
        MuseumSkinEntry entry,
        int wearIndex,
        MuseumDonationVariant variant,
        string label)
    {
        MuseumSlotEntry found = null;

        for (int i = 0; i < entry.slots.Count; i++)
        {
            MuseumSlotEntry slot = entry.slots[i];

            if (slot != null &&
                slot.wearIndex == wearIndex &&
                slot.variant == variant)
            {
                found = slot;
                break;
            }
        }

        if (found == null)
            return;

        string color = found.donated ? "#55FF66" : "#A0A0A0";
        string mark = found.donated ? "V" : "X";
        builder.Append($"<color={color}>{label} {mark}</color>  ");
    }

    private static string GetWearAbbreviation(int wearIndex)
    {
        switch (wearIndex)
        {
            case 0: return "FN";
            case 1: return "MW";
            case 2: return "FT";
            case 3: return "WW";
            default: return "BS";
        }
    }
}
