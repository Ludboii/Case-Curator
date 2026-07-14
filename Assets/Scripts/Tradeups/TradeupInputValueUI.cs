using TMPro;
using UnityEngine;

public class TradeupInputValueUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TradeupFlowUI tradeupFlow;
    [SerializeField] private TMP_Text inputValueText;

    [Header("Display")]
    [SerializeField] private string label = "Input Value: ";
    [SerializeField] private string numberFormat = "0.##";
    [SerializeField] private bool hideWhenNothingSelected;

    private int lastSelectedCount = -1;
    private float lastDisplayedValue = -1f;

    private void OnEnable()
    {
        Refresh(true);
    }

    private void LateUpdate()
    {
        Refresh(false);
    }

    public void RefreshNow()
    {
        Refresh(true);
    }

    private void Refresh(bool force)
    {
        if (inputValueText == null)
            return;

        int selectedCount = 0;
        float totalValue = 0f;

        if (tradeupFlow != null && tradeupFlow.SelectedInputs != null)
        {
            selectedCount = tradeupFlow.SelectedInputs.Count;

            for (int i = 0; i < selectedCount; i++)
            {
                InventoryItem item = tradeupFlow.SelectedInputs[i];

                if (item == null || item.skin == null)
                    continue;

                if (item.marketValue <= 0f)
                    item.marketValue = PriceCalculator.GetPrice(item);

                totalValue += Mathf.Max(0f, item.marketValue);
            }
        }

        if (!force &&
            selectedCount == lastSelectedCount &&
            Mathf.Approximately(totalValue, lastDisplayedValue))
        {
            return;
        }

        lastSelectedCount = selectedCount;
        lastDisplayedValue = totalValue;

        inputValueText.gameObject.SetActive(
            !hideWhenNothingSelected || selectedCount > 0);

        inputValueText.text =
            label + totalValue.ToString(numberFormat);
    }
}
