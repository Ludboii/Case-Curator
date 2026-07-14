using System;
using TMPro;
using UnityEngine;

public class StickerSlotVisibilityController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SkinInspectUI skinInspectUI;
    [SerializeField] private GameObject stickerSlotsRoot;

    [Header("Optional Overrides")]
    [Tooltip("Additional exact weapon names that should not show sticker slots.")]
    [SerializeField] private string[] additionalNonStickerWeaponNames;

    private string lastWeaponName;
    private string lastRarityText;
    private bool lastVisibleState;
    private bool hasAppliedState;

    private void OnEnable()
    {
        RefreshVisibility(true);
    }

    private void LateUpdate()
    {
        RefreshVisibility(false);
    }

    public void RefreshNow()
    {
        RefreshVisibility(true);
    }

    private void RefreshVisibility(bool force)
    {
        if (skinInspectUI == null || stickerSlotsRoot == null)
            return;

        string weaponName = GetText(skinInspectUI.weaponNameText);
        string rarity = GetText(skinInspectUI.rarityText);

        if (!force &&
            weaponName == lastWeaponName &&
            rarity == lastRarityText)
        {
            return;
        }

        lastWeaponName = weaponName;
        lastRarityText = rarity;

        bool shouldShow = !IsKnifeOrGlove(weaponName, rarity);

        if (!hasAppliedState || shouldShow != lastVisibleState)
        {
            stickerSlotsRoot.SetActive(shouldShow);
            lastVisibleState = shouldShow;
            hasAppliedState = true;
        }
    }

    private bool IsKnifeOrGlove(string weaponName, string rarity)
    {
        string normalizedWeapon = Normalize(weaponName);
        string normalizedRarity = Normalize(rarity);

        if (MatchesAdditionalOverride(normalizedWeapon))
            return true;

        // All current Rare Special items are knives or gloves. The weapon-name
        // checks remain as a safeguard if the rarity label changes visually.
        if (normalizedRarity.Contains("rarespecial") ||
            normalizedRarity.Contains("rare special"))
        {
            return true;
        }

        string[] blockedTerms =
        {
            "knife",
            "bayonet",
            "karambit",
            "daggers",
            "dagger",
            "glove",
            "hand wraps",
            "handwraps",
            "falchion",
            "bowie",
            "huntsman",
            "butterfly",
            "shadow",
            "navaja",
            "stiletto",
            "talon",
            "ursus",
            "nomad",
            "paracord",
            "survival",
            "skeleton",
            "kukri",
            "gut",
            "flip"
        };

        for (int i = 0; i < blockedTerms.Length; i++)
        {
            if (normalizedWeapon.Contains(blockedTerms[i]))
                return true;
        }

        return false;
    }

    private bool MatchesAdditionalOverride(string normalizedWeapon)
    {
        if (additionalNonStickerWeaponNames == null)
            return false;

        for (int i = 0; i < additionalNonStickerWeaponNames.Length; i++)
        {
            string overrideName = Normalize(
                additionalNonStickerWeaponNames[i]);

            if (!string.IsNullOrWhiteSpace(overrideName) &&
                string.Equals(
                    normalizedWeapon,
                    overrideName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetText(TMP_Text text)
    {
        return text != null ? text.text ?? "" : "";
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant();
    }
}
