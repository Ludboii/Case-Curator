using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TradeupOutcomePreviewUI : MonoBehaviour
{
    [Header("Tradeup")]
    [SerializeField] private TradeupFlowUI tradeupFlow;

    [Header("Outcome List")]
    [SerializeField] private Transform content;
    [SerializeField] private TradeupOutcomeCardUI outcomeCardPrefab;
    [SerializeField] private ScrollRect scrollRect;

    [Tooltip(
        "Optional child containing only the ScrollRect/list visuals. " +
        "Do not assign the GameObject that holds this component.")]
    [SerializeField] private GameObject outcomesRoot;

    [Header("Text")]
    [SerializeField] private TMP_Text headerText;
    [SerializeField] private TMP_Text statusText;

    [Header("Behaviour")]
    [SerializeField] private bool showIncompleteSelectionMessage = true;
    [SerializeField] private bool resetScrollWhenPreviewChanges = true;

    private readonly List<TradeupOutcomeCardUI> cardPool =
        new List<TradeupOutcomeCardUI>();

    private bool subscribed;

    private sealed class AggregatedOutcome
    {
        public SkinData skin;
        public float probability;
    }

    private void OnEnable()
    {
        Subscribe();

        Refresh(
            tradeupFlow != null
                ? tradeupFlow.SelectedInputs
                : null);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (subscribed || tradeupFlow == null)
            return;

        tradeupFlow.OnSelectionChanged += Refresh;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || tradeupFlow == null)
        {
            subscribed = false;
            return;
        }

        tradeupFlow.OnSelectionChanged -= Refresh;
        subscribed = false;
    }

    public void Refresh(IReadOnlyList<InventoryItem> selectedInputs)
    {
        HideAllCards();

        if (tradeupFlow == null)
        {
            ShowStatus("Tradeup Flow is not assigned.");
            SetOutcomeRootVisible(false);
            return;
        }

        if (TradeupResolver.Instance == null)
        {
            ShowStatus("Tradeup Resolver is unavailable.");
            SetOutcomeRootVisible(false);
            return;
        }

        if (content == null || outcomeCardPrefab == null)
        {
            ShowStatus(
                "Possible-output Content or Outcome Card Prefab is not assigned.");
            SetOutcomeRootVisible(false);
            return;
        }

        int selectedCount = selectedInputs != null
            ? selectedInputs.Count
            : 0;

        int requiredCount = GetRequiredInputCount(selectedInputs);

        if (selectedCount != requiredCount)
        {
            SetHeader("POSSIBLE OUTPUTS");
            SetOutcomeRootVisible(false);

            if (showIncompleteSelectionMessage)
            {
                ShowStatus(
                    selectedCount <= 0
                        ? $"Select {requiredCount} tradeup items to reveal possible outputs."
                        : $"Select {requiredCount - selectedCount} more item(s) to reveal possible outputs.");
            }
            else
            {
                ShowStatus("");
            }

            return;
        }

        TradeupPreview preview =
            TradeupResolver.Instance.BuildPreview(selectedInputs);

        if (preview == null ||
            preview.validation == null ||
            !preview.validation.isValid)
        {
            string reason =
                preview != null && preview.validation != null
                    ? preview.validation.errorMessage
                    : "Possible outputs could not be calculated.";

            SetHeader("POSSIBLE OUTPUTS");
            SetOutcomeRootVisible(false);
            ShowStatus(reason);
            return;
        }

        List<AggregatedOutcome> outcomes =
            AggregateOutcomes(preview.possibleOutcomes);

        if (outcomes.Count == 0)
        {
            SetHeader("POSSIBLE OUTPUTS");
            SetOutcomeRootVisible(false);
            ShowStatus("No valid output skins were found.");
            return;
        }

        EnsurePoolSize(outcomes.Count);

        for (int i = 0; i < outcomes.Count; i++)
        {
            AggregatedOutcome outcome = outcomes[i];
            TradeupOutcomeCardUI card = cardPool[i];

            card.gameObject.SetActive(true);
            card.Setup(
                outcome.skin,
                outcome.probability,
                preview.isStatTrak,
                preview.averageInputFloat);
        }

        SetHeader($"POSSIBLE OUTPUTS ({outcomes.Count})");
        ShowStatus("");
        SetOutcomeRootVisible(true);

        if (resetScrollWhenPreviewChanges && scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    private static List<AggregatedOutcome> AggregateOutcomes(
        IReadOnlyList<TradeupOutcomeChance> possibleOutcomes)
    {
        Dictionary<string, AggregatedOutcome> bySkinId =
            new Dictionary<string, AggregatedOutcome>(
                StringComparer.OrdinalIgnoreCase);

        if (possibleOutcomes != null)
        {
            for (int i = 0; i < possibleOutcomes.Count; i++)
            {
                TradeupOutcomeChance outcome = possibleOutcomes[i];

                if (outcome == null || outcome.skin == null)
                    continue;

                string key = GetSkinKey(outcome.skin);

                if (!bySkinId.TryGetValue(
                        key,
                        out AggregatedOutcome aggregated))
                {
                    aggregated = new AggregatedOutcome
                    {
                        skin = outcome.skin,
                        probability = 0f
                    };

                    bySkinId.Add(key, aggregated);
                }

                aggregated.probability +=
                    Mathf.Max(0f, outcome.probability);
            }
        }

        List<AggregatedOutcome> result =
            new List<AggregatedOutcome>(bySkinId.Values);

        result.Sort((first, second) =>
        {
            int chanceResult =
                second.probability.CompareTo(first.probability);

            if (chanceResult != 0)
                return chanceResult;

            string firstName = first.skin != null
                ? SkinDisplayUtility.GetDisplayName(first.skin)
                : "";

            string secondName = second.skin != null
                ? SkinDisplayUtility.GetDisplayName(second.skin)
                : "";

            return string.Compare(
                firstName,
                secondName,
                StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    private static string GetSkinKey(SkinData skin)
    {
        if (skin == null)
            return "missing-skin";

        if (!string.IsNullOrWhiteSpace(skin.apiId))
            return skin.apiId.Trim();

        return $"instance-{skin.GetInstanceID()}";
    }

    private int GetRequiredInputCount(
        IReadOnlyList<InventoryItem> selectedInputs)
    {
        if (selectedInputs != null &&
            selectedInputs.Count > 0 &&
            selectedInputs[0] != null &&
            selectedInputs[0].skin != null &&
            selectedInputs[0].skin.rarity == Rarity.Covert)
        {
            return 5;
        }

        return 10;
    }

    private void EnsurePoolSize(int requiredCount)
    {
        while (cardPool.Count < requiredCount)
        {
            TradeupOutcomeCardUI card =
                Instantiate(outcomeCardPrefab, content);

            card.gameObject.SetActive(false);
            cardPool.Add(card);
        }
    }

    private void HideAllCards()
    {
        for (int i = 0; i < cardPool.Count; i++)
        {
            TradeupOutcomeCardUI card = cardPool[i];

            if (card == null)
                continue;

            card.Clear();
            card.gameObject.SetActive(false);
        }
    }

    private void SetOutcomeRootVisible(bool visible)
    {
        if (outcomesRoot == null || outcomesRoot == gameObject)
            return;

        outcomesRoot.SetActive(visible);
    }

    private void SetHeader(string message)
    {
        if (headerText != null)
            headerText.text = message ?? "";
    }

    private void ShowStatus(string message)
    {
        if (statusText == null)
            return;

        bool visible = !string.IsNullOrWhiteSpace(message);
        statusText.text = visible ? message : "";
        statusText.gameObject.SetActive(visible);
    }
}
