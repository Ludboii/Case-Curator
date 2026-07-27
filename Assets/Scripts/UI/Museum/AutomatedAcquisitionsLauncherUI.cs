using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Museum-entrance card/button for the Automated Acquisitions department.
/// The card remains visible while locked and displays both late-game conditions.
/// </summary>
public class AutomatedAcquisitionsLauncherUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text lockText;
    [SerializeField] private GameObject lockedRoot;
    [SerializeField] private AutomatedAcquisitionsPanelUI panel;

    private AutoAcquisitionService service;

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
        }
    }

    private void OnEnable()
    {
        ResolveService();
        RefreshState();

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnProgressChanged -= RefreshState;
            SaveManager.Instance.OnProgressChanged += RefreshState;
        }

        MuseumMilestoneService milestones =
            MuseumMilestoneService.GetOrCreate();

        if (milestones != null)
        {
            milestones.OnMilestonesChanged -= RefreshState;
            milestones.OnMilestonesChanged += RefreshState;
        }
    }

    private void OnDisable()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.OnProgressChanged -= RefreshState;

        MuseumMilestoneService milestones = MuseumMilestoneService.Instance;

        if (milestones != null)
            milestones.OnMilestonesChanged -= RefreshState;
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }

    public void RefreshState()
    {
        ResolveService();
        bool unlocked = service != null &&
                        service.IsWingUnlocked(out string reason);

        if (titleText != null)
            titleText.text = "AUTOMATED ACQUISITIONS";

        if (descriptionText != null)
        {
            descriptionText.text =
                "Research containers, fund processing lines and claim pulls " +
                "from the Intake Vault.";
        }

        if (lockedRoot != null)
            lockedRoot.SetActive(!unlocked);

        if (lockText != null)
            lockText.text = unlocked ? "DEPARTMENT OPEN" : reason;

        if (button != null)
            button.interactable = unlocked && panel != null;

        MuseumLockVisualUtility.Apply(gameObject, unlocked, 0.55f);
    }

    private void HandleClicked()
    {
        ResolveService();

        if (service == null ||
            !service.IsWingUnlocked(out string reason))
        {
            Debug.Log(reason, this);
            return;
        }

        if (panel != null)
            panel.Open();
    }

    private void ResolveService()
    {
        if (service == null)
            service = AutoAcquisitionService.GetOrCreate();
    }
}
