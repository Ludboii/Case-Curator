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
        bool statTrakContract)
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

        if (variantBadgeText != null)
        {
            variantBadgeText.text = "";
            variantBadgeText.gameObject.SetActive(false);
        }
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
