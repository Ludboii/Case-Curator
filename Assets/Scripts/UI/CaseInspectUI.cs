using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CaseInspectUI : MonoBehaviour
{
    public static CaseInspectUI Instance { get; private set; }

    [Header("Root")]
    public GameObject root;

    [Header("Images")]
    public Image caseImage;
    public Image qualityBar;

    [Header("Text")]
    public TMP_Text caseNameText;
    public TMP_Text typeText;
    public TMP_Text priceText;
    public TMP_Text rankText;
    public TMP_Text xpText;
    public TMP_Text dropCountText;
    public TMP_Text rarityChanceText;
    public TMP_Text dropPreviewText;
    public TMP_Text buyButtonText;

    [Header("Buttons")]
    public Button buyButton;
    public Button closeButton;

    private CaseData currentCase;
    private CaseShopUI currentShop;

    private void Awake()
    {
        Instance = this;

        if (root == null)
            root = gameObject;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(BuyCurrentCase);
        }

        Close();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Open(CaseData caseData, CaseShopUI shopUI)
    {
        currentCase = caseData;
        currentShop = shopUI;

        if (currentCase == null)
        {
            Close();
            return;
        }

        if (root != null)
            root.SetActive(true);

        Refresh();
    }

    public void Close()
    {
        currentCase = null;
        currentShop = null;

        if (root != null)
            root.SetActive(false);
    }

    private void Refresh()
    {
        if (currentCase == null)
            return;

        if (caseImage != null)
        {
            caseImage.sprite = currentCase.icon;
            caseImage.enabled = currentCase.icon != null;
            caseImage.preserveAspect = true;
        }

        if (qualityBar != null)
        {
            qualityBar.color = CaseQualityUtility.GetColor(currentCase.quality);
        }

        if (caseNameText != null)
            caseNameText.text = currentCase.caseName;

        if (typeText != null)
            typeText.text = $"Type: {currentCase.containerType}";

        if (priceText != null)
            priceText.text = $"Price: {currentCase.priceInGold:0.00} G";

        if (rankText != null)
            rankText.text = $"Required Rank: {PlayerProgressUtility.GetRankDisplayName(currentCase.requiredRank)}";

        if (xpText != null)
            xpText.text = $"XP on open: {currentCase.xpRewardOnOpen}";

        if (dropCountText != null)
            dropCountText.text = $"Possible drops: {GetUniqueDropCount(currentCase)}";

        if (rarityChanceText != null)
            rarityChanceText.text = BuildRarityChanceText(currentCase);

        if (dropPreviewText != null)
            dropPreviewText.text = BuildDropPreviewText(currentCase);

        RefreshBuyButton();
    }

    private void RefreshBuyButton()
    {
        if (currentCase == null || currentShop == null)
            return;

        string failReason;
        bool canBuy = currentShop.CanBuyCase(currentCase, out failReason);

        int amount = currentShop.GetRequestedBuyAmount(currentCase);
        float totalCost = currentCase.priceInGold * amount;

        if (buyButton != null)
            buyButton.interactable = canBuy;

        if (buyButtonText != null)
        {
            if (canBuy)
                buyButtonText.text = $"BUY x{amount}\n{totalCost:0.00} G";
            else
                buyButtonText.text = string.IsNullOrWhiteSpace(failReason)
                    ? "LOCKED"
                    : failReason;
        }
    }

    private void BuyCurrentCase()
    {
        if (currentCase == null || currentShop == null)
            return;

        currentShop.TryBuyCase(currentCase);
        RefreshBuyButton();
    }

    private int GetUniqueDropCount(CaseData caseData)
    {
        if (caseData == null || caseData.dropPool == null)
            return 0;

        HashSet<string> uniqueIds = new HashSet<string>();

        foreach (WeightedDrop drop in caseData.dropPool)
        {
            if (drop == null || drop.skin == null)
                continue;

            string id = drop.skin.apiId;

            if (string.IsNullOrWhiteSpace(id))
                id = $"{drop.skin.weaponName}|{drop.skin.skinName}";

            uniqueIds.Add(id);
        }

        return uniqueIds.Count;
    }

    private string BuildRarityChanceText(CaseData caseData)
    {
        if (caseData == null || caseData.rarityChances == null || caseData.rarityChances.Count == 0)
            return "Rarity chances: not set";

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Rarity chances:");

        foreach (RarityChance chance in caseData.rarityChances)
        {
            if (chance == null)
                continue;

            builder.AppendLine($"{chance.rarity}: {chance.chance:0.###}%");
        }

        return builder.ToString();
    }

    private string BuildDropPreviewText(CaseData caseData)
    {
        if (caseData == null || caseData.dropPool == null || caseData.dropPool.Count == 0)
            return "No drops assigned.";

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Drop preview:");

        int shown = 0;
        int maxShown = 20;

        foreach (WeightedDrop drop in caseData.dropPool)
        {
            if (drop == null || drop.skin == null)
                continue;

            SkinData skin = drop.skin;

            string skinName = skin.isVanilla
                ? "Vanilla"
                : skin.skinName;

            builder.AppendLine($"{skin.rarity} - {skin.weaponName} | {skinName}");

            shown++;

            if (shown >= maxShown)
                break;
        }

        int remaining = caseData.dropPool.Count - shown;

        if (remaining > 0)
            builder.AppendLine($"+ {remaining} more...");

        return builder.ToString();
    }
}