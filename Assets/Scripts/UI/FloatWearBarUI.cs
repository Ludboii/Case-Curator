using UnityEngine;
using UnityEngine.UI;

public class FloatWearBarUI : MonoBehaviour
{
    [Header("Bar")]
    public Image barFill;
    public RectTransform marker;

    public void Setup(float floatValue)
    {
        float normalized = Mathf.Clamp01(floatValue);

        if (barFill != null)
        {
            barFill.fillAmount = normalized;
        }

        if (marker != null)
        {
            marker.anchorMin = new Vector2(normalized, 0.5f);
            marker.anchorMax = new Vector2(normalized, 0.5f);
            marker.anchoredPosition = Vector2.zero;
        }
    }
}