using System;
using System.Collections.Generic;
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
    [SerializeField] private Color readyTextColor =
        new Color(1f, 0.9f, 0.25f, 1f);
    [SerializeField] private Color protectedTextColor =
        new Color(1f, 0.65f, 0.25f, 1f);
    [SerializeField] private Color missingTextColor =
        new Color(0.75f, 0.75f, 0.75f, 1f);

    private MuseumSkinEntry entry;
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

    public void Setup(MuseumSkinEntry museumEntry, MuseumPanelUI panel)
    {
        ResolveProgressBar();

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
        GetDonationAvailability(out int readyCount, out int protectedCount);

        if (foundStateText != null)
        {
            if (readyCount > 0)
            {
                foundStateText.text = readyCount == 1
                    ? "1 ready to donate"
                    : $"{readyCount} ready to donate";
                foundStateText.color = readyTextColor;
            }
            else if (protectedCount > 0)
            {
                foundStateText.text = protectedCount == 1
                    ? "Owned - protected"
                    : $"{protectedCount} owned - protected";
                foundStateText.color = protectedTextColor;
            }
            else
            {
                foundStateText.text = discovered ? "Discovered" : "Missing";
                foundStateText.color = discovered
                    ? discoveredTextColor
                    : missingTextColor;
            }
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

        ApplyRarityThenNameSiblingOrder();
    }

    /// <summary>
    /// Counts inventory instances that currently match one of this exhibit's
    /// unfilled exact slots. Eligible items are separated from owned copies that
    /// are blocked by protection rules such as Favorite or Trophy Room use.
    /// </summary>
    private void GetDonationAvailability(
        out int readyCount,
        out int protectedCount)
    {
        readyCount = 0;
        protectedCount = 0;

        MuseumService service = owner != null ? owner.Service : null;

        if (entry == null ||
            entry.slots == null ||
            service == null ||
            InventoryManager.Instance == null)
        {
            return;
        }

        HashSet<string> openDonationKeys =
            new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < entry.slots.Count; i++)
        {
            MuseumSlotEntry slot = entry.slots[i];

            if (slot != null &&
                !slot.donated &&
                !string.IsNullOrWhiteSpace(slot.donationKey))
            {
                openDonationKeys.Add(slot.donationKey);
            }
        }

        if (openDonationKeys.Count == 0)
            return;

        List<InventoryItem> inventory =
            InventoryManager.Instance.GetItemsCopy();

        for (int i = 0; i < inventory.Count; i++)
        {
            InventoryItem item = inventory[i];

            if (item == null || string.IsNullOrWhiteSpace(item.instanceId))
                continue;

            string donationKey = MuseumDonationKeyUtility.Build(item);

            if (string.IsNullOrWhiteSpace(donationKey) ||
                !openDonationKeys.Contains(donationKey))
            {
                continue;
            }

            MuseumDonationPreview preview =
                service.PreviewDonation(item.instanceId);

            if (preview != null && preview.canDonate)
                readyCount++;
            else
                protectedCount++;
        }
    }

    private void ResolveProgressBar()
    {
        if (progressBar == null)
            progressBar = GetComponentInChildren<MuseumProgressBarUI>(true);
    }

    /// <summary>
    /// Orders cards within the current weapon view by highest rarity first,
    /// then alphabetically by finish name within the same rarity.
    /// </summary>
    private void ApplyRarityThenNameSiblingOrder()
    {
        if (transform.parent == null || entry == null || entry.skin == null)
            return;

        MuseumSkinCardUI[] cards =
            transform.parent.GetComponentsInChildren<MuseumSkinCardUI>(true);

        int targetIndex = 0;

        for (int i = 0; i < cards.Length; i++)
        {
            MuseumSkinCardUI other = cards[i];

            if (other == null ||
                other == this ||
                other.entry == null ||
                other.entry.skin == null)
            {
                continue;
            }

            if (CompareEntries(other.entry, entry) < 0)
                targetIndex++;
        }

        transform.SetSiblingIndex(targetIndex);
    }

    private static int CompareEntries(
        MuseumSkinEntry a,
        MuseumSkinEntry b)
    {
        SkinData aSkin = a != null ? a.skin : null;
        SkinData bSkin = b != null ? b.skin : null;

        if (ReferenceEquals(aSkin, bSkin))
            return 0;

        if (aSkin == null)
            return 1;

        if (bSkin == null)
            return -1;

        // Rarity enum values are ordered from lower to higher rarity, so the
        // comparison is reversed to place the highest rarity first.
        int rarityCompare = ((int)bSkin.rarity).CompareTo((int)aSkin.rarity);

        if (rarityCompare != 0)
            return rarityCompare;

        return string.Compare(
            GetAlphabeticalName(aSkin),
            GetAlphabeticalName(bSkin),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GetAlphabeticalName(SkinData skin)
    {
        if (skin == null)
            return "";

        if (skin.isVanilla || string.IsNullOrWhiteSpace(skin.skinName))
            return "Vanilla";

        return skin.skinName.Trim();
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
