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

    [Header("Price Table Layout")]
    [Range(0f, 100f)] public float wearLabelPosition = 0f;
    [Range(0f, 100f)] public float normalColumnPosition = 24f;
    [Range(0f, 100f)] public float premiumColumnPosition = 68f;

    [Header("Discovered Price Colors")]
    public Color normalFoundPriceColor = new Color(0.25f, 1f, 0.35f, 1f);
    public Color premiumFoundPriceColor = new Color(1f, 0.25f, 0.82f, 1f);
    public Color undiscoveredPriceColor = Color.white;

    [Header("Buttons")]
    public Button closeButton;
    public Button okButton;

    private CaseData currentSourceCase;

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
        currentSourceCase = sourceCase;

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
        {
            priceText.richText = true;
            priceText.alignment = TextAlignmentOptions.TopLeft;
            priceText.text = BuildPriceText(skin, sourceCase);
        }

        if (wearRangeText != null)
        {
            wearRangeText.text =
                $"Wear range:\n{skin.minFloat:0.00} - {skin.maxFloat:0.00}";
        }

        if (sourceText != null)
        {
            sourceText.text = sourceCase != null
                ? sourceCase.caseName
                : "Unknown source";
        }
    }

    public void Close()
    {
        currentSourceCase = null;

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

    private string BuildPriceText(SkinData skin, CaseData sourceCase)
    {
        if (skin == null)
            return "";

        if (skin.isVanilla)
            return BuildVanillaPriceText(skin, sourceCase);

        WearPrices normalPrices = skin.exteriorPrices;
        bool hasPremium = TryGetPremiumColumn(
            skin,
            sourceCase,
            out string premiumTitle,
            out WearPrices premiumPrices,
            out ContainerItemVariant premiumVariant);

        return hasPremium
            ? BuildTwoColumnPriceText(
                skin,
                sourceCase,
                normalPrices,
                premiumTitle,
                premiumPrices,
                premiumVariant)
            : BuildSingleColumnPriceText(skin, sourceCase, normalPrices);
    }

    private string BuildVanillaPriceText(SkinData skin, CaseData sourceCase)
    {
        bool hasPremium =
            skin.canBeStatTrak && skin.vanillaStatTrakPrice > 0f;

        StringBuilder builder = new StringBuilder();

        if (hasPremium)
        {
            AppendAt(builder, normalColumnPosition, "<b>Normal</b>");
            AppendAt(builder, premiumColumnPosition, "<b>StatTrak</b>");
            builder.AppendLine();

            AppendAt(
                builder,
                normalColumnPosition,
                FormatDiscoveredPrice(
                    skin.vanillaPrice,
                    IsPriceDiscovered(
                        sourceCase,
                        skin,
                        0,
                        ContainerItemVariant.Normal),
                    normalFoundPriceColor));

            AppendAt(
                builder,
                premiumColumnPosition,
                FormatDiscoveredPrice(
                    skin.vanillaStatTrakPrice,
                    IsPriceDiscovered(
                        sourceCase,
                        skin,
                        0,
                        ContainerItemVariant.StatTrak),
                    premiumFoundPriceColor));
        }
        else
        {
            AppendAt(builder, normalColumnPosition, "<b>Vanilla</b>");
            builder.AppendLine();

            AppendAt(
                builder,
                normalColumnPosition,
                FormatDiscoveredPrice(
                    skin.vanillaPrice,
                    IsPriceDiscovered(
                        sourceCase,
                        skin,
                        0,
                        ContainerItemVariant.Normal),
                    normalFoundPriceColor));
        }

        return builder.ToString();
    }

    private string BuildTwoColumnPriceText(
        SkinData skin,
        CaseData sourceCase,
        WearPrices normalPrices,
        string premiumTitle,
        WearPrices premiumPrices,
        ContainerItemVariant premiumVariant)
    {
        StringBuilder builder = new StringBuilder();

        AppendAt(builder, normalColumnPosition, "<b>Normal</b>");
        AppendAt(builder, premiumColumnPosition, $"<b>{premiumTitle}</b>");
        builder.AppendLine();

        for (int wearIndex = 0; wearIndex < 5; wearIndex++)
        {
            AppendAt(builder, wearLabelPosition, GetWearAbbreviation(wearIndex));

            AppendAt(
                builder,
                normalColumnPosition,
                FormatDiscoveredPrice(
                    normalPrices.Get(wearIndex),
                    IsPriceDiscovered(
                        sourceCase,
                        skin,
                        wearIndex,
                        ContainerItemVariant.Normal),
                    normalFoundPriceColor));

            AppendAt(
                builder,
                premiumColumnPosition,
                FormatDiscoveredPrice(
                    premiumPrices.Get(wearIndex),
                    IsPriceDiscovered(
                        sourceCase,
                        skin,
                        wearIndex,
                        premiumVariant),
                    premiumFoundPriceColor));

            if (wearIndex < 4)
                builder.AppendLine();
        }

        return builder.ToString();
    }

    private string BuildSingleColumnPriceText(
        SkinData skin,
        CaseData sourceCase,
        WearPrices prices)
    {
        StringBuilder builder = new StringBuilder();
        AppendAt(builder, normalColumnPosition, "<b>Normal</b>");
        builder.AppendLine();

        for (int wearIndex = 0; wearIndex < 5; wearIndex++)
        {
            AppendAt(builder, wearLabelPosition, GetWearAbbreviation(wearIndex));

            AppendAt(
                builder,
                normalColumnPosition,
                FormatDiscoveredPrice(
                    prices.Get(wearIndex),
                    IsPriceDiscovered(
                        sourceCase,
                        skin,
                        wearIndex,
                        ContainerItemVariant.Normal),
                    normalFoundPriceColor));

            if (wearIndex < 4)
                builder.AppendLine();
        }

        return builder.ToString();
    }

    private bool TryGetPremiumColumn(
        SkinData skin,
        CaseData sourceCase,
        out string title,
        out WearPrices prices,
        out ContainerItemVariant variant)
    {
        bool souvenirSource = sourceCase != null &&
            (sourceCase.containerType == CaseContainerType.SouvenirPackage ||
             sourceCase.forceSouvenirDrops);

        if (souvenirSource && skin.canBeSouvenir)
        {
            title = "Souvenir";
            prices = skin.souvenirExteriorPrices;
            variant = ContainerItemVariant.Souvenir;
            return true;
        }

        bool statTrakSource = sourceCase != null &&
            sourceCase.allowStatTrak &&
            skin.canBeStatTrak;

        if (statTrakSource ||
            (sourceCase == null && skin.canBeStatTrak))
        {
            title = "StatTrak";
            prices = skin.statTrakExteriorPrices;
            variant = ContainerItemVariant.StatTrak;
            return true;
        }

        if (skin.canBeSouvenir)
        {
            title = "Souvenir";
            prices = skin.souvenirExteriorPrices;
            variant = ContainerItemVariant.Souvenir;
            return true;
        }

        title = "";
        prices = default;
        variant = ContainerItemVariant.Normal;
        return false;
    }

    private bool IsPriceDiscovered(
        CaseData sourceCase,
        SkinData skin,
        int wearIndex,
        ContainerItemVariant variant)
    {
        return sourceCase != null &&
               ContainerProgressManager.Instance != null &&
               ContainerProgressManager.Instance.HasDiscoveredPrice(
                   sourceCase,
                   skin,
                   wearIndex,
                   variant);
    }

    private string FormatDiscoveredPrice(
        float value,
        bool discovered,
        Color discoveredColor)
    {
        string price = FormatPrice(value);

        if (value <= 0f)
            return price;

        Color color = discovered
            ? discoveredColor
            : undiscoveredPriceColor;

        return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{price}</color>";
    }

    private static void AppendAt(
        StringBuilder builder,
        float positionPercent,
        string text)
    {
        builder.Append($"<pos={Mathf.Clamp(positionPercent, 0f, 100f):0.##}%>");
        builder.Append(text);
    }

    private static string GetWearAbbreviation(int wearIndex)
    {
        switch (wearIndex)
        {
            case 0: return "FN";
            case 1: return "MW";
            case 2: return "FT";
            case 3: return "WW";
            default: return "BS";
        }
    }

    private static string FormatPrice(float value)
    {
        if (value <= 0f)
            return "-";

        return $"{value:0.##} G";
    }
}
