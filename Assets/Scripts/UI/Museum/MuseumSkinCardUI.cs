using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MuseumSkinCardUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image rarityBackground;
    [SerializeField] private Image skinImage;
    [SerializeField] private TMP_Text weaponNameText;
    [SerializeField] private TMP_Text skinNameText;
    [SerializeField] private TMP_Text foundStateText;
    [SerializeField] private MuseumProgressBarUI progressBar;
    [SerializeField] private GameObject discoveredIndicator;

    [Header("State Colors")]
    [SerializeField] private Color discoveredTextColor =
        new Color(0.35f, 1f, 0.35f, 1f);
    [SerializeField] private Color missingTextColor =
        new Color(0.75f, 0.75f, 0.75f, 1f);

    private MuseumSkinEntry entry;
    private MuseumPanelUI owner;

    public void Setup(MuseumSkinEntry museumEntry, MuseumPanelUI panel)
    {
        entry = museumEntry;
        owner = panel;
        SkinData skin = entry != null ? entry.skin : null;

        if (rarityBackground != null)
        {
            Color rarityColor = skin != null
                ? RarityColorUtility.GetColor(skin.rarity)
                : Color.gray;
            rarityBackground.color = rarityColor;
        }

        if (skinImage != null)
        {
            skinImage.sprite = skin != null ? skin.icon : null;
            skinImage.enabled = skin != null && skin.icon != null;
            skinImage.preserveAspect = true;
        }

        if (weaponNameText != null)
            weaponNameText.text = skin != null ? skin.weaponName : "Unknown";

        if (skinNameText != null)
        {
            skinNameText.text = skin == null
                ? "Unknown Skin"
                : skin.isVanilla || string.IsNullOrWhiteSpace(skin.skinName)
                    ? "Vanilla"
                    : skin.skinName;
        }

        bool discovered = entry != null && entry.DonatedSlots > 0;

        if (foundStateText != null)
        {
            foundStateText.text = discovered ? "Discovered" : "Missing";
            foundStateText.color = discovered
                ? discoveredTextColor
                : missingTextColor;
        }

        if (discoveredIndicator != null)
            discoveredIndicator.SetActive(discovered);

        if (progressBar != null)
        {
            progressBar.SetProgress(
                entry != null ? entry.DonatedSlots : 0,
                entry != null ? entry.TotalSlots : 0);
        }

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
            button.interactable = entry != null && skin != null;
        }
    }

    private void HandleClicked()
    {
        if (owner != null && entry != null)
            owner.OpenSkin(entry);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }
}
