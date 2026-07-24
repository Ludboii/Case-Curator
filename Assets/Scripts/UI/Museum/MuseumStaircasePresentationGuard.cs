using UnityEngine;

/// <summary>
/// Keeps the M4 staircase overlay closed until its launcher opens it and keeps
/// the entrance card visible only while the Museum wing/entrance view is active.
/// Attach this component to the persistent MuseumPanelUI object, not to either
/// object that it hides.
/// </summary>
public class MuseumStaircasePresentationGuard : MonoBehaviour
{
    [SerializeField] private GameObject museumWingView;
    [SerializeField] private GameObject staircaseRoot;
    [SerializeField] private GameObject staircaseCard;

    [Tooltip(
        "When enabled, missing references are resolved by exact or partial " +
        "child object names. Explicit Inspector assignments remain preferred.")]
    [SerializeField] private bool autoResolveByName = true;

    private bool lastWingVisible;
    private bool initialized;

    private void Awake()
    {
        ResolveReferences();
        ValidateReferences();

        // The staircase is an overlay. It must never begin open merely because
        // its scene object was left active while building the hierarchy.
        if (CanSafelyControl(staircaseRoot))
            staircaseRoot.SetActive(false);

        RefreshCardVisibility(true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        ValidateReferences();

        if (!initialized && CanSafelyControl(staircaseRoot))
            staircaseRoot.SetActive(false);

        RefreshCardVisibility(true);
    }

    private void OnValidate()
    {
        ResolveReferences();
        ValidateReferences();
    }

    private void LateUpdate()
    {
        RefreshCardVisibility(false);
    }

    public void RefreshNow()
    {
        ResolveReferences();
        ValidateReferences();
        RefreshCardVisibility(true);
    }

    private void RefreshCardVisibility(bool force)
    {
        bool wingVisible =
            museumWingView != null &&
            museumWingView.activeInHierarchy;

        if (!force && initialized && wingVisible == lastWingVisible)
            return;

        initialized = true;
        lastWingVisible = wingVisible;

        // A card placed under WingView already inherits WingView visibility.
        // Keep its own active state enabled instead of turning it off while the
        // parent is hidden, which avoids it remaining disabled when WingView is
        // shown again. Only explicitly control cards outside WingView.
        if (!CanSafelyControl(staircaseCard))
            return;

        bool cardIsChildOfWing =
            museumWingView != null &&
            staircaseCard.transform.IsChildOf(museumWingView.transform);

        bool shouldBeActive = cardIsChildOfWing ? true : wingVisible;

        if (staircaseCard.activeSelf != shouldBeActive)
            staircaseCard.SetActive(shouldBeActive);
    }

    private void ResolveReferences()
    {
        if (!autoResolveByName)
            return;

        if (museumWingView == null)
        {
            museumWingView = FindChildObject(
                transform,
                "WingView",
                "MuseumWingView",
                "Wing View");
        }

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

    private void ValidateReferences()
    {
        // Never permit a bad Inspector assignment to disable WingView or the
        // entire Museum panel. These mistakes are easy when dragging objects.
        if (staircaseCard == museumWingView ||
            staircaseCard == gameObject ||
            IsAncestorOf(staircaseCard, museumWingView))
        {
            Debug.LogWarning(
                "MuseumStaircasePresentationGuard: Staircase Card was assigned " +
                "to WingView, Panel_Museum, or one of their ancestors. The bad " +
                "reference was cleared. Assign the actual MuseumStaircaseCard.",
                this);
            staircaseCard = null;
        }

        if (staircaseRoot == museumWingView ||
            staircaseRoot == gameObject ||
            IsAncestorOf(staircaseRoot, museumWingView))
        {
            Debug.LogWarning(
                "MuseumStaircasePresentationGuard: Staircase Root was assigned " +
                "to WingView, Panel_Museum, or one of their ancestors. The bad " +
                "reference was cleared. Assign MuseumStaircaseRoot.",
                this);
            staircaseRoot = null;
        }
    }

    private bool CanSafelyControl(GameObject target)
    {
        if (target == null || target == gameObject || target == museumWingView)
            return false;

        return !IsAncestorOf(target, museumWingView);
    }

    private static bool IsAncestorOf(
        GameObject possibleAncestor,
        GameObject possibleChild)
    {
        if (possibleAncestor == null || possibleChild == null)
            return false;

        return possibleChild.transform.IsChildOf(
            possibleAncestor.transform);
    }

    private static GameObject FindChildObject(
        Transform parent,
        params string[] names)
    {
        if (parent == null || names == null)
            return null;

        Transform[] children =
            parent.GetComponentsInChildren<Transform>(true);

        for (int nameIndex = 0;
             nameIndex < names.Length;
             nameIndex++)
        {
            string expected = names[nameIndex];

            for (int childIndex = 0;
                 childIndex < children.Length;
                 childIndex++)
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

        for (int childIndex = 0;
             childIndex < children.Length;
             childIndex++)
        {
            Transform child = children[childIndex];

            if (child == null)
                continue;

            string childName = child.name.ToLowerInvariant();

            for (int nameIndex = 0;
                 nameIndex < names.Length;
                 nameIndex++)
            {
                string expected =
                    names[nameIndex].ToLowerInvariant();

                if (childName.Contains(expected))
                    return child.gameObject;
            }
        }

        return null;
    }
}
