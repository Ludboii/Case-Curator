using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkinInspectUI : MonoBehaviour
{
    public static SkinInspectUI Instance { get; private set; }

    [Header("Root")]
    public GameObject overlayRoot;

    [Header("Background")]
    public Image rarityBackground;

    [Header("Images")]
    public Image skinImage;
    public Image gemTierIcon;

    [Header("Float Bar")]
    public FloatWearBarUI floatWearBar;

    [Header("Text")]
    public TMP_Text weaponNameText;
    public TMP_Text skinNameText;
    public TMP_Text rarityText;
    public TMP_Text collectionText;
    public TMP_Text wearText;
    public TMP_Text floatText;
    public TMP_Text patternText;
    public TMP_Text patternTierText;

    [Header("Sticker Slots")]
    [Tooltip("Parent containing the sticker count text and all sticker slots.")]
    public GameObject stickerSlotsRoot;

    [Header("Buttons")]
    public Button closeButton;
    public Button sellButton;
    public Button moveButton;
    public Button favoriteButton;

    [Header("Favorite")]
    public TMP_Text favoriteButtonText;
    public string favoriteOffText = "Favorite";
    public string favoriteOnText = "Favorited";

    [Header("Selling")]
    public TMP_Text sellButtonText;
    [Range(0f, 1f)] public float sellMultiplier = 1f;

    [Header("Sell Confirmation")]
    public bool confirmSingleSell = true;
    public float singleSellConfirmationThreshold = 50f;

    [Header("Gem Sprites")]
    public Sprite t1Gem;
    public Sprite t2Gem;
    public Sprite t3Gem;
    public Sprite rubyGem;
    public Sprite sapphireGem;
    public Sprite emeraldGem;
    public Sprite blackPearlGem;

    private InventoryItem currentItem;
    private TMP_Text cachedMoveButtonText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "Duplicate SkinInspectUI found, using the newest active instance.");
        }

        Instance = this;

        if (sellButtonText == null && sellButton != null)
            sellButtonText = sellButton.GetComponentInChildren<TMP_Text>(true);

        if (favoriteButtonText == null && favoriteButton != null)
        {
            favoriteButtonText =
                favoriteButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (moveButton != null)
            cachedMoveButtonText = moveButton.GetComponentInChildren<TMP_Text>(true);

        SetupButton(closeButton, Close);
        SetupButton(sellButton, SellCurrentItem);
        SetupButton(moveButton, MoveCurrentItem);
        SetupButton(favoriteButton, ToggleFavoriteCurrentItem);
        Close();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private static void SetupButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    public void OpenOwnedItem(InventoryItem item)
    {
        currentItem = item;

        if (item == null || item.skin == null)
        {
            Debug.LogWarning(
                "SkinInspectUI: tried to inspect a null item.");
            return;
        }

        SkinData skin = item.skin;

        if (overlayRoot != null)
            overlayRoot.SetActive(true);
        else
            gameObject.SetActive(true);

        UpdateBackground(skin);
        UpdateSkinImage(skin);
        UpdateText(item, skin);
        UpdateFloatBar(item);
        UpdateGemIcon(item, skin);
        UpdateStickerSlotVisibility(skin);
        UpdateSellButton();
        UpdateFavoriteButton();
        UpdateMoveButton();
    }

    private void UpdateStickerSlotVisibility(SkinData skin)
    {
        if (stickerSlotsRoot == null)
            return;

        stickerSlotsRoot.SetActive(SupportsStickers(skin));
    }

    private static bool SupportsStickers(SkinData skin)
    {
        if (skin == null)
            return false;

        // Current Rare Special assets are knives or gloves. Neither category
        // receives weapon-sticker slots.
        if (skin.rarity == Rarity.RareSpecial)
            return false;

        string weaponName = (skin.weaponName ?? "").ToLowerInvariant();

        string[] nonStickerTerms =
        {
            "knife",
            "bayonet",
            "karambit",
            "dagger",
            "glove",
            "hand wrap",
            "handwrap",
            "falchion",
            "bowie",
            "huntsman",
            "butterfly",
            "navaja",
            "stiletto",
            "talon",
            "ursus",
            "nomad",
            "paracord",
            "survival",
            "skeleton",
            "kukri",
            "gut knife",
            "flip knife"
        };

        for (int i = 0; i < nonStickerTerms.Length; i++)
        {
            if (weaponName.Contains(nonStickerTerms[i]))
                return false;
        }

        return true;
    }

    private void UpdateSellButton()
    {
        if (sellButton == null)
            return;

        bool hasItem = currentItem != null && currentItem.skin != null;
        bool canSell =
            hasItem &&
            !currentItem.favorite &&
            InventoryManager.Instance != null &&
            SaveManager.Instance != null;

        sellButton.interactable = canSell;

        if (sellButtonText == null)
            return;

        if (!hasItem)
            sellButtonText.text = "Sell";
        else if (currentItem.favorite)
            sellButtonText.text = "Favorited";
        else
            sellButtonText.text = $"Sell\n{GetSellValue(currentItem):0.##}";
    }

    private void ToggleFavoriteCurrentItem()
    {
        if (currentItem == null)
        {
            Debug.LogWarning(
                "SkinInspectUI: No current item to favorite.");
            return;
        }

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.ToggleFavorite(currentItem);
        else
            currentItem.favorite = !currentItem.favorite;

        UpdateFavoriteButton();
        UpdateSellButton();
    }

    private void UpdateFavoriteButton()
    {
        if (favoriteButton == null)
            return;

        bool hasItem = currentItem != null && currentItem.skin != null;
        favoriteButton.interactable = hasItem;

        if (favoriteButtonText == null)
            return;

        favoriteButtonText.text = hasItem && currentItem.favorite
            ? favoriteOnText
            : favoriteOffText;
    }

    private void UpdateMoveButton()
    {
        if (moveButton == null)
            return;

        InventoryManager manager = InventoryManager.Instance;

        if (currentItem == null ||
            manager == null ||
            manager.UnlockedStoragePages <= 1)
        {
            moveButton.interactable = false;

            if (cachedMoveButtonText != null)
                cachedMoveButtonText.text = "MOVE";

            return;
        }

        int destination =
            manager.FindNextStorageWithSpace(currentItem.storageIndex);

        bool canMove = destination >= 0;
        moveButton.interactable = canMove;

        if (cachedMoveButtonText != null)
        {
            cachedMoveButtonText.text = canMove
                ? $"MOVE\nTO {destination + 1}"
                : "STORAGE\nFULL";
        }
    }

    private void MoveCurrentItem()
    {
        if (currentItem == null || InventoryManager.Instance == null)
            return;

        int destination =
            InventoryManager.Instance.MoveItemToNextStorage(currentItem);

        if (destination < 0)
        {
            UpdateMoveButton();
            return;
        }

        Debug.Log(
            $"Moved inspected item to Storage {destination + 1}.");

        Close();
    }

    private float GetSellValue(InventoryItem item)
    {
        if (item == null || item.skin == null)
            return 0f;

        float value = item.marketValue;

        if (value <= 0f)
        {
            value = PriceCalculator.GetPrice(item);
            item.marketValue = value;
        }

        return value * sellMultiplier;
    }

    private void SellCurrentItem()
    {
        if (currentItem == null || currentItem.skin == null)
        {
            Debug.LogWarning(
                "SkinInspectUI: No item selected to sell.");
            return;
        }

        if (currentItem.favorite)
        {
            Debug.LogWarning(
                "SkinInspectUI: Cannot sell a favorited item. " +
                "Unfavorite it first.");
            return;
        }

        float sellValue = GetSellValue(currentItem);
        bool shouldConfirm =
            confirmSingleSell &&
            sellValue >= singleSellConfirmationThreshold;

        if (shouldConfirm && SellConfirmationPopupUI.Instance != null)
        {
            string itemName =
                SkinDisplayUtility.GetDisplayName(currentItem.skin);

            SellConfirmationPopupUI.Instance.Show(
                "Confirm Sell",
                $"Sell {itemName} for {sellValue:0.##} gold?",
                "Sell",
                "Cancel",
                ConfirmSellCurrentItem);
            return;
        }

        ConfirmSellCurrentItem();
    }

    private void ConfirmSellCurrentItem()
    {
        if (currentItem == null || currentItem.skin == null)
            return;

        if (currentItem.favorite)
        {
            Debug.LogWarning(
                "SkinInspectUI: Cannot sell a favorited item. " +
                "Unfavorite it first.");
            return;
        }

        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning(
                "SkinInspectUI: Cannot sell because InventoryManager is missing.");
            return;
        }

        if (SaveManager.Instance == null)
        {
            Debug.LogWarning(
                "SkinInspectUI: Cannot sell because SaveManager is missing.");
            return;
        }

        float sellValue = GetSellValue(currentItem);
        string soldName =
            SkinDisplayUtility.GetDisplayName(currentItem.skin);

        bool removed =
            InventoryManager.Instance.RemoveItem(currentItem);

        if (!removed)
        {
            Debug.LogWarning(
                $"SkinInspectUI: Failed to remove sold item: {soldName}");
            return;
        }

        SaveManager.Instance.AddGold(sellValue);

        Debug.Log(
            $"Sold {soldName} for {sellValue:0.##} gold.");

        currentItem = null;
        Close();
    }

    private void UpdateBackground(SkinData skin)
    {
        if (rarityBackground == null)
            return;

        Color rarityColor =
            RarityColorUtility.GetColor(skin.rarity);

        Color backgroundColor =
            Color.Lerp(Color.black, rarityColor, 0.55f);

        backgroundColor.a = 1f;
        rarityBackground.color = backgroundColor;
    }

    private void UpdateSkinImage(SkinData skin)
    {
        if (skinImage == null)
            return;

        skinImage.sprite = skin.icon;
        skinImage.enabled = skin.icon != null;
        skinImage.preserveAspect = true;
    }

    private void UpdateText(InventoryItem item, SkinData skin)
    {
        if (weaponNameText != null)
            weaponNameText.text = (skin.weaponName ?? "").ToUpperInvariant();

        if (skinNameText != null)
            skinNameText.text = skin.isVanilla ? "Vanilla" : skin.skinName;

        if (rarityText != null)
            rarityText.text = skin.rarity.ToString();

        if (collectionText != null)
        {
            if (skin.collectionData != null)
                collectionText.text = skin.collectionData.collectionName;
            else if (!string.IsNullOrWhiteSpace(skin.collection))
                collectionText.text = skin.collection;
            else
                collectionText.text = "Unknown Source";
        }

        if (wearText != null)
            wearText.text = SkinDisplayUtility.GetWearDisplay(item);

        if (floatText != null)
        {
            floatText.text = item.isVanilla
                ? "Float: Vanilla"
                : $"Float: {SkinDisplayUtility.GetInspectFloatDisplay(item)}";
        }

        if (patternText != null)
        {
            patternText.text = item.isVanilla
                ? "Pattern: None"
                : $"Pattern: {item.patternId}";
        }

        if (patternTierText != null)
        {
            string tierText = GetPatternTierText(item, skin);
            patternTierText.text = tierText;
            patternTierText.gameObject.SetActive(
                !string.IsNullOrWhiteSpace(tierText));
        }
    }

    private void UpdateFloatBar(InventoryItem item)
    {
        if (floatWearBar == null)
            return;

        if (item.isVanilla)
        {
            floatWearBar.gameObject.SetActive(false);
            return;
        }

        floatWearBar.gameObject.SetActive(true);
        floatWearBar.Setup((float)item.floatValue);
    }

    private void UpdateGemIcon(InventoryItem item, SkinData skin)
    {
        if (gemTierIcon == null)
            return;

        Sprite icon = GetGemSprite(item, skin);
        gemTierIcon.sprite = icon;
        gemTierIcon.gameObject.SetActive(icon != null);
    }

    private static string GetPatternTierText(
        InventoryItem item,
        SkinData skin)
    {
        if (item == null || skin == null || item.isVanilla)
            return "";

        if (item.patternTier != PatternTier.None)
            return $"Gem Tier: {FormatPatternTier(item.patternTier)}";

        if (skin.patternType != PatternType.None)
            return $"Pattern Type: {skin.patternType}";

        return "";
    }

    private static string FormatPatternTier(PatternTier tier)
    {
        switch (tier)
        {
            case PatternTier.Tier1: return "Tier 1";
            case PatternTier.Tier2: return "Tier 2";
            case PatternTier.Tier3: return "Tier 3";
            default: return tier.ToString();
        }
    }

    private Sprite GetGemSprite(
        InventoryItem item,
        SkinData skin)
    {
        if (item == null || skin == null || item.isVanilla)
            return null;

        switch (item.patternTier)
        {
            case PatternTier.Tier1: return t1Gem;
            case PatternTier.Tier2: return t2Gem;
            case PatternTier.Tier3: return t3Gem;
        }

        string lowerName = (skin.skinName ?? "").ToLowerInvariant();

        if (lowerName.Contains("ruby"))
            return rubyGem;
        if (lowerName.Contains("sapphire"))
            return sapphireGem;
        if (lowerName.Contains("emerald"))
            return emeraldGem;
        if (lowerName.Contains("black pearl"))
            return blackPearlGem;

        return null;
    }

    public void Close()
    {
        currentItem = null;

        if (overlayRoot != null)
            overlayRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    public InventoryItem GetCurrentItem()
    {
        return currentItem;
    }
}
