using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CaseInspectUI : MonoBehaviour
{
    public static CaseInspectUI Instance { get; private set; }

    [Header("Root")]
    public GameObject root;

    [Header("Main Window")]
    public Image caseImage;
    public TMP_Text caseNameText;
    public TMP_Text completionText;
    public Button completionButton;
    public Button closeButton;

    [Header("Main Drop List")]
    public Transform dropContent;
    public CaseInspectDropCardUI dropCardPrefab;

    [Header("Rare Special Placeholder")]
    public Sprite rareSpecialPlaceholderSprite;

    [Header("Rare Special List Panel")]
    public GameObject rareSpecialListPanel;
    public Transform rareSpecialContent;
    public TMP_Text rareSpecialListTitleText;
    public Button rareSpecialBackButton;

    [Header("Popups")]
    public CaseInspectSkinInfoPopupUI skinInfoPopup;
    public CaseInspectCompletionPopupUI completionPopup;

    private readonly List<GameObject> spawnedMainCards = new List<GameObject>();
    private readonly List<GameObject> spawnedRareSpecialCards = new List<GameObject>();

    private readonly List<SkinData> normalSkins = new List<SkinData>();
    private readonly List<SkinData> rareSpecialSkins = new List<SkinData>();

    private CaseData currentCase;

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

        if (completionButton != null)
        {
            completionButton.onClick.RemoveAllListeners();
            completionButton.onClick.AddListener(OpenCompletionInfo);
        }

        if (rareSpecialBackButton != null)
        {
            rareSpecialBackButton.onClick.RemoveAllListeners();
            rareSpecialBackButton.onClick.AddListener(CloseRareSpecialList);
        }

        Close();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Open(CaseData caseData)
    {
        currentCase = caseData;

        if (currentCase == null)
        {
            Close();
            return;
        }

        if (root != null)
            root.SetActive(true);

        if (rareSpecialListPanel != null)
            rareSpecialListPanel.SetActive(false);

        BuildSkinLists();
        RefreshHeader();
        SpawnMainCards();
    }

    public void Close()
    {
        currentCase = null;

        ClearMainCards();
        ClearRareSpecialCards();

        if (rareSpecialListPanel != null)
            rareSpecialListPanel.SetActive(false);

        if (skinInfoPopup != null)
            skinInfoPopup.Close();

        if (completionPopup != null)
            completionPopup.Close();

        if (root != null)
            root.SetActive(false);
    }

    private void BuildSkinLists()
    {
        normalSkins.Clear();
        rareSpecialSkins.Clear();

        if (currentCase == null || currentCase.dropPool == null)
            return;

        HashSet<string> normalIds = new HashSet<string>();
        HashSet<string> rareIds = new HashSet<string>();

        foreach (WeightedDrop drop in currentCase.dropPool)
        {
            if (drop == null || drop.skin == null)
                continue;

            SkinData skin = drop.skin;
            string id = GetSkinId(skin);

            if (skin.rarity == Rarity.RareSpecial)
            {
                if (rareIds.Add(id))
                    rareSpecialSkins.Add(skin);
            }
            else
            {
                if (normalIds.Add(id))
                    normalSkins.Add(skin);
            }
        }

        normalSkins.Sort(CompareSkinsForInspect);
        rareSpecialSkins.Sort(CompareSkinsForInspect);
    }

    private int CompareSkinsForInspect(SkinData a, SkinData b)
    {
        if (a == null && b == null)
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        int rarityCompare = b.rarity.CompareTo(a.rarity);
        if (rarityCompare != 0)
            return rarityCompare;

        int weaponCompare = string.Compare(a.weaponName, b.weaponName);
        if (weaponCompare != 0)
            return weaponCompare;

        return string.Compare(a.skinName, b.skinName);
    }

    private string GetSkinId(SkinData skin)
    {
        if (skin == null)
            return "";

        if (!string.IsNullOrWhiteSpace(skin.apiId))
            return skin.apiId;

        return $"{skin.weaponName}|{skin.skinName}|{skin.rarity}";
    }

    private void RefreshHeader()
    {
        if (currentCase == null)
            return;

        if (caseImage != null)
        {
            caseImage.sprite = currentCase.icon;
            caseImage.enabled = currentCase.icon != null;
            caseImage.preserveAspect = true;
        }

        if (caseNameText != null)
            caseNameText.text = currentCase.caseName;

        if (completionText != null)
            completionText.text = $"Found 0 / {GetCompletionTargetCount()}";
    }

    private int GetCompletionTargetCount()
    {
        int count = normalSkins.Count;

        if (rareSpecialSkins.Count > 0)
            count += 1;

        return count;
    }

    private void SpawnMainCards()
    {
        ClearMainCards();

        if (dropContent == null || dropCardPrefab == null)
            return;

        // Rare Special always first = top-left
        if (rareSpecialSkins.Count > 0)
        {
            CaseInspectDropCardUI rareCard = Instantiate(dropCardPrefab, dropContent);
            rareCard.SetupRareSpecialEntry(this, rareSpecialSkins.Count, rareSpecialPlaceholderSprite);
            spawnedMainCards.Add(rareCard.gameObject);
        }

        // Then the normal skins (Covert first, then Classified, etc.)
        foreach (SkinData skin in normalSkins)
        {
            CaseInspectDropCardUI card = Instantiate(dropCardPrefab, dropContent);
            card.Setup(skin, this);
            spawnedMainCards.Add(card.gameObject);
        }
    }

    public void OpenRareSpecialList()
    {
        if (rareSpecialSkins.Count == 0)
            return;

        if (rareSpecialListPanel != null)
            rareSpecialListPanel.SetActive(true);

        if (rareSpecialListTitleText != null)
            rareSpecialListTitleText.text = "Rare Special Items";

        SpawnRareSpecialCards();
    }

    private void CloseRareSpecialList()
    {
        ClearRareSpecialCards();

        if (rareSpecialListPanel != null)
            rareSpecialListPanel.SetActive(false);
    }

    private void SpawnRareSpecialCards()
    {
        ClearRareSpecialCards();

        if (rareSpecialContent == null || dropCardPrefab == null)
            return;

        foreach (SkinData skin in rareSpecialSkins)
        {
            CaseInspectDropCardUI card = Instantiate(dropCardPrefab, rareSpecialContent);
            card.Setup(skin, this);
            spawnedRareSpecialCards.Add(card.gameObject);
        }
    }

    public void OpenSkinInfo(SkinData skin)
    {
        if (skinInfoPopup == null || skin == null)
            return;

        skinInfoPopup.Open(skin, currentCase);
    }

    private void OpenCompletionInfo()
    {
        if (completionPopup == null)
            return;

        completionPopup.Open(currentCase);
    }

    private void ClearMainCards()
    {
        foreach (GameObject obj in spawnedMainCards)
        {
            if (obj != null)
                Destroy(obj);
        }

        spawnedMainCards.Clear();
    }

    private void ClearRareSpecialCards()
    {
        foreach (GameObject obj in spawnedRareSpecialCards)
        {
            if (obj != null)
                Destroy(obj);
        }

        spawnedRareSpecialCards.Clear();
    }
}