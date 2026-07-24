using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small reusable progress presenter used by Museum wing, category, weapon and
/// skin cards. It displays a solid horizontal fill from left to right and an
/// optional completed / total TMP label.
/// </summary>
public class MuseumProgressBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text progressText;

    [Header("Fill Appearance")]
    [Tooltip("Color used by the solid progress fill.")]
    [SerializeField] private Color fillColor = new Color(0.2f, 0.85f, 0.2f, 1f);

    [Tooltip(
        "Removes the assigned sprite from the fill Image so transparent borders " +
        "or circular artwork cannot create gaps in the progress bar.")]
    [SerializeField] private bool useSolidFill = true;

    [Tooltip(
        "When enabled, append the percentage after the completed / total count.")]
    [SerializeField] private bool showPercentage;

    private bool missingTextWarningLogged;

    private void Awake()
    {
        ResolveReferences();
        ConfigureFillImage();
    }

    private void Reset()
    {
        ResolveReferences();
        ConfigureFillImage();
    }

    private void OnValidate()
    {
        ResolveReferences();
        ConfigureFillImage();
    }

    public void SetProgress(int completed, int total)
    {
        ResolveReferences();
        ConfigureFillImage();

        completed = Mathf.Max(0, completed);
        total = Mathf.Max(0, total);

        if (total > 0)
            completed = Mathf.Min(completed, total);

        float progress01 = total > 0
            ? Mathf.Clamp01(completed / (float)total)
            : 0f;

        if (fillImage != null)
            fillImage.fillAmount = progress01;

        if (progressText != null)
        {
            progressText.text = showPercentage
                ? $"{completed} / {total} ({progress01 * 100f:0.#}%)"
                : $"{completed} / {total}";

            missingTextWarningLogged = false;
        }
        else if (Application.isPlaying && !missingTextWarningLogged)
        {
            Debug.LogWarning(
                $"{nameof(MuseumProgressBarUI)} on '{name}' could not find its " +
                "ProgressText TMP component. Name the child object 'ProgressText' " +
                "or assign it in the Inspector.",
                this);

            missingTextWarningLogged = true;
        }
    }

    private void ConfigureFillImage()
    {
        if (fillImage == null)
            return;

        if (useSolidFill)
            fillImage.sprite = null;

        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillClockwise = true;
        fillImage.fillCenter = true;
        fillImage.preserveAspect = false;
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;
    }

    private void ResolveReferences()
    {
        if (progressText == null)
            progressText = FindProgressText();

        if (fillImage == null)
            fillImage = FindFillImage();
    }

    private TMP_Text FindProgressText()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];

            if (text != null &&
                string.Equals(
                    text.gameObject.name,
                    "ProgressText",
                    StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }
        }

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];

            if (text != null &&
                text.gameObject.name.IndexOf(
                    "progress",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return text;
            }
        }

        return texts.Length == 1 ? texts[0] : null;
    }

    private Image FindFillImage()
    {
        Image[] images = GetComponentsInChildren<Image>(true);

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];

            if (image != null &&
                image.gameObject.name.IndexOf(
                    "fill",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return image;
            }
        }

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null && images[i].type == Image.Type.Filled)
                return images[i];
        }

        return null;
    }
}
