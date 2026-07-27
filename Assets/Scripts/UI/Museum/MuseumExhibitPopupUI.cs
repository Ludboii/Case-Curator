using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interactive M3 skin exhibit. It replaces the temporary text-only M2 table.
/// </summary>
public class MuseumExhibitPopupUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Image rarityBackground;
    [SerializeField] private Image skinImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text progressText;

    [Header("Column Headers")]
    [SerializeField] private Transform columnHeaderRoot;
    [SerializeField] private TMP_Text wearHeaderText;
    [SerializeField] private TMP_Text normalHeaderText;
    [SerializeField] private TMP_Text statTrakHeaderText;
    [SerializeField] private TMP_Text souvenirHeaderText;

    [Header("Wear Rows")]
    [SerializeField] private Transform wearRowContent;
    [SerializeField] private MuseumExhibitWearRowUI wearRowPrefab;
    [SerializeField] private float rowSpacing = 8f;

    [Header("Bulk Donation")]
    [Tooltip(
        "Fills every currently empty wear/variant slot for this skin. " +
        "Warning-only items are allowed; Favorite, Trophy Room and other " +
        "hard-blocked items remain protected.")]
    [SerializeField] private Button bulkDonateButton;
    [SerializeField] private TMP_Text bulkDonateButtonText;
    [SerializeField] private TMP_Text bulkDonationStatusText;
    [SerializeField] private bool requireBulkDonationConfirmation = true;
    [SerializeField, Min(1f)] private float bulkConfirmationWindowSeconds = 5f;

    [SerializeField] private Button closeButton;

    private readonly List<GameObject> spawnedRows = new List<GameObject>();
    private MuseumSkinEntry entry;
    private MuseumPanelUI owner;
    private MuseumService service;
    private MuseumBulkDonationPlan pendingBulkPlan;
    private float bulkConfirmationExpiresAt;

    public bool IsOpen => root != null && root.activeSelf;
    public MuseumSkinEntry Entry => entry;

    private void Awake()
    {
        ResolveReferences();
        ConfigureRowLayout();
        SetupButtons();
    }

    private void OnDisable()
    {
        ClearBulkConfirmation(false);
    }

    private void OnValidate()
    {
        ResolveReferences();
        ConfigureRowLayout();
        ApplyHeaderLabels();
    }

    public void Open(
        MuseumSkinEntry museumEntry,
        MuseumPanelUI panel,
        MuseumService museumService)
    {
        if (museumEntry == null || museumEntry.skin == null)
            return;

        bool changedSkin = entry == null ||
                           entry.skin == null ||
                           !string.Equals(
                               entry.skin.apiId,
                               museumEntry.skin.apiId,
                               StringComparison.Ordinal);

        entry = museumEntry;
        owner = panel;
        service = museumService;

        ResolveReferences();
        ConfigureRowLayout();
        ApplyHeaderLabels();
        SetupButtons();

        if (changedSkin)
        {
            ClearBulkConfirmation(false);
            SetBulkStatus("");
        }

        if (root == null)
            root = gameObject;

        root.SetActive(true);

        SkinData skin = entry.skin;

        if (rarityBackground != null)
            rarityBackground.color = RarityColorUtility.GetColor(skin.rarity);

        if (skinImage != null)
        {
            skinImage.sprite = skin.icon;
            skinImage.enabled = skin.icon != null;
            skinImage.preserveAspect = true;
        }

        if (titleText != null)
            titleText.text = SkinDisplayUtility.GetDisplayName(skin);

        if (rarityText != null)
            rarityText.text = skin.rarity.ToString();

        if (progressText != null)
        {
            float percentage = entry.TotalSlots > 0
                ? entry.DonatedSlots * 100f / entry.TotalSlots
                : 0f;

            progressText.text =
                $"Museum slots: {entry.DonatedSlots} / {entry.TotalSlots} " +
                $"({percentage:0.#}%)";
        }

        BuildRows();
        RefreshBulkDonationButton();
    }

    public void Refresh(MuseumSkinEntry refreshedEntry)
    {
        Open(refreshedEntry, owner, service);
    }

    public void Close()
    {
        ClearRows();
        ClearBulkConfirmation(false);
        entry = null;

        if (root != null)
            root.SetActive(false);
    }

    public void RequestBulkDonation()
    {
        if (entry == null || entry.skin == null || service == null)
        {
            SetBulkStatus("Museum donation services are unavailable.");
            return;
        }

        if (pendingBulkPlan != null &&
            Time.unscaledTime > bulkConfirmationExpiresAt)
        {
            ClearBulkConfirmation(false);
        }

        if (pendingBulkPlan == null)
        {
            MuseumBulkDonationPlan plan =
                MuseumBulkDonationUtility.BuildPlan(service, entry);

            if (plan == null || plan.DonationCount <= 0)
            {
                SetBulkStatus(
                    "Nothing can be bulk donated. Filled slots, Favorite items, " +
                    "Trophy Room items and other hard-blocked items were skipped.");
                RefreshBulkDonationButton();
                return;
            }

            if (requireBulkDonationConfirmation)
            {
                pendingBulkPlan = plan;
                bulkConfirmationExpiresAt =
                    Time.unscaledTime +
                    Mathf.Max(1f, bulkConfirmationWindowSeconds);

                SetBulkStatus(BuildConfirmationText(plan));
                SetBulkButtonText($"CONFIRM ({plan.DonationCount:N0})");
                return;
            }

            ExecuteBulkDonation(plan);
            return;
        }

        MuseumBulkDonationPlan confirmedPlan = pendingBulkPlan;
        ClearBulkConfirmation(false);
        ExecuteBulkDonation(confirmedPlan);
    }

    private void ExecuteBulkDonation(MuseumBulkDonationPlan plan)
    {
        if (bulkDonateButton != null)
            bulkDonateButton.interactable = false;

        MuseumBulkDonationResult result =
            MuseumBulkDonationUtility.Execute(service, plan);

        string message = BuildResultText(result);
        SetBulkStatus(message);

        if (owner != null)
            owner.ShowMuseumMessage(message);

        RefreshBulkDonationButton();
    }

    private string BuildConfirmationText(MuseumBulkDonationPlan plan)
    {
        string warningText = plan.entriesWithWarnings > 0
            ? $" {plan.entriesWithWarnings:N0} selected item(s) have donation " +
              "warnings; those warnings will be ignored."
            : "";

        return
            $"Donate {plan.DonationCount:N0} item(s), worth " +
            $"{plan.totalMarketValue:N2} Gold, for approximately " +
            $"{plan.estimatedMuseumPoints:N2} Museum Points?" +
            warningText +
            " Favorite and hard-blocked items stay protected. Press again to confirm.";
    }

    private static string BuildResultText(MuseumBulkDonationResult result)
    {
        if (result == null || result.donated <= 0)
        {
            return result != null &&
                   !string.IsNullOrWhiteSpace(result.firstFailure)
                ? "Bulk donation failed: " + result.firstFailure
                : "No items were donated.";
        }

        string text =
            $"Bulk donated {result.donated:N0} item(s) for " +
            $"{result.museumPointsAwarded:N2} Museum Points.";

        if (result.failed > 0)
        {
            text +=
                $" {result.failed:N0} item(s) failed or became unavailable.";

            if (!string.IsNullOrWhiteSpace(result.firstFailure))
                text += " First failure: " + result.firstFailure;
        }

        return text;
    }

    private void RefreshBulkDonationButton()
    {
        if (bulkDonateButton == null)
            return;

        if (entry == null || service == null)
        {
            bulkDonateButton.interactable = false;
            SetBulkButtonText("DONATE ALL");
            return;
        }

        MuseumBulkDonationPlan plan =
            MuseumBulkDonationUtility.BuildPlan(service, entry);
        int count = plan != null ? plan.DonationCount : 0;

        bulkDonateButton.interactable = count > 0;
        SetBulkButtonText(
            count > 0
                ? $"DONATE ALL ({count:N0})"
                : "NOTHING TO DONATE");
    }

    private void ClearBulkConfirmation(bool refreshButton)
    {
        pendingBulkPlan = null;
        bulkConfirmationExpiresAt = 0f;

        if (refreshButton)
            RefreshBulkDonationButton();
    }

    private void SetBulkButtonText(string value)
    {
        if (bulkDonateButtonText != null)
            bulkDonateButtonText.text = value ?? "";
    }

    private void SetBulkStatus(string value)
    {
        if (bulkDonationStatusText == null)
            return;

        bulkDonationStatusText.text = value ?? "";
        bulkDonationStatusText.gameObject.SetActive(
            !string.IsNullOrWhiteSpace(value));
    }

    private void BuildRows()
    {
        ClearRows();

        if (entry == null || entry.slots == null ||
            wearRowContent == null || wearRowPrefab == null)
        {
            return;
        }

        int[] wearOrder = entry.skin != null && entry.skin.isVanilla
            ? new[] { -1 }
            : new[] { 0, 1, 2, 3, 4 };

        for (int i = 0; i < wearOrder.Length; i++)
        {
            int wearIndex = wearOrder[i];

            if (!HasWear(entry, wearIndex))
                continue;

            MuseumExhibitWearRowUI row = Instantiate(wearRowPrefab, wearRowContent);
            row.gameObject.SetActive(true);
            row.Setup(entry, wearIndex, owner, service);
            spawnedRows.Add(row.gameObject);
        }

        if (wearRowContent is RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            Canvas.ForceUpdateCanvases();
        }
    }

    private void ConfigureRowLayout()
    {
        if (wearRowContent == null)
            return;

        VerticalLayoutGroup layout =
            wearRowContent.GetComponent<VerticalLayoutGroup>();

        if (layout == null)
            layout = wearRowContent.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.spacing = rowSpacing;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter =
            wearRowContent.GetComponent<ContentSizeFitter>();

        if (fitter == null)
            fitter = wearRowContent.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (wearRowContent is RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
        }
    }

    private void ApplyHeaderLabels()
    {
        if (wearHeaderText != null)
            wearHeaderText.text = "Wear";

        if (normalHeaderText != null)
            normalHeaderText.text = "Normal";

        if (statTrakHeaderText != null)
            statTrakHeaderText.text = "StatTrak";

        if (souvenirHeaderText != null)
            souvenirHeaderText.text = "Souvenir";
    }

    private void SetupButtons()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        if (bulkDonateButton != null)
        {
            bulkDonateButton.onClick.RemoveListener(RequestBulkDonation);
            bulkDonateButton.onClick.AddListener(RequestBulkDonation);
        }
    }

    private void ResolveReferences()
    {
        if (root == null)
            root = gameObject;

        if (columnHeaderRoot == null)
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];

                if (candidate != null && candidate.name.IndexOf(
                        "columnheader",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    columnHeaderRoot = candidate;
                    break;
                }
            }
        }

        if (columnHeaderRoot != null)
        {
            TMP_Text[] headers = columnHeaderRoot.GetComponentsInChildren<TMP_Text>(true);

            if (wearHeaderText == null && headers.Length > 0)
                wearHeaderText = headers[0];
            if (normalHeaderText == null && headers.Length > 1)
                normalHeaderText = headers[1];
            if (statTrakHeaderText == null && headers.Length > 2)
                statTrakHeaderText = headers[2];
            if (souvenirHeaderText == null && headers.Length > 3)
                souvenirHeaderText = headers[3];
        }

        if (bulkDonateButton == null)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);

            for (int i = 0; i < buttons.Length; i++)
            {
                Button candidate = buttons[i];

                if (candidate == null)
                    continue;

                string normalizedName = candidate.gameObject.name
                    .Replace(" ", "")
                    .Replace("_", "")
                    .ToLowerInvariant();

                if (normalizedName.Contains("bulkdonate") ||
                    normalizedName.Contains("bulkdonation"))
                {
                    bulkDonateButton = candidate;
                    break;
                }
            }
        }

        if (bulkDonateButton != null && bulkDonateButtonText == null)
        {
            bulkDonateButtonText =
                bulkDonateButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (bulkDonationStatusText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text candidate = texts[i];

                if (candidate == null)
                    continue;

                string normalizedName = candidate.gameObject.name
                    .Replace(" ", "")
                    .Replace("_", "")
                    .ToLowerInvariant();

                if (normalizedName.Contains("bulkdonationstatus") ||
                    normalizedName.Contains("bulkdonateresult"))
                {
                    bulkDonationStatusText = candidate;
                    break;
                }
            }
        }
    }

    private static bool HasWear(MuseumSkinEntry skin, int wearIndex)
    {
        for (int i = 0; i < skin.slots.Count; i++)
        {
            if (skin.slots[i] != null && skin.slots[i].wearIndex == wearIndex)
                return true;
        }

        return false;
    }

    private void ClearRows()
    {
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            if (spawnedRows[i] != null)
                Destroy(spawnedRows[i]);
        }

        spawnedRows.Clear();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);

        if (bulkDonateButton != null)
            bulkDonateButton.onClick.RemoveListener(RequestBulkDonation);
    }
}
