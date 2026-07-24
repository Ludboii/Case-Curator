using UnityEngine;

/// <summary>
/// Keeps the M4 staircase overlay closed until its launcher opens it and keeps
/// the entrance card visible only while the Museum wing/entrance view is active.
/// Attach this component to the same object as MuseumPanelUI.
/// </summary>
public class MuseumStaircasePresentationGuard : MonoBehaviour
{
    [Header("Museum")]
    [SerializeField] private MuseumPanelUI museumPanel;
    [SerializeField] private GameObject museumWingView;
    [SerializeField] private GameObject museumCategoryView;
    [SerializeField] private GameObject museumWeaponView;
    [SerializeField] private GameObject museumSkinView;

    [Header("Staircase")]
    [SerializeField] private GameObject staircaseRoot;
    [SerializeField] private GameObject staircaseCard;

    [Tooltip(
        "When enabled, missing references are resolved by exact or partial " +
        "child object names. Explicit Inspector assignments remain preferred.")]
    [SerializeField] private bool autoResolveByName = true;

    private bool lastWingVisible;
    private bool initialized;
    private bool restoringEntrance;

    private void Awake()
    {
        ResolveReferences();

        if (staircaseRoot != null)
            staircaseRoot.SetActive(false);

        EnsureNormalMuseumViewVisible();
        RefreshCardVisibility(true);
    }

    private void Start()
    {
        ResolveReferences();
        EnsureNormalMuseumViewVisible();
        RefreshCardVisibility(true);
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (!initialized && staircaseRoot != null)
            staircaseRoot.SetActive(false);

        EnsureNormalMuseumViewVisible();
        RefreshCardVisibility(true);
    }

    private void LateUpdate()
    {
        EnsureNormalMuseumViewVisible();
        RefreshCardVisibility(false);
    }

    public void RefreshNow()
    {
        ResolveReferences();
        EnsureNormalMuseumViewVisible();
        RefreshCardVisibility(true);
    }

    private void EnsureNormalMuseumViewVisible()
    {
        if (restoringEntrance || !gameObject.activeInHierarchy)
            return;

        bool staircaseOpen =
            staircaseRoot != null && staircaseRoot.activeInHierarchy;

        if (staircaseOpen)
            return;

        bool anyNormalViewActive =
            IsActive(museumWingView) ||
            IsActive(museumCategoryView) ||
            IsActive(museumWeaponView) ||
            IsActive(museumSkinView);

        if (anyNormalViewActive)
            return;

        restoringEntrance = true;

        try
        {
            if (museumPanel != null)
            {
                museumPanel.ShowEntrance();
            }
            else if (museumWingView != null)
            {
                museumWingView.SetActive(true);
            }
        }
        finally
        {
            restoringEntrance = false;
        }
    }

    private void RefreshCardVisibility(bool force)
    {
        bool wingVisible = IsActive(museumWingView);

        if (!force && initialized && wingVisible == lastWingVisible)
            return;

        initialized = true;
        lastWingVisible = wingVisible;

        if (staircaseCard == null)
            return;

        // When the card is already inside WingView, the parent controls its
        // visibility. Never disable an ancestor or the WingView itself.
        if (staircaseCard == museumWingView ||
            IsAncestor(staircaseCard.transform, museumWingView != null
                ? museumWingView.transform
                : null))
        {
            return;
        }

        bool cardInsideWing =
            museumWingView != null &&
            staircaseCard.transform.IsChildOf(museumWingView.transform);

        if (!cardInsideWing && staircaseCard.activeSelf != wingVisible)
            staircaseCard.SetActive(wingVisible);
    }

    private void ResolveReferences()
    {
        if (museumPanel == null)
            museumPanel = GetComponent<MuseumPanelUI>();

        if (!autoResolveByName)
            return;

        if (museumWingView == null)
            museumWingView = FindChildObject(transform, "WingView");

        if (museumCategoryView == null)
            museumCategoryView = FindChildObject(transform, "CategoryView");

        if (museumWeaponView == null)
            museumWeaponView = FindChildObject(transform, "WeaponView");

        if (museumSkinView == null)
            museumSkinView = FindChildObject(transform, "SkinView");

        if (staircaseRoot == null)
        {
            MuseumStaircaseUI staircase =
                GetComponentInChildren<MuseumStaircaseUI>(true);

            staircaseRoot = staircase != null
                ? staircase.gameObject
                : FindChildObject(
                    transform,
                    "MuseumStaircaseRoot",
                    "StaircaseRoot");
        }

        if (staircaseCard == null)
        {
            MuseumStaircaseLauncherUI launcher =
                GetComponentInChildren<MuseumStaircaseLauncherUI>(true);

            staircaseCard = launcher != null
                ? launcher.gameObject
                : FindChildObject(
                    transform,
                    "MuseumStaircaseCard",
                    "StaircaseCard");
        }
    }

    private static bool IsActive(GameObject value)
    {
        return value != null && value.activeInHierarchy;
    }

    private static bool IsAncestor(Transform possibleAncestor, Transform child)
    {
        if (possibleAncestor == null || child == null)
            return false;

        return child.IsChildOf(possibleAncestor);
    }

    private static GameObject FindChildObject(
        Transform parent,
        params string[] names)
    {
        if (parent == null || names == null)
            return null;

        Transform[] children =
            parent.GetComponentsInChildren<Transform>(true);

        for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
        {
            string expected = names[nameIndex];

            for (int childIndex = 0; childIndex < children.Length; childIndex++)
            {
                Transform child = children[childIndex];

                if (child != null &&
                    string.Equals(
                        child.name,
                        expected,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return child.gameObject;
                }
            }
        }

        for (int childIndex = 0; childIndex < children.Length; childIndex++)
        {
            Transform child = children[childIndex];

            if (child == null)
                continue;

            string childName = child.name.ToLowerInvariant();

            for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
            {
                string expected = names[nameIndex].ToLowerInvariant();

                if (childName.Contains(expected))
                    return child.gameObject;
            }
        }

        return null;
    }
}
