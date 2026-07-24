using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phase M4.5 screen for viewing fragments, assembling presents and opening
/// them. Reward generation remains inside MuseumPresentService.
/// </summary>
public class MuseumPresentDeskUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private RectTransform content;
    [SerializeField] private MuseumPresentTierCardUI tierCardPrefab;
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button closeButton;
    [SerializeField, Min(0f)] private float cardSpacing = 12f;

    private readonly List<MuseumPresentTierCardUI> cards =
        new List<MuseumPresentTierCardUI>();

    private MuseumPresentService service;
    private bool subscribed;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        ResolveReferences();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }

    public void Open()
    {
        ResolveReferences();
        service = MuseumPresentService.GetOrCreate();
        Subscribe();

        if (root == null)
            root = gameObject;

        root.SetActive(true);
        HideResult();
        Refresh();
    }

    public void Close()
    {
        Unsubscribe();

        if (root != null)
            root.SetActive(false);
    }

    public void Refresh()
    {
        if (service == null)
            service = MuseumPresentService.GetOrCreate();

        service.ProcessClaimedMilestoneRewards();
        BuildCards();

        if (headerText != null)
        {
            int totalPresents = 0;

            for (int i = 0; i < MuseumPresentUtility.AllTiers.Length; i++)
            {
                totalPresents +=
                    service.GetPresents(MuseumPresentUtility.AllTiers[i]);
            }

            headerText.text =
                $"Museum Present Desk — {totalPresents:N0} presents ready";
        }
    }

    public void Assemble(MuseumPresentTier tier)
    {
        if (service == null)
            service = MuseumPresentService.GetOrCreate();

        bool success = service.AssembleOne(tier, out string message);
        ShowResult(message);

        if (success)
            RefreshCardsOnly();
    }

    public void OpenPresent(MuseumPresentTier tier)
    {
        if (service == null)
            service = MuseumPresentService.GetOrCreate();

        MuseumPresentOpenResult result = service.OpenPresent(tier);
        ShowResult(result != null ? result.message : "Present opening failed.");
        RefreshCardsOnly();
    }

    private void BuildCards()
    {
        ClearCards();
        ConfigureContent();

        if (content == null || tierCardPrefab == null)
            return;

        for (int i = 0; i < MuseumPresentUtility.AllTiers.Length; i++)
        {
            MuseumPresentTierCardUI card =
                Instantiate(tierCardPrefab, content);

            card.gameObject.SetActive(true);
            card.Setup(MuseumPresentUtility.AllTiers[i], this);
            cards.Add(card);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private void RefreshCardsOnly()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null)
                cards[i].Refresh();
        }

        if (headerText != null && service != null)
        {
            int totalPresents = 0;

            for (int i = 0; i < MuseumPresentUtility.AllTiers.Length; i++)
            {
                totalPresents +=
                    service.GetPresents(MuseumPresentUtility.AllTiers[i]);
            }

            headerText.text =
                $"Museum Present Desk — {totalPresents:N0} presents ready";
        }
    }

    private void ConfigureContent()
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

        layout.spacing = cardSpacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter =
            content.GetComponent<ContentSizeFitter>();

        if (fitter == null)
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void ClearCards()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] != null)
                Destroy(cards[i].gameObject);
        }

        cards.Clear();
    }

    private void HandlePresentStateChanged()
    {
        if (IsOpen)
            RefreshCardsOnly();
    }

    private void HandleMilestoneRewardGranted(
        MuseumPresentGrantSummary summary)
    {
        if (!IsOpen || summary == null || !summary.HasRewards)
            return;

        ShowResult(string.Join("\n", summary.rewardLines));
        RefreshCardsOnly();
    }

    private void Subscribe()
    {
        if (service == null || subscribed)
            return;

        service.OnPresentStateChanged += HandlePresentStateChanged;
        service.OnMilestonePresentRewardGranted +=
            HandleMilestoneRewardGranted;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (service != null && subscribed)
        {
            service.OnPresentStateChanged -= HandlePresentStateChanged;
            service.OnMilestonePresentRewardGranted -=
                HandleMilestoneRewardGranted;
        }

        subscribed = false;
    }

    private void ShowResult(string message)
    {
        if (resultText == null)
            return;

        resultText.text = message ?? "";
        resultText.gameObject.SetActive(true);
    }

    private void HideResult()
    {
        if (resultText == null)
            return;

        resultText.text = "";
        resultText.gameObject.SetActive(false);
    }

    private void ResolveReferences()
    {
        if (root == null)
            root = gameObject;

        if (content == null)
        {
            ScrollRect scrollRect = GetComponentInChildren<ScrollRect>(true);

            if (scrollRect != null)
                content = scrollRect.content;
        }
    }
}
