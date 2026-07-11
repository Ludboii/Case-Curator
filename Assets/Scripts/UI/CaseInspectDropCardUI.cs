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

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClicked);
        }
    }

    public void SetupRareSpecialEntry(CaseInspectUI inspectOwner, int possibleCount, Sprite placeholderSprite)
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

    private string GetRarityDisplayName(Rarity rarity)
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