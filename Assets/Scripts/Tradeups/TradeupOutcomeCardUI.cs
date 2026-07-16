using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TradeupOutcomeCardUI : MonoBehaviour
{
    [Header("Images")]
    [SerializeField] private Image skinImage;
    [SerializeField] private Image rarityBar;

    [Header("Text")]
    [SerializeField] private TMP_Text weaponNameText;
    [SerializeField] private TMP_Text skinNameText;
    [SerializeField] private TMP_Text chanceText;

    [Tooltip(
        "Optional testing text. Displays the exact output float that this " +
        "skin would receive from the current contract.")]
    [SerializeField] private TMP_Text expectedFloatText;

    [Header("Optional Variant Badge")]
    [SerializeField] private TMP_Text variantBadgeText;

    private void Awake()
    {
        // Outcome cards are presentation-only. Prevent their child graphics from
        // adding unnecessary GraphicRaycaster work inside the preview ScrollRect.
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
                graphics[i].raycastTarget = false;
        }
    }

    public void Setup(
        SkinData skin,
        float probability,
        bool statTrakContract,
        double averageInputFloat)
    {
        if (skin == null)
        {
            Clear();
            gameObject.SetActive(false);
            return;
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (skinImage != null)
        {
            skinImage.sprite = skin.icon;
            skinImage.enabled = skin.icon != null;
            skinImage.preserveAspect = true;
        }

        if (rarityBar != null)
            rarityBar.color = RarityColorUtility.GetColor(skin.rarity);

        if (weaponNameText != null)
        {
            weaponNameText.text =
                (skin.weaponName ?? "").ToUpperInvariant();
        }

        if (skinNameText != null)
        {
            skinNameText.text = skin.isVanilla
                ? "Vanilla"
                : skin.skinName ?? "";
        }

        if (chanceText != null)
            chanceText.text = FormatProbability(probability);

        if (expectedFloatText != null)
        {
            if (skin.isVanilla)
            {
                expectedFloatText.text = "";
                expectedFloatText.gameObject.SetActive(false);
            }
            else
            {
                double expectedFloat = CalculateExpectedFloat(
                    skin,
                    averageInputFloat);

                expectedFloatText.text =
                    $"FLOAT: {expectedFloat:0.0000000000}";

                expectedFloatText.gameObject.SetActive(true);
            }
        }

        if (variantBadgeText != null)
        {
            bool outputIsStatTrak =
                statTrakContract && skin.canBeStatTrak;

            variantBadgeText.text = outputIsStatTrak ? "ST" : "";
            variantBadgeText.gameObject.SetActive(outputIsStatTrak);
        }
    }

    public void Clear()
    {
        if (skinImage != null)
        {
            skinImage.sprite = null;
            skinImage.enabled = false;
        }

        if (weaponNameText != null)
            weaponNameText.text = "";

        if (skinNameText != null)
            skinNameText.text = "";

        if (chanceText != null)
            chanceText.text = "";

        if (expectedFloatText != null)
        {
            expectedFloatText.text = "";
            expectedFloatText.gameObject.SetActive(false);
        }

        if (variantBadgeText != null)
        {
            variantBadgeText.text = "";
            variantBadgeText.gameObject.SetActive(false);
        }
    }

    private static double CalculateExpectedFloat(
        SkinData skin,
        double averageInputFloat)
    {
        if (skin == null || skin.isVanilla)
            return -1d;

        double normalizedAverage = Clamp(
            averageInputFloat,
            0d,
            1d);

        double minimumFloat = skin.minFloat;
        double maximumFloat = skin.maxFloat;

        if (maximumFloat < minimumFloat)
        {
            double temporary = minimumFloat;
            minimumFloat = maximumFloat;
            maximumFloat = temporary;
        }

        double expectedFloat =
            minimumFloat +
            normalizedAverage *
            (maximumFloat - minimumFloat);

        return Clamp(
            expectedFloat,
            minimumFloat,
            maximumFloat);
    }

    private static double Clamp(
        double value,
        double minimum,
        double maximum)
    {
        if (value < minimum)
            return minimum;

        if (value > maximum)
            return maximum;

        return value;
    }

    private static string FormatProbability(float probability)
    {
        float percent = Mathf.Max(0f, probability) * 100f;

        if (percent >= 10f)
            return $"{percent:0.##}%";

        if (percent >= 1f)
            return $"{percent:0.###}%";

        if (percent >= 0.01f)
            return $"{percent:0.####}%";

        return $"{percent:0.######}%";
    }
}
