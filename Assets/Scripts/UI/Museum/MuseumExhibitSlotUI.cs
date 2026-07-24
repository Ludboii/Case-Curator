using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One clickable Normal, StatTrak or Souvenir cell in the M3 exhibit table.
/// </summary>
public class MuseumExhibitSlotUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private TMP_Text ownedCountText;
    [SerializeField] private TMP_Text pointsText;

    [Header("State Colors")]
    [SerializeField] private Color completedColor = new Color(0.2f, 0.8f, 0.25f, 1f);
    [SerializeField] private Color availableColor = new Color(0.2f, 0.45f, 0.9f, 1f);
    [SerializeField] private Color missingColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color unavailableColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);

    private MuseumSlotEntry slot;
    private MuseumPanelUI owner;
    private MuseumService service;
    private bool unavailable;

    private void Awake()
    {
        ResolveReferences();
    }

    public void Setup(
        MuseumSlotEntry museumSlot,
        MuseumPanelUI panel,
        MuseumService museumService)
    {
        ResolveReferences();

        slot = museumSlot;
        owner = panel;
        service = museumService;
        unavailable = slot == null;

        if (unavailable)
        {
            ApplyUnavailable();
            return;
        }

        MuseumSlotUnlockState unlock = service != null
            ? service.EvaluateSlotUnlock(slot)
            : null;
        bool unlocked = unlock == null || unlock.isUnlocked;

        IReadOnlyList<MuseumDonationCandidate> candidates =
            service != null && !slot.donated
                ? service.GetDonationCandidates(slot)
                : new List<MuseumDonationCandidate>();

        int ownedCount = candidates != null ? candidates.Count : 0;
        MuseumDonationCandidate firstSelectable = FindFirstSelectable(candidates);

        if (stateText != null)
            stateText.text = slot.donated ? "V" : unlocked ? "X" : "L";

        if (ownedCountText != null)
        {
            ownedCountText.text = slot.donated
                ? "Collected"
                : ownedCount > 0
                    ? $"Owned: {ownedCount}"
                    : "Owned: 0";
        }

        if (pointsText != null)
        {
            pointsText.text = firstSelectable != null && firstSelectable.preview != null
                ? $"{firstSelectable.preview.MuseumPoints:0.##} MP"
                : "";
        }

        if (background != null)
        {
            background.color = slot.donated
                ? completedColor
                : !unlocked
                    ? lockedColor
                    : ownedCount > 0
                        ? availableColor
                        : missingColor;
        }

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
            button.interactable = !slot.donated && unlocked;
        }
    }

    public void SetupUnavailable()
    {
        ResolveReferences();
        slot = null;
        owner = null;
        service = null;
        unavailable = true;
        ApplyUnavailable();
    }

    private void ApplyUnavailable()
    {
        if (stateText != null)
            stateText.text = "-";

        if (ownedCountText != null)
            ownedCountText.text = "";

        if (pointsText != null)
            pointsText.text = "";

        if (background != null)
            background.color = unavailableColor;

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.interactable = false;
        }
    }

    private void HandleClicked()
    {
        if (unavailable || slot == null || owner == null)
            return;

        owner.OpenDonationSelection(slot);
    }

    private static MuseumDonationCandidate FindFirstSelectable(
        IReadOnlyList<MuseumDonationCandidate> candidates)
    {
        if (candidates == null)
            return null;

        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] != null && candidates[i].selectable)
                return candidates[i];
        }

        return null;
    }

    private void ResolveReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (background == null)
            background = GetComponent<Image>();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }
}
