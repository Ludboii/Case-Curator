using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable Museum progress presenter. The host Image remains the grey
/// background; a separate green child Image is resized from left to right.
/// </summary>
public class MuseumProgressBarUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text progressText;

    [Header("Fill Appearance")]
    [SerializeField] private Color fillColor = new Color(0.15f, 0.85f, 0.15f, 1f);

    [Tooltip("Optional padding between the grey background and green fill.")]
    [Min(0f)]
    [SerializeField] private float fillPadding;

    [Tooltip(
        "When enabled, append the percentage after the completed / total count.")]
    [SerializeField] private bool showPercentage;

    private RectTransform fillRect;
    private bool missingTextWarningLogged;

    private void Awake()
    {
        ResolveReferences();
        EnsureSeparateFillImage();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();

        if (fillImage != null && fillImage.gameObject != gameObject)
            ConfigureFillImage();
    }

    public void SetProgress(int completed, int total)
    {
        ResolveReferences();
        EnsureSeparateFillImage();

        completed = Mathf.Max(0, completed);
        total = Mathf.Max(0, total);

        if (total > 0)
            completed = Mathf.Min(completed, total);

        float progress01 = total > 0
            ? Mathf.Clamp01(completed / (float)total)
            : 0f;

        UpdateFillWidth(progress01);

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

    private void EnsureSeparateFillImage()
    {
        // A previous version could assign the grey background Image itself as the
        // fill. Reject that reference so the background is never recolored.
        if (fillImage != null && fillImage.gameObject == gameObject)
            fillImage = null;

        if (fillImage == null)
            fillImage = FindDedicatedFillImage();

        if (fillImage == null && Application.isPlaying)
            fillImage = CreateFillImage();

        ConfigureFillImage();
    }

    private Image CreateFillImage()
    {
        GameObject fillObject = new GameObject(
            "ProgressFill",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));

        fillObject.transform.SetParent(transform, false);
        fillObject.transform.SetAsFirstSibling();

        Image image = fillObject.GetComponent<Image>();
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;

        return image;
    }

    private void ConfigureFillImage()
    {
        if (fillImage == null || fillImage.gameObject == gameObject)
            return;

        fillImage.sprite = null;
        fillImage.type = Image.Type.Simple;
        fillImage.preserveAspect = false;
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;

        fillRect = fillImage.rectTransform;
        fillRect.pivot = new Vector2(0f, 0.5f);
    }

    private void UpdateFillWidth(float progress01)
    {
        if (fillImage == null || fillRect == null)
            return;

        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(progress01, 1f);
        fillRect.offsetMin = new Vector2(fillPadding, fillPadding);
        fillRect.offsetMax = new Vector2(-fillPadding, -fillPadding);
        fillImage.enabled = progress01 > 0f;
    }

    private void ResolveReferences()
    {
        if (progressText == null)
            progressText = FindProgressText();

        if (fillImage == null || fillImage.gameObject == gameObject)
            fillImage = FindDedicatedFillImage();
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

    private Image FindDedicatedFillImage()
    {
        Image[] images = GetComponentsInChildren<Image>(true);

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];

            if (image == null || image.gameObject == gameObject)
                continue;

            string objectName = image.gameObject.name;

            if (string.Equals(
                    objectName,
                    "ProgressFill",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    objectName,
                    "Fill",
                    StringComparison.OrdinalIgnoreCase))
            {
                return image;
            }
        }

        return null;
    }
}
