using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CaseShopCardUI : MonoBehaviour
{
    [Header("Images")]
    public Image caseImage;
    public Image backgroundImage;
    public Image qualityBar;
    public Image buyButtonImage;

    [Header("Completion Border")]
    [Tooltip("Assign the Image on the child named ContainerProgressPanel.")]
    public Image containerProgressPanel;
    public Color noCompletionColor = Color.black;
    public Color bronzeCompletionColor = new Color(0.72f, 0.38f, 0.12f, 1f);
    public Color silverCompletionColor = new Color(0.78f, 0.84f, 0.88f, 1f);
    public Color goldCompletionColor = new Color(1f, 0.72f, 0.08f, 1f);
    public Color diamondCompletionColor = new Color(0.25f, 0.9f, 1f, 1f);

    [Header("Text")]
    public TMP_Text caseNameText;
    public TMP_Text collectionText;
    public TMP_Text buyButtonText;
    public TMP_Text lockText;

    [Header("Buttons")]
    public Button inspectButton;
    public Button buyButton;

    [Header("Button Colors")]
    public Color availableBuyColor = new Color(0.2f, 0.85f, 0.35f, 1f);
    public Color unavailableBuyColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    public Color lockedBuyColor = new Color(0.22f, 0.22f, 0.22f, 1f);

    [Header("Background Settings")]
    [Range(0f, 1f)] public float backgroundAlpha = 0.85f;
    [Range(0f, 1f)] public float backgroundDarkenAmount = 0.25f;

    private CaseData caseData;
    private CaseShopUI shopUI;

    private void Awake()
    {
        CacheCompletionPanel();
        DisableTextRaycasts();
    }

    public void Setup(CaseData data, CaseShopUI owner)
    {
        caseData = data;
        shopUI = owner;

        if (caseData == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        CacheCompletionPanel();
        DisableTextRaycasts();

        if (caseImage != null)
        {
            caseImage.sprite = caseData.icon;
            caseImage.enabled = caseData.icon != null;
            caseImage.preserveAspect = true;
            caseImage.raycastTarget = false;
        }

        if (caseNameText != null)
            caseNameText.text = caseData.caseName;

        ApplyCaseQualityVisuals();

        if (lockText != null)
            lockText.gameObject.SetActive(false);

        if (inspectButton != null)
        {
            inspectButton.onClick.RemoveAllListeners();
            inspectButton.onClick.AddListener(OpenInspect);
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(BuyCase);
        }

        RefreshState();
    }

    public void RefreshState()
    {
        if (caseData == null || shopUI == null)
            return;

        string failReason;
        bool canBuy = shopUI.CanBuyCase(caseData, out failReason);

        int buyAmount = shopUI.GetRequestedBuyAmount(caseData);
        float totalCost = caseData.priceInGold * buyAmount;

        if (buyButton != null)
            buyButton.interactable = canBuy;

        ApplyBuyButtonColor(canBuy, failReason);
        RefreshProgressText();
        ApplyCompletionBorder();

        if (buyButtonText != null)
        {
            if (buyAmount <= 0)
            {
                buyButtonText.text = "BUY\n0.00 G";
            }
            else if (shopUI.IsMaxQuantitySelected)
            {
                buyButtonText.text = $"BUY MAX x{buyAmount}\n{totalCost:0.00} G";
            }
            else
            {
                buyButtonText.text = $"BUY x{buyAmount}\n{totalCost:0.00} G";
            }

            buyButtonText.color = Color.white;
        }
    }

    private void RefreshProgressText()
    {
        if (collectionText == null)
            return;

        if (ContainerProgressManager.Instance != null)
        {
            collectionText.text = ContainerProgressManager.Instance.GetCompletionDisplayText(caseData);
            return;
        }

        collectionText.text = $"Found 0 / {GetCompletionTargetCount(caseData)}";
    }

    private void ApplyCompletionBorder()
    {
        CacheCompletionPanel();

        if (containerProgressPanel == null)
            return;

        ContainerCompletionTier tier = ContainerCompletionTier.None;

        if (ContainerProgressManager.Instance != null)
            tier = ContainerProgressManager.Instance.GetCompletionTier(caseData);

        switch (tier)
        {
            case ContainerCompletionTier.Bronze:
                containerProgressPanel.color = bronzeCompletionColor;
                break;
            case ContainerCompletionTier.Silver:
                containerProgressPanel.color = silverCompletionColor;
                break;
            case ContainerCompletionTier.Gold:
                containerProgressPanel.color = goldCompletionColor;
                break;
            case ContainerCompletionTier.Diamond:
                containerProgressPanel.color = diamondCompletionColor;
                break;
            default:
                containerProgressPanel.color = noCompletionColor;
                break;
        }

        containerProgressPanel.raycastTarget = false;
    }

    private void CacheCompletionPanel()
    {
        if (containerProgressPanel != null)
            return;

        Transform panelTransform = FindChildRecursive(transform, "ContainerProgressPanel");

        if (panelTransform != null)
            containerProgressPanel = panelTransform.GetComponent<Image>();
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform nested = FindChildRecursive(child, childName);

            if (nested != null)
                return nested;
        }

        return null;
    }

    private void ApplyCaseQualityVisuals()
    {
        Color qualityColor = CaseQualityUtility.GetColor(caseData.quality);
        Color backgroundColor = DarkenColor(qualityColor, backgroundDarkenAmount);
        backgroundColor.a = backgroundAlpha;

        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundColor;
            backgroundImage.raycastTarget = false;
        }

        if (qualityBar != null)
        {
            qualityBar.color = qualityColor;
            qualityBar.raycastTarget = false;
        }
    }

    private Color DarkenColor(Color color, float amount)
    {
        amount = Mathf.Clamp01(amount);

        return new Color(
            color.r * (1f - amount),
            color.g * (1f - amount),
            color.b * (1f - amount),
            color.a);
    }

    private void ApplyBuyButtonColor(bool canBuy, string failReason)
    {
        Color targetColor = canBuy ? availableBuyColor : unavailableBuyColor;

        if (!string.IsNullOrWhiteSpace(failReason) &&
            failReason.StartsWith("Rank"))
        {
            targetColor = lockedBuyColor;
        }

        if (buyButtonImage != null)
            buyButtonImage.color = targetColor;

        if (buyButton != null)
        {
            ColorBlock colors = buyButton.colors;
            colors.normalColor = targetColor;
            colors.highlightedColor = targetColor;
            colors.pressedColor = targetColor * 0.9f;
            colors.selectedColor = targetColor;
            colors.disabledColor = targetColor;
            colors.colorMultiplier = 1f;

            buyButton.colors = colors;
        }

        if (buyButtonText != null)
            buyButtonText.color = Color.white;
    }

    private void OpenInspect()
    {
        if (shopUI == null || caseData == null)
            return;

        shopUI.OpenCaseInspect(caseData);
    }

    private void BuyCase()
    {
        if (shopUI == null || caseData == null)
            return;

        shopUI.TryBuyCase(caseData);
    }

    private int GetCompletionTargetCount(CaseData data)
    {
        if (data == null || data.dropPool == null)
            return 0;

        HashSet<string> uniqueNormalSkins = new HashSet<string>();
        bool hasRareSpecialItem = false;

        foreach (WeightedDrop drop in data.dropPool)
        {
            if (drop == null || drop.skin == null)
                continue;

            SkinData skin = drop.skin;

            if (skin.rarity == Rarity.RareSpecial)
            {
                hasRareSpecialItem = true;
                continue;
            }

            string id = skin.apiId;

            if (string.IsNullOrWhiteSpace(id))
                id = skin.weaponName + "|" + skin.skinName;

            uniqueNormalSkins.Add(id);
        }

        int targetCount = uniqueNormalSkins.Count;

        if (hasRareSpecialItem)
            targetCount += 1;

        return targetCount;
    }

    private void DisableTextRaycasts()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text != null)
                text.raycastTarget = false;
        }
    }
}
