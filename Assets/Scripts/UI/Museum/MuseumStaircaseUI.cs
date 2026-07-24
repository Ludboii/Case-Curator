using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phase M4 staircase screen. It renders all 80 generated milestone assets,
/// preserves a selected step, allows manual claims and refreshes claimable state
/// whenever Museum Points or milestone claims change.
/// </summary>
public class MuseumStaircaseUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Header")]
    [SerializeField] private TMP_Text currentPointsText;
    [SerializeField] private TMP_Text currentBandText;
    [SerializeField] private TMP_Text claimedStepsText;
    [SerializeField] private TMP_Text nextMilestoneText;
    [SerializeField] private MuseumProgressBarUI totalProgressBar;

    [Header("Milestone List")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform content;
    [SerializeField] private MuseumMilestoneStepUI stepPrefab;
    [SerializeField] private TMP_Text emptyStateText;

    [Header("Selected Step")]
    [SerializeField] private MuseumRewardPreviewUI rewardPreview;
    [SerializeField] private Button claimButton;
    [SerializeField] private TMP_Text claimButtonText;

    [Header("Controls")]
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text resultText;

    [Header("Layout")]
    [SerializeField, Min(0f)] private float stepSpacing = 12f;
    [SerializeField] private bool scrollToSelectedOnOpen = true;

    private readonly List<MuseumMilestoneStepUI> spawnedSteps =
        new List<MuseumMilestoneStepUI>();

    private MuseumMilestoneService service;
    private MuseumMilestoneState selectedState;
    private string selectedMilestoneId;
    private bool subscribed;

    public bool IsOpen => root != null && root.activeSelf;
    public MuseumMilestoneState SelectedState => selectedState;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        ResolveReferences();
        SetupButton(claimButton, ClaimSelected);
        SetupButton(closeButton, Close);
        HideResult();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        RemoveButton(claimButton, ClaimSelected);
        RemoveButton(closeButton, Close);
    }

    public void Open(MuseumMilestoneService milestoneService = null)
    {
        ResolveReferences();

        service = milestoneService != null
            ? milestoneService
            : MuseumMilestoneService.GetOrCreate();

        if (root == null)
            root = gameObject;

        root.SetActive(true);
        Subscribe();
        HideResult();
        Refresh(true);
    }

    public void Close()
    {
        Unsubscribe();
        ClearSteps();
        selectedState = null;
        selectedMilestoneId = "";

        if (rewardPreview != null)
            rewardPreview.Hide();

        if (root != null)
            root.SetActive(false);
    }

    public void Refresh(bool chooseDefaultSelection = false)
    {
        ResolveReferences();

        if (service == null)
            service = MuseumMilestoneService.GetOrCreate();

        IReadOnlyList<MuseumMilestoneState> states =
            service != null
                ? service.GetMilestoneStates()
                : new List<MuseumMilestoneState>();

        RefreshHeader(states);
        BuildSteps(states);

        MuseumMilestoneState selection =
            FindState(states, selectedMilestoneId);

        if (selection == null && chooseDefaultSelection)
            selection = FindDefaultSelection(states);

        if (selection == null && states.Count > 0)
            selection = states[0];

        SelectMilestone(selection);

        if (emptyStateText != null)
        {
            bool empty = states == null || states.Count == 0;
            emptyStateText.gameObject.SetActive(empty);
            emptyStateText.text = empty
                ? "No Museum milestone assets were found."
                : "";
        }

        if (scrollToSelectedOnOpen && chooseDefaultSelection)
            ScrollToSelected();
    }

    public void SelectMilestone(MuseumMilestoneState state)
    {
        selectedState = state;
        selectedMilestoneId =
            state != null ? state.MilestoneId : "";

        for (int i = 0; i < spawnedSteps.Count; i++)
        {
            MuseumMilestoneStepUI step = spawnedSteps[i];

            if (step != null)
                step.SetSelected(step.State == selectedState);
        }

        if (rewardPreview != null)
        {
            if (selectedState != null)
                rewardPreview.Show(selectedState);
            else
                rewardPreview.Hide();
        }

        RefreshClaimButton();
    }

    private void ClaimSelected()
    {
        if (service == null ||
            selectedState == null ||
            selectedState.data == null)
        {
            return;
        }

        MuseumMilestoneClaimResult result =
            service.Claim(selectedState.MilestoneId);

        ShowClaimResult(result);

        if (result != null && result.success)
        {
            selectedMilestoneId =
                result.milestone != null
                    ? result.milestone.milestoneId
                    : selectedMilestoneId;

            Refresh(false);
        }
        else
        {
            RefreshClaimButton();
        }
    }

    private void RefreshHeader(
        IReadOnlyList<MuseumMilestoneState> states)
    {
        double points = service != null &&
                        service.IsReady &&
                        SaveManager.Instance != null &&
                        SaveManager.Instance.Museum != null
            ? Math.Max(
                0d,
                SaveManager.Instance.Museum.museumPoints)
            : 0d;

        if (currentPointsText != null)
            currentPointsText.text = $"{points:N0} Museum Points";

        MuseumMilestoneState current =
            service != null
                ? service.GetCurrentReachedMilestone()
                : null;

        if (currentBandText != null)
        {
            currentBandText.text =
                current != null && current.data != null
                    ? current.data.BandDisplayName
                    : "Dusty Lobby";
        }

        int claimed = service != null
            ? service.GetClaimedCount()
            : 0;

        int total = states != null ? states.Count : 0;

        if (claimedStepsText != null)
            claimedStepsText.text = $"{claimed} / {total} steps claimed";

        MuseumMilestoneState next =
            service != null
                ? service.GetNextUnclaimedMilestone()
                : null;

        if (nextMilestoneText != null)
        {
            if (next == null || next.data == null)
            {
                nextMilestoneText.text = "Museum Staircase complete";
            }
            else if (next.IsClaimable)
            {
                nextMilestoneText.text =
                    $"Step {next.Step:00} is ready to claim";
            }
            else
            {
                double remaining = Math.Max(
                    0d,
                    next.requiredMuseumPoints - points);

                nextMilestoneText.text =
                    $"Next: Step {next.Step:00} at " +
                    $"{next.requiredMuseumPoints:N0} MP " +
                    $"({remaining:N0} remaining)";
            }
        }

        if (totalProgressBar != null)
        {
            int currentValue = Mathf.Clamp(
                (int)Math.Round(points),
                0,
                (int)MuseumMilestone80Defaults.FinalMuseumPoints);

            totalProgressBar.SetProgress(
                currentValue,
                (int)MuseumMilestone80Defaults.FinalMuseumPoints);
        }
    }

    private void BuildSteps(
        IReadOnlyList<MuseumMilestoneState> states)
    {
        ClearSteps();
        ConfigureScrollContent();

        if (states == null ||
            content == null ||
            stepPrefab == null)
        {
            return;
        }

        for (int i = 0; i < states.Count; i++)
        {
            MuseumMilestoneState state = states[i];

            if (state == null || state.data == null)
                continue;

            MuseumMilestoneStepUI step =
                Instantiate(stepPrefab, content);

            step.gameObject.SetActive(true);
            step.Setup(state, this);
            spawnedSteps.Add(step);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        if (scrollRect != null)
        {
            scrollRect.content = content;
            scrollRect.vertical = true;
            scrollRect.horizontal = false;
        }
    }

    private void ConfigureScrollContent()
    {
        if (content == null)
            return;

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);

        VerticalLayoutGroup layout =
            content.GetComponent<VerticalLayoutGroup>();

        if (layout == null)
            layout = content.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.spacing = stepSpacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter =
            content.GetComponent<ContentSizeFitter>();

        if (fitter == null)
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit =
            ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;
    }

    private void RefreshClaimButton()
    {
        bool claimable =
            selectedState != null &&
            selectedState.IsClaimable &&
            !selectedState.runtimePreviewOnly;

        if (claimButton != null)
            claimButton.interactable = claimable;

        if (claimButtonText != null)
        {
            if (selectedState == null)
                claimButtonText.text = "Select a Step";
            else if (selectedState.runtimePreviewOnly)
                claimButtonText.text = "Generate Assets First";
            else if (selectedState.IsClaimed)
                claimButtonText.text = "Claimed";
            else if (selectedState.IsClaimable)
                claimButtonText.text = "Claim Reward";
            else
                claimButtonText.text = "Locked";
        }
    }

    private void ShowClaimResult(
        MuseumMilestoneClaimResult result)
    {
        if (resultText == null)
            return;

        resultText.gameObject.SetActive(true);

        if (result == null)
        {
            resultText.text = "Milestone claim failed.";
            return;
        }

        if (!result.success)
        {
            resultText.text = result.message;
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine(result.message);

        if (result.grantedRewardLines != null)
        {
            for (int i = 0;
                 i < result.grantedRewardLines.Count;
                 i++)
            {
                string line = result.grantedRewardLines[i];

                if (!string.IsNullOrWhiteSpace(line))
                    builder.AppendLine(line);
            }
        }

        resultText.text = builder.ToString().TrimEnd();
    }

    private void HideResult()
    {
        if (resultText != null)
        {
            resultText.text = "";
            resultText.gameObject.SetActive(false);
        }
    }

    private void ScrollToSelected()
    {
        if (scrollRect == null ||
            selectedState == null ||
            spawnedSteps.Count <= 1)
        {
            return;
        }

        int selectedIndex = -1;

        for (int i = 0; i < spawnedSteps.Count; i++)
        {
            if (spawnedSteps[i] != null &&
                spawnedSteps[i].State == selectedState)
            {
                selectedIndex = i;
                break;
            }
        }

        if (selectedIndex < 0)
            return;

        float normalized =
            1f - selectedIndex /
            (float)Math.Max(1, spawnedSteps.Count - 1);

        scrollRect.verticalNormalizedPosition =
            Mathf.Clamp01(normalized);
    }

    private static MuseumMilestoneState FindDefaultSelection(
        IReadOnlyList<MuseumMilestoneState> states)
    {
        if (states == null)
            return null;

        for (int i = 0; i < states.Count; i++)
        {
            if (states[i] != null && states[i].IsClaimable)
                return states[i];
        }

        for (int i = 0; i < states.Count; i++)
        {
            if (states[i] != null && !states[i].IsClaimed)
                return states[i];
        }

        return states.Count > 0 ? states[states.Count - 1] : null;
    }

    private static MuseumMilestoneState FindState(
        IReadOnlyList<MuseumMilestoneState> states,
        string milestoneId)
    {
        if (states == null ||
            string.IsNullOrWhiteSpace(milestoneId))
        {
            return null;
        }

        for (int i = 0; i < states.Count; i++)
        {
            MuseumMilestoneState state = states[i];

            if (state != null &&
                string.Equals(
                    state.MilestoneId,
                    milestoneId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return state;
            }
        }

        return null;
    }

    private void ClearSteps()
    {
        for (int i = 0; i < spawnedSteps.Count; i++)
        {
            MuseumMilestoneStepUI step = spawnedSteps[i];

            if (step != null)
                Destroy(step.gameObject);
        }

        spawnedSteps.Clear();
    }

    private void HandleMilestonesChanged()
    {
        Refresh(false);
    }

    private void Subscribe()
    {
        if (service == null || subscribed)
            return;

        service.OnMilestonesChanged +=
            HandleMilestonesChanged;

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (service != null && subscribed)
        {
            service.OnMilestonesChanged -=
                HandleMilestonesChanged;
        }

        subscribed = false;
    }

    private void ResolveReferences()
    {
        if (root == null)
            root = gameObject;

        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);

        if (content == null &&
            scrollRect != null &&
            scrollRect.content != null)
        {
            content = scrollRect.content;
        }

        if (rewardPreview == null)
        {
            rewardPreview =
                GetComponentInChildren<MuseumRewardPreviewUI>(true);
        }

        if (claimButtonText == null && claimButton != null)
        {
            claimButtonText =
                claimButton.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private static void SetupButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void RemoveButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }
}
