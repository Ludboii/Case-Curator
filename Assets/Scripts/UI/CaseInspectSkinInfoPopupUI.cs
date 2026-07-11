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
    if (skin == null)
        return "";

    if (skin.isVanilla)
        return BuildVanillaPriceText(skin);

    string leftTitle = "Normal";
    WearPrices leftPrices = skin.exteriorPrices;

    if (skin.canBeStatTrak)
    {
        return BuildTwoColumnPriceText(
            leftTitle,
            leftPrices,
            "StatTrak",
            skin.statTrakExteriorPrices);
    }

    if (skin.canBeSouvenir)
    {
        return BuildTwoColumnPriceText(
            leftTitle,
            leftPrices,
            "Souvenir",
            skin.souvenirExteriorPrices);
    }

    return BuildSingleColumnPriceText(leftTitle, leftPrices);
}

private string BuildVanillaPriceText(SkinData skin)
{
    string normalPrice = FormatPrice(skin.vanillaPrice);
    string statTrakPrice = FormatPrice(skin.vanillaStatTrakPrice);

    if (skin.canBeStatTrak && skin.vanillaStatTrakPrice > 0f)
    {
        return
            $"Vanilla        Vanilla StatTrak\n" +
            $"{normalPrice,-14}{statTrakPrice}";
    }

    return
        $"Vanilla\n" +
        $"{normalPrice}";
}

private string BuildTwoColumnPriceText(
    string leftTitle,
    WearPrices leftPrices,
    string rightTitle,
    WearPrices rightPrices)
{
    return
        $"{leftTitle,-18}{rightTitle}\n" +
        $"FN  {FormatPrice(leftPrices.factoryNew),-14}FN  {FormatPrice(rightPrices.factoryNew)}\n" +
        $"MW  {FormatPrice(leftPrices.minimalWear),-14}MW  {FormatPrice(rightPrices.minimalWear)}\n" +
        $"FT  {FormatPrice(leftPrices.fieldTested),-14}FT  {FormatPrice(rightPrices.fieldTested)}\n" +
        $"WW  {FormatPrice(leftPrices.wellWorn),-14}WW  {FormatPrice(rightPrices.wellWorn)}\n" +
        $"BS  {FormatPrice(leftPrices.battleScarred),-14}BS  {FormatPrice(rightPrices.battleScarred)}";
}

private string BuildSingleColumnPriceText(string title, WearPrices prices)
{
    return
        $"{title}\n" +
        $"FN  {FormatPrice(prices.factoryNew)}\n" +
        $"MW  {FormatPrice(prices.minimalWear)}\n" +
        $"FT  {FormatPrice(prices.fieldTested)}\n" +
        $"WW  {FormatPrice(prices.wellWorn)}\n" +
        $"BS  {FormatPrice(prices.battleScarred)}";
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