using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small reusable progress presenter used by Museum wing, category, weapon and
/// skin cards. It supports an optional Image fill and an optional TMP label.
/// </summary>
public class MuseumProgressBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private bool showPercentage;

    public void SetProgress(int completed, int total)
    {
        completed = Mathf.Max(0, completed);
        total = Mathf.Max(0, total);

        float progress01 = total > 0
            ? Mathf.Clamp01(completed / (float)total)
            : 0f;

        if (fillImage != null)
            fillImage.fillAmount = progress01;

        if (progressText != null)
        {
            progressText.text = showPercentage
                ? $"{progress01 * 100f:0.#}%"
                : $"{completed} / {total}";
        }
    }
}
