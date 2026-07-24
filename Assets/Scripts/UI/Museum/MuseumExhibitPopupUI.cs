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

    [SerializeField] private Button closeButton;

    private readonly List<GameObject> spawnedRows = new List<GameObject>();
    private MuseumSkinEntry entry;
    private MuseumPanelUI owner;
    private MuseumService service;

    public bool IsOpen => root != null && root.activeSelf;
    public MuseumSkinEntry Entry => entry;

    private void Awake()
    {
        ResolveReferences();
        ConfigureRowLayout();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
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

        entry = museumEntry;
        owner = panel;
        service = museumService;

        ResolveReferences();
        ConfigureRowLayout();
        ApplyHeaderLabels();

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
    }

    public void Refresh(MuseumSkinEntry refreshedEntry)
    {
        Open(refreshedEntry, owner, service);
    }

    public void Close()
    {
        ClearRows();
        entry = null;

        if (root != null)
            root.SetActive(false);
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
    }
}
