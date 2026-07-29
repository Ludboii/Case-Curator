using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Add this beside the tradeup selection/contract summary. Applied sticker
/// value is already included through PriceCalculator, while the stickers are
/// destroyed when the input skins leave inventory.
/// </summary>
public sealed class TradeupStickerWarningUI : MonoBehaviour
{
    [SerializeField] private TradeupFlowUI tradeupFlow;
    [SerializeField] private GameObject warningRoot;
    [SerializeField] private TMP_Text warningText;

    private StickerApplicationService service;

    private void Awake()
    {
        if (tradeupFlow == null)
            tradeupFlow = GetComponentInParent<TradeupFlowUI>(true);

        service = StickerApplicationService.GetOrCreate();
    }

    private void OnEnable()
    {
        if (tradeupFlow != null)
        {
            tradeupFlow.OnSelectionChanged -= Refresh;
            tradeupFlow.OnSelectionChanged += Refresh;
            Refresh(tradeupFlow.SelectedInputs);
        }
    }

    private void OnDisable()
    {
        if (tradeupFlow != null)
            tradeupFlow.OnSelectionChanged -= Refresh;
    }

    private void Refresh(IReadOnlyList<InventoryItem> inputs)
    {
        int stickerCount = 0;
        float stickerValue = 0f;

        if (inputs != null && service != null)
        {
            for (int i = 0; i < inputs.Count; i++)
            {
                InventoryItem item = inputs[i];
                IReadOnlyList<AppliedStickerSaveData> applied =
                    service.GetAppliedStickers(item);
                stickerCount += applied.Count;
                stickerValue += service.GetAppliedStickerValue(item);
            }
        }

        bool show = stickerCount > 0;

        if (warningRoot != null)
            warningRoot.SetActive(show);

        if (warningText != null)
        {
            warningText.text = show
                ? $"WARNING: {stickerCount:N0} applied sticker(s) will be " +
                  $"destroyed. Their {stickerValue:N2} Gold added value is " +
                  "included in the tradeup input value."
                : "";
        }
    }
}
