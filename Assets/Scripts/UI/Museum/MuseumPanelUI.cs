using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phase M2 read-only Museum browser:
/// Entrance/Wings -> Categories -> Weapon Models -> Skin Exhibits.
/// It consumes MuseumService catalog snapshots and never mutates save data.
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

    [Header("Read-Only Skin Detail")]
    [SerializeField] private GameObject skinDetailRoot;
    [SerializeField] private Image detailRarityBackground;
    [SerializeField] private Image detailSkinImage;
    [SerializeField] private TMP_Text detailTitleText;
    [SerializeField] private TMP_Text detailRarityText;
    [SerializeField] private TMP_Text detailProgressText;
    [SerializeField] private TMP_Text detailSlotsText;
    [SerializeField] private Button closeDetailButton;

    private readonly List<GameObject> spawnedCards = new List<GameObject>();

    private MuseumCatalogSnapshot catalog;
    private MuseumWingEntry currentWing;
    private MuseumCategoryEntry currentCategory;
    private MuseumWeaponEntry currentWeapon;
    private MuseumSkinEntry currentSkin;
    private MuseumBrowserPage currentPage = MuseumBrowserPage.Wings;
    private bool subscribed;

    private void Awake()
    {
        SetupButton(backButton, Back);
        SetupButton(homeButton, ShowEntrance);
        SetupButton(refreshButton, RefreshCatalog);
        SetupButton(closeDetailButton, CloseSkinDetail);

        CloseSkinDetail();
    }

    private void OnEnable()
    {
        ResolveService();
        Subscribe();
        RefreshCatalog();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearCards();
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
        CloseSkinDetail();
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

        CloseSkinDetail();
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

        CloseSkinDetail();
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

        CloseSkinDetail();
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

        currentSkin = entry;
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

    public void CloseSkinDetail()
    {
        currentSkin = null;

        if (skinDetailRoot != null)
            skinDetailRoot.SetActive(false);
    }

    public void Back()
    {
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

            default:
                break;
        }
    }

    private void HandleMuseumChanged()
    {
        // Donations normally occur outside this read-only panel. Rebuild the
        // snapshot so returning to the Museum immediately shows new progress.
        catalog = museumService != null
            ? museumService.GetCatalogSnapshot(true)
            : null;

        RefreshHeader();
        ShowEntrance();
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

    private static void SetupButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void RemoveButton(Button button, UnityEngine.Events.UnityAction action)
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
            bool hasAny = HasSlotForWear(entry, wear);

            if (!hasAny)
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
        string mark = found.donated ? "✓" : "—";
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
