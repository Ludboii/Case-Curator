using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Companion for the main Museum sidebar button. It waits until the generic
/// navigation action has activated the Museum panel, then always resets the
/// browser to the Museum Entrance instead of preserving a previous subview.
/// </summary>
[DisallowMultipleComponent]
public sealed class MuseumEntranceNavigationButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private MuseumPanelUI museumPanel;

    private Coroutine openRoutine;

    private void Awake()
    {
        ResolveReferences();

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }

    private void HandleClicked()
    {
        if (openRoutine != null)
            StopCoroutine(openRoutine);

        openRoutine = StartCoroutine(OpenEntranceAfterNavigation());
    }

    private IEnumerator OpenEntranceAfterNavigation()
    {
        // Let the existing sidebar navigation listener activate the Museum panel
        // first. Running at end of frame also makes this independent of listener order.
        yield return null;

        ResolveReferences();

        if (museumPanel != null)
            museumPanel.ShowEntrance();

        openRoutine = null;
    }

    private void ResolveReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (museumPanel == null)
        {
            museumPanel = FindFirstObjectByType<MuseumPanelUI>(
                FindObjectsInactive.Include);
        }
    }
}
