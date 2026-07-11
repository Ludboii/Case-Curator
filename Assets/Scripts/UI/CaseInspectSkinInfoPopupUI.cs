using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CaseInspectSkinInfoPopupUI : MonoBehaviour
{
    [Header("Root")]
    public GameObject root;

    [Header("Colorable Backgrounds")]
    public Image mainBackgroundImage;
    public Image topBarImage;

    [Header("Skin Image")]
    public Image skinImage;

    [Header("Text")]
    public TMP_Text titleText;
    public TMP_Text priceText;
    public TMP_Text wearRangeText;
    public TMP_Text sourceText;

    [Header("Buttons")]
    public Button closeButton;
    public Button okButton;

    private void Awake()
    {
        if (root == null)
            root = gameObject;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        if (okButton != null)
        {
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(Close);
        }

        Close();
    }

    public void Open(SkinData skin, CaseData sourceCase)
    {
        if (skin == null)
        {
            Close();
            return;
        }

        if (root != null)
            root.SetActive(true);

        ApplyRarityColors(skin.rarity);

        if (skinImage != null)
        {
            skinImage.sprite = skin.icon;
            skinImage.enabled = skin.icon != null;
            skinImage.preserveAspect = true;
        }

        if (titleText != null)
        {
            string finishName = skin.isVanilla ? "Vanilla" : skin.skinName;
            titleText.text = $"{skin.weaponName} | {finishName}";
        }

        if (priceText != null)
            priceText.text = BuildPriceText(skin);

        if (wearRangeText != null)
            wearRangeText.text = $"Wear range:\n{skin.minFloat:0.00} - {skin.maxFloat:0.00}";

        if (sourceText != null)
            sourceText.text = sourceCase != null ? sourceCase.caseName : "Unknown source";
    }

    public void Close()
    {
        if (root != null)
            root.SetActive(false);
    }

    private void ApplyRarityColors(Rarity rarity)
    {
        Color rarityColor = RarityColorUtility.GetColor(rarity);

        Color backgroundColor = Color.Lerp(Color.black, rarityColor, 0.55f);
        backgroundColor.a = 0.96f;

        Color topBarColor = Color.Lerp(Color.black, rarityColor, 0.78f);
        topBarColor.a = 1f;

        if (mainBackgroundImage != null)
            mainBackgroundImage.color = backgroundColor;

        if (topBarImage != null)
            topBarImage.color = topBarColor;
    }

    private string BuildPriceText(SkinData skin)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine("Normal");
        AppendWearPrices(builder, skin.exteriorPrices);

        if (skin.canBeStatTrak)
        {
            builder.AppendLine();
            builder.AppendLine("StatTrak");
            AppendWearPrices(builder, skin.statTrakExteriorPrices);
        }

        if (skin.canBeSouvenir)
        {
            builder.AppendLine();
            builder.AppendLine("Souvenir");
            AppendWearPrices(builder, skin.souvenirExteriorPrices);
        }

        return builder.ToString();
    }

    private void AppendWearPrices(StringBuilder builder, WearPrices prices)
    {
        builder.AppendLine($"FN  {FormatPrice(prices.factoryNew)}");
        builder.AppendLine($"MW  {FormatPrice(prices.minimalWear)}");
        builder.AppendLine($"FT  {FormatPrice(prices.fieldTested)}");
        builder.AppendLine($"WW  {FormatPrice(prices.wellWorn)}");
        builder.AppendLine($"BS  {FormatPrice(prices.battleScarred)}");
    }

    private string FormatPrice(float value)
    {
        if (value <= 0f)
            return "-";

        return $"{value:0.##} G";
    }
}