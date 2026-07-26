using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Companion for the main Museum sidebar button. It closes Museum overlays
/// immediately, then resets the browser after the generic navigation action has
/// activated the Museum panel.
/// </summary>
[DisallowMultipleComponent]
public sealed class MuseumEntranceNavigationButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private MuseumPanelUI museumPanel;
    [SerializeField] private TrophyRoomPanelUI trophyRoomPanel;
    [SerializeField] private TrophySelectionPopupUI trophySelectionPopup;
    [SerializeField] private MuseumIdleIncomePopupUI idleIncomePopup;

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
        // Hide overlays before the generic sidebar listener changes panels. This
        // prevents the previous Trophy selection screen flashing for one frame.
        ResolveReferences();
        CloseMuseumOverlays();

        if (openRoutine != null)
            StopCoroutine(openRoutine);

        openRoutine = StartCoroutine(OpenEntranceAfterNavigation());
    }

    private IEnumerator OpenEntranceAfterNavigation()
    {
        yield return null;

        ResolveReferences();
        CloseMuseumOverlays();

        if (museumPanel != null)
            museumPanel.ShowEntrance();

        // A few overlay components refresh during OnEnable. Reassert the entrance
        // after the frame has fully settled without leaving the old UI visible.
        yield return new WaitForEndOfFrame();

        ResolveReferences();
        CloseMuseumOverlays();

        if (museumPanel != null)
            museumPanel.ShowEntrance();

        openRoutine = null;
    }

    private void CloseMuseumOverlays()
    {
        if (trophySelectionPopup != null)
            trophySelectionPopup.Close();

        if (trophyRoomPanel != null)
            trophyRoomPanel.Close();

        if (idleIncomePopup != null)
            idleIncomePopup.Close();
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

        if (trophyRoomPanel == null)
        {
            trophyRoomPanel = FindFirstObjectByType<TrophyRoomPanelUI>(
                FindObjectsInactive.Include);
        }

        if (trophySelectionPopup == null)
        {
            trophySelectionPopup = FindFirstObjectByType<TrophySelectionPopupUI>(
                FindObjectsInactive.Include);
        }

        if (idleIncomePopup == null)
        {
            idleIncomePopup = FindFirstObjectByType<MuseumIdleIncomePopupUI>(
                FindObjectsInactive.Include);
        }
    }
}