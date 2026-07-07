using TMPro;
using UnityEngine;

public class CurrencyWidgetUI : MonoBehaviour
{
    [Header("Text References")]
    public TMP_Text goldText;
    public TMP_Text diamondText;

    private SaveManager subscribedManager;

    private void OnEnable()
    {
        TrySubscribe();
        Refresh();
    }

    private void Start()
{
    TrySubscribe();
    Refresh();
    Invoke(nameof(Refresh), 0.1f);
}
    private void OnDisable()
    {
        if (subscribedManager != null)
        {
            subscribedManager.OnCurrencyChanged -= Refresh;
            subscribedManager = null;
        }
    }

    private void TrySubscribe()
    {
        if (SaveManager.Instance == null)
            return;

        if (subscribedManager == SaveManager.Instance)
            return;

        if (subscribedManager != null)
            subscribedManager.OnCurrencyChanged -= Refresh;

        subscribedManager = SaveManager.Instance;
        subscribedManager.OnCurrencyChanged += Refresh;
    }

    public void Refresh()
    {
        if (SaveManager.Instance == null)
        {
            if (goldText != null)
                goldText.text = "0";

            if (diamondText != null)
                diamondText.text = "0";

            return;
        }

        if (goldText != null)
            goldText.text = SaveManager.Instance.Gold.ToString("0.##");

        if (diamondText != null)
            diamondText.text = SaveManager.Instance.Diamonds.ToString();
    }
}