using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MuseumWeaponCardUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text skinCountText;
    [SerializeField] private MuseumProgressBarUI progressBar;

    private MuseumWeaponEntry entry;
    private MuseumPanelUI owner;

    private void Awake()
    {
        ResolveProgressBar();
    }

    private void Reset()
    {
        ResolveProgressBar();
    }

    private void OnValidate()
    {
        ResolveProgressBar();
    }

    public void Setup(MuseumWeaponEntry museumEntry, MuseumPanelUI panel)
    {
        ResolveProgressBar();

        entry = museumEntry;
        owner = panel;

        if (titleText != null)
            titleText.text = entry != null ? entry.weaponName : "Weapon";

        if (skinCountText != null)
        {
            int count = entry != null && entry.skins != null
                ? entry.skins.Count
                : 0;
            skinCountText.text = $"{count} skins";
        }

        if (iconImage != null)
        {
            Sprite icon = GetRepresentativeIcon(entry);
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

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
            button.interactable = entry != null;
        }
    }

    private void ResolveProgressBar()
    {
        if (progressBar == null)
            progressBar = GetComponentInChildren<MuseumProgressBarUI>(true);
    }

    private void HandleClicked()
    {
        if (owner != null && entry != null)
            owner.OpenWeapon(entry);
    }

    private static Sprite GetRepresentativeIcon(MuseumWeaponEntry weapon)
    {
        if (weapon == null || weapon.skins == null)
            return null;

        for (int i = 0; i < weapon.skins.Count; i++)
        {
            SkinData skin = weapon.skins[i] != null
                ? weapon.skins[i].skin
                : null;

            if (skin != null && skin.icon != null)
                return skin.icon;
        }

        return null;
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }
}