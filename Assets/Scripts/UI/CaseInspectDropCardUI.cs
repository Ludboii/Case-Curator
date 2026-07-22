using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CaseInspectDropCardUI : MonoBehaviour
{
    [Header("Images")]
    public Image backgroundImage;
    public Image rarityBar;
    public Image skinImage;

    [Header("Text")]
    public TMP_Text weaponNameText;
    public TMP_Text skinNameText;
    public TMP_Text rarityText;
    public TMP_Text foundStateText;

    [Header("Completion Text Colors")]
    public Color foundColor = new Color(1f, 0.82f, 0.10f, 1f);
    public Color notFoundColor = new Color(0.62f, 0.62f, 0.62f, 1f);
    public Color requirementCompleteColor =
        new Color(0.30f, 0.92f, 0.20f, 1f);

    public Color requirementIncompleteColor =
        new Color(1f, 0.25f, 0.22f, 1f);

    [Header("Button")]
    public Button button;

    private SkinData skin;
    private CaseInspectUI owner;
    private bool isRareSpecialEntry;

    public void Setup(SkinData skinData, CaseInspectUI inspectOwner)
    {
        skin = skinData;
        owner = inspectOwner;
        isRareSpecialEntry = false;

        if (skin == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        Color rarityColor = RarityColorUtility.GetColor(skin.rarity);
        Color bgColor = Color.Lerp(Color.black, rarityColor, 0.35f);
        bgColor.a = 0.9f;

        if (backgroundImage != null)
        {
            backgroundImage.color = bgColor;
            backgroundImage.raycastTarget = false;
        }

        if (rarityBar != null)
        {
            rarityBar.color = rarityColor;
            rarityBar.raycastTarget = false;
        }

        if (skinImage != null)
        {
            skinImage.sprite = skin.icon;
            skinImage.enabled = skin.icon != null;
            skinImage.preserveAspect = true;
            skinImage.raycastTarget = false;
        }

        if (weaponNameText != null)
        {
            weaponNameText.text = skin.weaponName;
            weaponNameText.raycastTarget = false;
        }

        if (skinNameText != null)
        {
            skinNameText.text = skin.isVanilla ? "Vanilla" : skin.skinName;
            skinNameText.raycastTarget = false;
        }

        if (rarityText != null)
        {
            rarityText.text = GetRarityDisplayName(skin.rarity);
            rarityText.color = rarityColor;
            rarityText.raycastTarget = false;
        }

        UpdateFoundStateText();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClicked);
        }
    }

    public void SetupRareSpecialEntry(
        CaseInspectUI inspectOwner,
        int possibleCount,
        Sprite placeholderSprite)
    {
        skin = null;
        owner = inspectOwner;
        isRareSpecialEntry = true;
        gameObject.SetActive(true);

        Color rarityColor = RarityColorUtility.GetColor(Rarity.RareSpecial);
        Color bgColor = Color.Lerp(Color.black, rarityColor, 0.35f);
        bgColor.a = 0.95f;

        if (backgroundImage != null)
        {
            backgroundImage.color = bgColor;
            backgroundImage.raycastTarget = false;
        }

        if (rarityBar != null)
        {
            rarityBar.color = rarityColor;
            rarityBar.raycastTarget = false;
        }

        if (skinImage != null)
        {
            skinImage.sprite = placeholderSprite;
            skinImage.enabled = placeholderSprite != null;
            skinImage.preserveAspect = true;
            skinImage.raycastTarget = false;
        }

        if (weaponNameText != null)
        {
            weaponNameText.text = "Rare Special";
            weaponNameText.raycastTarget = false;
        }

        if (skinNameText != null)
        {
            skinNameText.text = $"{possibleCount} possible";
            skinNameText.raycastTarget = false;
        }

        if (rarityText != null)
        {
            rarityText.text = "Open List";
            rarityText.color = rarityColor;
            rarityText.raycastTarget = false;
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClicked);
        }

        if (foundStateText != null)
        {
            foundStateText.raycastTarget = false;
            foundStateText.richText = true;
            foundStateText.color = Color.white;

            bool foundRare =
                ContainerProgressManager.Instance != null &&
                owner != null &&
                ContainerProgressManager.Instance.HasFoundRareSpecial(
                    owner.CurrentCase);

            foundStateText.text = Colorize(
                foundRare ? "Found" : "Not Found",
                foundRare ? foundColor : notFoundColor);
        }
    }

    private void OnClicked()
    {
        if (owner == null)
            return;

        if (isRareSpecialEntry)
        {
            owner.OpenRareSpecialList();
            return;
        }

        if (skin != null)
            owner.OpenSkinInfo(skin);
    }

    private void UpdateFoundStateText()
    {
        if (foundStateText == null)
            return;

        foundStateText.raycastTarget = false;
        foundStateText.richText = true;
        foundStateText.color = Color.white;

        if (skin == null)
        {
            foundStateText.text = "";
            return;
        }

        ContainerProgressManager progress =
            ContainerProgressManager.Instance;

        CaseData sourceCase = owner != null ? owner.CurrentCase : null;

        if (progress == null || sourceCase == null)
        {
            foundStateText.text = Colorize("Not Found", notFoundColor);
            return;
        }

        bool found = progress.HasFoundSkin(sourceCase, skin);
        StringBuilder builder = new StringBuilder();

        builder.Append(
            Colorize(
                found ? "Found" : "Not Found",
                found ? foundColor : notFoundColor));

        // Rare Special items only participate in Bronze Completion.
        if (skin.rarity == Rarity.RareSpecial)
        {
            foundStateText.text = builder.ToString();
            return;
        }

        bool diamondStage =
            progress.IsGoldComplete(sourceCase) &&
            progress.CanCompleteDiamond(sourceCase);

        // Bronze unlocks the wear-quality line. During Diamond progression the
        // normal line is replaced, rather than adding a fourth line.
        if (progress.IsBronzeComplete(sourceCase))
        {
            int bestWearIndex = diamondStage
                ? progress.GetBestFoundStatTrakWearIndex(sourceCase, skin)
                : progress.GetBestFoundWearIndex(sourceCase, skin);

            bool highestWearComplete = diamondStage
                ? progress.HasFoundBestWearStatTrak(sourceCase, skin)
                : progress.HasFoundBestWear(sourceCase, skin);

            string prefix = diamondStage
                ? "StatTrak Highest: "
                : "Highest: ";

            string wearName = bestWearIndex >= 0
                ? ContainerProgressManager.GetWearDisplayName(bestWearIndex)
                : "Unknown";

            builder.Append('\n');
            builder.Append(
                Colorize(
                    prefix + wearName,
                    highestWearComplete
                        ? requirementCompleteColor
                        : requirementIncompleteColor));
        }

        // Silver unlocks the float line. Gold uses any eligible variant;
        // Diamond resets the line to StatTrak-only progress.
        if (progress.IsSilverComplete(sourceCase))
        {
            float threshold =
                ContainerProgressManager.GetTopQuarterFloatThreshold(skin);

            bool floatComplete = diamondStage
                ? progress.HasFoundTopQuarterFloatStatTrak(sourceCase, skin)
                : progress.HasFoundTopQuarterFloat(sourceCase, skin);

            builder.Append('\n');
            builder.Append(
                Colorize(
                    $"Float ≤ {threshold:0.######}",
                    floatComplete
                        ? requirementCompleteColor
                        : requirementIncompleteColor));
        }

        foundStateText.text = builder.ToString();
    }

    private static string Colorize(string text, Color color)
    {
        return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";
    }

    private static string GetRarityDisplayName(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Consumer: return "Consumer";
            case Rarity.Industrial: return "Industrial";
            case Rarity.MilSpec: return "Mil-Spec";
            case Rarity.Restricted: return "Restricted";
            case Rarity.Classified: return "Classified";
            case Rarity.Covert: return "Covert";
            case Rarity.RareSpecial: return "Rare Special";
            default: return rarity.ToString();
        }
    }
}
