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