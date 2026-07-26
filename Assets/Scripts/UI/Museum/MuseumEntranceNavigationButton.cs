using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Companion for the main Museum sidebar button. The generic navigation action
/// activates the Museum panel; this component then closes Museum-owned overlays
/// and resets the browser to the entrance view.
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
        // Let the existing sidebar listener activate Panel_Museum first.
        yield return null;

        ResetMuseumOverlays();
        ResolveReferences();

        if (museumPanel != null)
            museumPanel.ShowEntrance();

        // Some popup roots are later siblings and can be re-enabled by their own
        // OnEnable work during the first frame. Repeat the reset at end of frame.
        yield return new WaitForEndOfFrame();

        ResetMuseumOverlays();

        if (museumPanel != null)
            museumPanel.ShowEntrance();

        openRoutine = null;
    }

    private static void ResetMuseumOverlays()
    {
        TrophySelectionPopupUI[] trophySelections =
            FindObjectsByType<TrophySelectionPopupUI>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int i = 0; i < trophySelections.Length; i++)
        {
            if (trophySelections[i] != null)
                trophySelections[i].Close();
        }

        TrophyRoomPanelUI[] trophyRooms =
            FindObjectsByType<TrophyRoomPanelUI>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int i = 0; i < trophyRooms.Length; i++)
        {
            if (trophyRooms[i] != null)
                trophyRooms[i].Close();
        }

        MuseumIdleIncomePopupUI[] incomePopups =
            FindObjectsByType<MuseumIdleIncomePopupUI>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int i = 0; i < incomePopups.Length; i++)
        {
            if (incomePopups[i] != null)
                incomePopups[i].Close();
        }
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
