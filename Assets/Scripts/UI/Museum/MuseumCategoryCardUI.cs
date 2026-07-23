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
    [SerializeField] private MuseumProgressBarUI progressBar;
    [SerializeField] private GameObject lockedRoot;
    [SerializeField] private TMP_Text lockedText;

    private MuseumCategoryEntry entry;
    private MuseumPanelUI owner;

    public void Setup(MuseumCategoryEntry museumEntry, MuseumPanelUI panel)
    {
        entry = museumEntry;
        owner = panel;

        MuseumCategoryConfig config = entry != null ? entry.config : null;

        if (titleText != null)
            titleText.text = entry != null ? entry.DisplayName : "Category";

        if (descriptionText != null)
        {
            descriptionText.text = config != null
                ? config.description
                : "";
        }

        if (iconImage != null)
        {
            Sprite icon = config != null ? config.icon : null;
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
            iconImage.preserveAspect = true;
        }

        if (progressBar != null)
        {
            progressBar.SetProgress(
                entry != null ? entry.donatedSlots : 0,
                entry != null ? entry.totalSlots : 0);
        }

        bool unlocked = config == null ||
                        config.unlockDefinition == null ||
                        UnlockEvaluator.IsUnlocked(config.unlockDefinition);

        if (lockedRoot != null)
            lockedRoot.SetActive(!unlocked);

        if (lockedText != null)
            lockedText.text = unlocked ? "" : "Locked";

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
            button.interactable = unlocked && entry != null;
        }
    }

    private void HandleClicked()
    {
        if (owner != null && entry != null)
            owner.OpenCategory(entry);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }
}
