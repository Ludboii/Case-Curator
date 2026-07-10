using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryItemCardUI : MonoBehaviour
{
    [Header("Images")]
    public Image skinImage;
    public Image rarityBar;
    public Image goldIcon;

    [Header("Text")]
    public TMP_Text floatText;
    public TMP_Text badgeText;
    public TMP_Text weaponNameText;
    public TMP_Text skinNameText;
    public TMP_Text priceText;

    [Header("Pattern Badge")]
    public GameObject patternBadgeRoot;
    public TMP_Text patternBadgeText;
    public Image patternBadgeIcon;
    public Sprite tier1BlueGemIcon;
    public Sprite tier2BlueGemIcon;
    public Sprite tier3BlueGemIcon;
    public Sprite rubyIcon;
    public Sprite sapphireIcon;
    public Sprite emeraldIcon;
    public Sprite blackPearlIcon;
    public Sprite fadeIcon;
    public Sprite defaultPatternIcon;

    [Header("Favorite")]
    public GameObject favoriteIcon;
    public TMP_Text favoriteText;

    [Header("Click")]
    public Button button;
    public SkinInspectUI inspectUI;

    [Header("Selection Mode")]
    public GameObject selectedOverlay;
    public Image selectedBorder;

    private InventoryItem currentItem;
    private bool isSelected;

    public InventoryItem CurrentItem => currentItem;
    public bool IsSelected => isSelected;

    private void Awake()
    {
        CacheInspectUI();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnCardClicked);
        }

        SetSelected(false);
    }

    private void Start()
    {
        CacheInspectUI();
    }

    private void OnEnable()
    {
        CacheInspectUI();
    }

    public void Setup(InventoryItem item)
    {
        currentItem = item;
        SetSelected(false);

        if (item == null || item.skin == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        SkinData skin = item.skin;

        if (skinImage != null)
        {
            skinImage.sprite = skin.icon;
            skinImage.enabled = skin.icon != null;
            skinImage.preserveAspect = true;
        }

        if (rarityBar != null)
        {
            rarityBar.color = RarityColorUtility.GetColor(skin.rarity);
        }

        if (floatText != null)
        {
            floatText.text = SkinDisplayUtility.GetCardFloatDisplay(item);
        }

        if (badgeText != null)
        {
            string badge = SkinDisplayUtility.GetSpecialBadgeText(item);

            badgeText.text = badge;
            badgeText.gameObject.SetActive(!string.IsNullOrEmpty(badge));

            if (badge == "ST")
            {
                badgeText.color = new Color(1f, 0.55f, 0f);
            }
            else if (badge == "SV")
            {
                badgeText.color = new Color(1f, 0.85f, 0.25f);
            }
        }

        UpdatePatternBadge(item);

        if (weaponNameText != null)
        {
            weaponNameText.text = skin.weaponName.ToUpperInvariant();
        }

        if (skinNameText != null)
        {
            skinNameText.text = skin.isVanilla ? "Vanilla" : skin.skinName;
        }

        if (priceText != null)
        {
            float value = item.marketValue;

            if (value <= 0f)
            {
                value = PriceCalculator.GetPrice(item);
                item.marketValue = value;
            }

            priceText.text = value.ToString("F2");
        }

        UpdateFavoriteVisual();
    }

    private void UpdatePatternBadge(InventoryItem item)
    {
        string badge = GetPatternBadgeText(item);
        bool showBadge = !string.IsNullOrEmpty(badge);
        Color badgeColor = GetPatternBadgeColor(item, badge);
        Sprite badgeSprite = GetPatternBadgeSprite(item, badge);

        if (patternBadgeRoot != null)
            patternBadgeRoot.SetActive(showBadge);

        if (patternBadgeText != null)
        {
            patternBadgeText.text = badge;
            patternBadgeText.color = badgeColor;
            patternBadgeText.gameObject.SetActive(showBadge);
            patternBadgeText.raycastTarget = false;
        }

        if (patternBadgeIcon != null)
        {
            patternBadgeIcon.sprite = badgeSprite;
            patternBadgeIcon.color = badgeColor;
            patternBadgeIcon.enabled = showBadge && badgeSprite != null;
            patternBadgeIcon.gameObject.SetActive(showBadge);
            patternBadgeIcon.raycastTarget = false;
        }
    }

    private string GetPatternBadgeText(InventoryItem item)
    {
        if (item == null || item.skin == null || item.isVanilla)
            return "";

        SkinData skin = item.skin;
        string skinName = skin.skinName != null ? skin.skinName.ToLowerInvariant() : "";

        if (skinName.Contains("black pearl"))
            return "BLACK PEARL";

        if (skinName.Contains("sapphire"))
            return "SAPPHIRE";

        if (skinName.Contains("emerald"))
            return "EMERALD";

        if (skinName.Contains("ruby"))
            return "RUBY";

        if (skin.patternType == PatternType.CaseHardened && item.patternTier != PatternTier.None)
            return $"T{GetPatternTierNumber(item.patternTier)} BLUE GEM";

        if (skin.patternType == PatternType.Fade && item.patternTier != PatternTier.None)
            return $"T{GetPatternTierNumber(item.patternTier)} FADE";

        if (item.patternTier != PatternTier.None)
            return $"T{GetPatternTierNumber(item.patternTier)}";

        return "";
    }

    private int GetPatternTierNumber(PatternTier tier)
    {
        switch (tier)
        {
            case PatternTier.Tier1:
                return 1;
            case PatternTier.Tier2:
                return 2;
            case PatternTier.Tier3:
                return 3;
            default:
                return 0;
        }
    }

    private Color GetPatternBadgeColor(InventoryItem item, string badge)
    {
        if (badge == "RUBY")
            return new Color(1f, 0.1f, 0.18f);

        if (badge == "EMERALD")
            return new Color(0.1f, 1f, 0.45f);

        if (badge == "SAPPHIRE" || badge == "BLACK PEARL")
            return new Color(0.25f, 0.65f, 1f);

        if (badge.Contains("BLUE GEM"))
            return new Color(0.25f, 0.75f, 1f);

        if (badge.Contains("FADE"))
            return new Color(1f, 0.75f, 0.25f);

        return new Color(1f, 0.85f, 0.25f);
    }

    private Sprite GetPatternBadgeSprite(InventoryItem item, string badge)
    {
        if (badge == "RUBY")
            return rubyIcon != null ? rubyIcon : defaultPatternIcon;

        if (badge == "EMERALD")
            return emeraldIcon != null ? emeraldIcon : defaultPatternIcon;

        if (badge == "SAPPHIRE")
            return sapphireIcon != null ? sapphireIcon : defaultPatternIcon;

        if (badge == "BLACK PEARL")
            return blackPearlIcon != null ? blackPearlIcon : defaultPatternIcon;

        if (badge.Contains("BLUE GEM"))
        {
            if (item != null)
            {
                switch (item.patternTier)
                {
                    case PatternTier.Tier1:
                        return tier1BlueGemIcon != null ? tier1BlueGemIcon : defaultPatternIcon;
                    case PatternTier.Tier2:
                        return tier2BlueGemIcon != null ? tier2BlueGemIcon : defaultPatternIcon;
                    case PatternTier.Tier3:
                        return tier3BlueGemIcon != null ? tier3BlueGemIcon : defaultPatternIcon;
                }
            }

            return defaultPatternIcon;
        }

        if (badge.Contains("FADE"))
            return fadeIcon != null ? fadeIcon : defaultPatternIcon;

        return defaultPatternIcon;
    }

    private void OnCardClicked()
    {
        if (currentItem == null)
        {
            Debug.LogWarning("InventoryItemCardUI: No current item assigned.");
            return;
        }

        InventoryUI inventoryUI = InventoryUI.Instance;

        if (inventoryUI != null && inventoryUI.SelectionModeActive)
        {
            inventoryUI.ToggleSelectedItem(this);
            return;
        }

        OpenInspect();
    }

    private void OpenInspect()
    {
        if (currentItem == null)
        {
            Debug.LogWarning("InventoryItemCardUI: No current item assigned.");
            return;
        }

        CacheInspectUI();

        if (inspectUI == null)
        {
            Debug.LogWarning("InventoryItemCardUI: No SkinInspectUI found in scene.");
            return;
        }

        inspectUI.OpenOwnedItem(currentItem);
    }

    private void CacheInspectUI()
    {
        if (inspectUI != null)
            return;

        inspectUI = FindFirstObjectByType<SkinInspectUI>(FindObjectsInactive.Include);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (selectedOverlay != null)
            selectedOverlay.SetActive(selected);

        if (selectedBorder != null)
            selectedBorder.gameObject.SetActive(selected);
    }

    public void ToggleSelected()
    {
        SetSelected(!isSelected);
    }

    public void UpdateFavoriteVisual()
    {
        bool isFavorite =
            currentItem != null &&
            currentItem.favorite;

        if (favoriteIcon != null)
            favoriteIcon.SetActive(isFavorite);

        if (favoriteText != null)
        {
            favoriteText.text = isFavorite ? "FAV" : "";
            favoriteText.gameObject.SetActive(isFavorite);
            favoriteText.raycastTarget = false;
        }

        if (favoriteIcon != null)
        {
            Graphic[] graphics =
                favoriteIcon.GetComponentsInChildren<Graphic>(true);

            foreach (Graphic graphic in graphics)
            {
                graphic.raycastTarget = false;
            }
        }
    }

    public InventoryItem GetItem()
    {
        return currentItem;
    }
}
