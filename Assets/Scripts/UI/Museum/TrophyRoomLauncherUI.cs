using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Museum entrance card for M7. It reports pedestal progress and opens the
/// horizontal Trophy Room without modifying stored items itself.
/// </summary>
public sealed class TrophyRoomLauncherUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TrophyRoomPanelUI panel;
    [SerializeField] private TMP_Text summaryText;

    private TrophyRoomService service;
    private bool subscribed;

    private void Awake()
    {
        ResolveReferences();

        if (button != null)
        {
            button.onClick.RemoveListener(OpenTrophyRoom);
            button.onClick.AddListener(OpenTrophyRoom);
        }
    }

    private void Start()
    {
        ResolveService();
        Subscribe();
        Refresh();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResolveService();
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (button != null)
            button.onClick.RemoveListener(OpenTrophyRoom);
    }

    public void OpenTrophyRoom()
    {
        ResolveReferences();
        ResolveService();

        if (panel == null)
        {
            Debug.LogWarning(
                "TrophyRoomLauncherUI: Trophy Room panel is not assigned.",
                this);
            return;
        }

        panel.Open();
    }

    public void Refresh()
    {
        ResolveService();

        if (service == null || summaryText == null)
            return;

        TrophyRoomSnapshot snapshot = service.GetSnapshot();

        if (snapshot.unlockedSlotCount <= 0)
        {
            summaryText.text =
                "Reach Global Elite and purchase Trophy Pedestal I";
            return;
        }

        summaryText.text =
            $"{snapshot.occupiedSlotCount:N0} / " +
            $"{snapshot.unlockedSlotCount:N0} pedestals filled  •  " +
            $"{snapshot.totalWeightedPower:N0} Trophy Power\n" +
            TrophyRoomPanelUI.FormatFocusBonus(
                snapshot.focus,
                snapshot.activeBonusFraction);
    }

    private void ResolveReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (panel == null)
        {
            panel = FindFirstObjectByType<TrophyRoomPanelUI>(
                FindObjectsInactive.Include);
        }
    }

    private void ResolveService()
    {
        if (service == null && SaveManager.Instance != null)
            service = TrophyRoomService.GetOrCreate();
    }

    private void Subscribe()
    {
        if (service == null || subscribed)
            return;

        service.OnTrophyRoomChanged += Refresh;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (service != null && subscribed)
            service.OnTrophyRoomChanged -= Refresh;

        subscribed = false;
    }
}
