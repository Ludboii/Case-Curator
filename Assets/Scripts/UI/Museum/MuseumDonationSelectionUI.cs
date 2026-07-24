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
    [SerializeField] private Transform content;
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
            emptyStateText.gameObject.SetActive(candidates == null || candidates.Count == 0);
            emptyStateText.text =
                "You do not own an item matching this exact wear and variant.";
        }

        RefreshSelection();

        if (content is RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }
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
            continueButton.interactable =
                selectedCandidate != null && selectedCandidate.selectable;

        if (selectedDetailsText != null)
        {
            selectedDetailsText.text = selectedCandidate != null
                ? $"Selected value: {selectedCandidate.MarketValue:0.##} Gold\n" +
                  $"Reward: {selectedCandidate.preview.MuseumPoints:0.##} MP"
                : "Select one eligible inventory item.";
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
