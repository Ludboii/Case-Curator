using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows exact owned inventory instances matching one empty Museum slot.
/// </summary>
public class MuseumDonationSelectionUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text slotText;

    [Header("Scrollable Candidate List")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform content;
    [SerializeField] private MuseumDonationItemCardUI cardPrefab;

    [SerializeField] private TMP_Text selectedDetailsText;
    [SerializeField] private TMP_Text emptyStateText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button cancelButton;

    private readonly List<MuseumDonationItemCardUI> cards =
        new List<MuseumDonationItemCardUI>();

    private MuseumSlotEntry slot;
    private MuseumDonationCandidate selectedCandidate;
    private MuseumPanelUI owner;
    private MuseumService service;

    public bool IsOpen => root != null && root.activeSelf;
    public MuseumSlotEntry Slot => slot;
    public MuseumDonationCandidate SelectedCandidate => selectedCandidate;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        ResolveScrollReferences();
        ConfigureScrollRect();

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(Continue);
            continueButton.onClick.AddListener(Continue);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(Close);
            cancelButton.onClick.AddListener(Close);
        }
    }

    private void Reset()
    {
        ResolveScrollReferences();
        ConfigureScrollRect();
    }

    private void OnValidate()
    {
        ResolveScrollReferences();
        ConfigureScrollRect();
    }

    public void Open(
        MuseumSlotEntry targetSlot,
        MuseumPanelUI panel,
        MuseumService museumService)
    {
        slot = targetSlot;
        owner = panel;
        service = museumService;
        selectedCandidate = null;

        if (root == null)
            root = gameObject;

        ResolveScrollReferences();
        ConfigureScrollRect();

        root.SetActive(true);
        ClearCards();

        if (headerText != null)
        {
            headerText.text = slot != null && slot.skin != null
                ? $"Donate {SkinDisplayUtility.GetDisplayName(slot.skin)}"
                : "Museum Donation";
        }

        if (slotText != null)
            slotText.text = BuildSlotLabel(slot);

        IReadOnlyList<MuseumDonationCandidate> candidates =
            service != null
                ? service.GetDonationCandidates(slot)
                : new List<MuseumDonationCandidate>();

        if (candidates != null && content != null && cardPrefab != null)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                MuseumDonationItemCardUI card = Instantiate(cardPrefab, content);
                card.gameObject.SetActive(true);
                card.Setup(candidates[i], this);
                cards.Add(card);
            }
        }

        if (emptyStateText != null)
        {
            emptyStateText.gameObject.SetActive(
                candidates == null || candidates.Count == 0);
            emptyStateText.text =
                "You do not own an item matching this exact wear and variant.";
        }

        RefreshSelection();
        RebuildScrollableContent();
    }

    public void SelectCandidate(MuseumDonationCandidate candidate)
    {
        if (candidate == null || !candidate.selectable)
            return;

        selectedCandidate = candidate;
        RefreshSelection();
    }

    public void Close()
    {
        ClearCards();
        slot = null;
        selectedCandidate = null;

        if (root != null)
            root.SetActive(false);
    }

    private void Continue()
    {
        if (owner == null || slot == null || selectedCandidate == null)
            return;

        owner.OpenDonationConfirmation(slot, selectedCandidate);
    }

    private void RefreshSelection()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            MuseumDonationItemCardUI card = cards[i];

            if (card != null)
                card.SetSelected(card.Candidate == selectedCandidate);
        }

        if (continueButton != null)
        {
            continueButton.interactable =
                selectedCandidate != null && selectedCandidate.selectable;
        }

        if (selectedDetailsText != null)
        {
            selectedDetailsText.text = selectedCandidate != null
                ? $"Selected value: {selectedCandidate.MarketValue:0.##} Gold\n" +
                  $"Reward: {selectedCandidate.preview.MuseumPoints:0.##} MP"
                : "Select one eligible inventory item.";
        }
    }

    private void RebuildScrollableContent()
    {
        if (content == null)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        float preferredHeight = LayoutUtility.GetPreferredHeight(content);

        if (preferredHeight > 0f)
        {
            content.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                preferredHeight);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();

        if (scrollRect != null)
        {
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void ResolveScrollReferences()
    {
        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);

        if (content == null && scrollRect != null)
            content = scrollRect.content;

        if (viewport == null && scrollRect != null)
            viewport = scrollRect.viewport;

        if (content == null)
        {
            Transform found = transform.Find("ScrollView/Viewport/Content");

            if (found != null)
                content = found as RectTransform;
        }
    }

    private void ConfigureScrollRect()
    {
        if (scrollRect == null)
            return;

        if (content != null)
            scrollRect.content = content;

        if (viewport != null)
            scrollRect.viewport = viewport;

        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = 25f;

        if (content != null)
        {
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();

            if (fitter == null)
                fitter = content.gameObject.AddComponent<ContentSizeFitter>();

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    private void ClearCards()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null)
                Destroy(cards[i].gameObject);
        }

        cards.Clear();
    }

    private static string BuildSlotLabel(MuseumSlotEntry value)
    {
        if (value == null)
            return "Unknown Museum slot";

        string wear = value.isVanilla
            ? "Vanilla"
            : value.wearTier.ToString();

        return $"Target: {wear} | {value.variant}";
    }

    private void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(Continue);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(Close);
    }
}
