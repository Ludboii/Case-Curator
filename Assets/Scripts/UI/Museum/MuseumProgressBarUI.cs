using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small reusable progress presenter used by Museum wing, category, weapon and
/// skin cards. It supports an optional Image fill and TMP label.
/// </summary>
public class MuseumProgressBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text progressText;

    [Tooltip(
        "When enabled, append the percentage after the completed / total count.")]
    [SerializeField] private bool showPercentage;

    private bool missingTextWarningLogged;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    public void SetProgress(int completed, int total)
    {
        ResolveReferences();

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