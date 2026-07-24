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

        // The staircase is an overlay. It must never begin open merely because
        // its scene object was left active while building the hierarchy.
        if (staircaseRoot != null)
            staircaseRoot.SetActive(false);

        RefreshCardVisibility(true);
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (!initialized && staircaseRoot != null)
            staircaseRoot.SetActive(false);

        RefreshCardVisibility(true);
    }

    private void LateUpdate()
    {
        RefreshCardVisibility(false);
    }

    public void RefreshNow()
    {
        ResolveReferences();
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

        if (staircaseCard != null &&
            staircaseCard.activeSelf != wingVisible)
        {
            staircaseCard.SetActive(wingVisible);
        }
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
