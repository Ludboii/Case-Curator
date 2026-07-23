using System;
using System.Collections.Generic;
using UnityEngine;

public enum MuseumCatalogFilterMode
{
    AllSkins = 0,
    ListedWeapons = 1,
    RareSpecialOnly = 2,
    NonRareSpecialOnly = 3
}

[Serializable]
public class MuseumCatalogFilter
{
    [Tooltip(
        "How the future MuseumCatalogService selects SkinData for this " +
        "category. Listed Weapons compares against SkinData.weaponName until " +
        "WeaponDefinition migration is complete.")]
    public MuseumCatalogFilterMode filterMode =
        MuseumCatalogFilterMode.ListedWeapons;

    [Tooltip(
        "Exact player-facing weapon/model names, for example AK-47, Bayonet or " +
        "Sport Gloves. No individual skin rows belong here.")]
    public List<string> weaponNames = new List<string>();

    [Header("Variant Visibility")]
    public bool includeNormal = true;
    public bool includeStatTrak = true;
    public bool includeSouvenir = true;
    public bool includeVanilla = true;
}

[Serializable]
public class MuseumCategoryConfig
{
    [Tooltip("Stable ID used by navigation and optional saved last-view state.")]
    public string categoryId;

    public string displayName;

    [TextArea(1, 3)]
    public string description;

    public Sprite icon;
    public int sortOrder;
    public bool includeInCompletion = true;
    public UnlockDefinition unlockDefinition;
    public MuseumCatalogFilter filter = new MuseumCatalogFilter();

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : categoryId;
}

[Serializable]
public class MuseumWingConfig
{
    [Tooltip("Stable Museum wing ID. Do not change after release.")]
    public string wingId;

    public string displayName;

    [TextArea(1, 4)]
    public string description;

    public Sprite icon;
    public int sortOrder;
    public bool includeInCompletion = true;
    public UnlockDefinition unlockDefinition;

    public List<MuseumCategoryConfig> categories =
        new List<MuseumCategoryConfig>();

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : wingId;
}

/// <summary>
/// Presentation and grouping configuration for the generated Museum catalog.
/// It defines wings and weapon/model categories, but never stores one row per
/// skin. The future MuseumCatalogService will still build exhibits directly
/// from GameDatabase.allSkins.
/// </summary>
[CreateAssetMenu(
    fileName = "MuseumCatalogConfig",
    menuName = "Case Curator/Museum/Museum Catalog Config")]
public class MuseumCatalogConfig : ScriptableObject
{
    [Tooltip("Wing opened when the Museum panel has no saved navigation state.")]
    public string defaultWingId;

    public List<MuseumWingConfig> wings =
        new List<MuseumWingConfig>();

    public MuseumWingConfig GetWingById(string wingId)
    {
        if (string.IsNullOrWhiteSpace(wingId) || wings == null)
            return null;

        for (int i = 0; i < wings.Count; i++)
        {
            MuseumWingConfig wing = wings[i];

            if (wing != null &&
                string.Equals(
                    wing.wingId,
                    wingId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return wing;
            }
        }

        return null;
    }

    public MuseumCategoryConfig GetCategoryById(string categoryId)
    {
        if (string.IsNullOrWhiteSpace(categoryId) || wings == null)
            return null;

        for (int wingIndex = 0; wingIndex < wings.Count; wingIndex++)
        {
            MuseumWingConfig wing = wings[wingIndex];

            if (wing == null || wing.categories == null)
                continue;

            for (int categoryIndex = 0;
                 categoryIndex < wing.categories.Count;
                 categoryIndex++)
            {
                MuseumCategoryConfig category =
                    wing.categories[categoryIndex];

                if (category != null &&
                    string.Equals(
                        category.categoryId,
                        categoryId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return category;
                }
            }
        }

        return null;
    }

    private void OnValidate()
    {
        defaultWingId = Trim(defaultWingId);

        if (wings == null)
            wings = new List<MuseumWingConfig>();

        for (int wingIndex = 0; wingIndex < wings.Count; wingIndex++)
        {
            MuseumWingConfig wing = wings[wingIndex];

            if (wing == null)
                continue;

            wing.wingId = Trim(wing.wingId);
            wing.displayName = Trim(wing.displayName);

            if (wing.categories == null)
                wing.categories = new List<MuseumCategoryConfig>();

            for (int categoryIndex = 0;
                 categoryIndex < wing.categories.Count;
                 categoryIndex++)
            {
                MuseumCategoryConfig category =
                    wing.categories[categoryIndex];

                if (category == null)
                    continue;

                category.categoryId = Trim(category.categoryId);
                category.displayName = Trim(category.displayName);

                if (category.filter == null)
                    category.filter = new MuseumCatalogFilter();

                if (category.filter.weaponNames == null)
                {
                    category.filter.weaponNames =
                        new List<string>();
                }

                for (int weaponIndex = 0;
                     weaponIndex < category.filter.weaponNames.Count;
                     weaponIndex++)
                {
                    category.filter.weaponNames[weaponIndex] =
                        Trim(category.filter.weaponNames[weaponIndex]);
                }
            }
        }
    }

    private static string Trim(string value)
    {
        return value != null ? value.Trim() : "";
    }
}
