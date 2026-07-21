using TMPro;
using UnityEngine;

public class CaseShopRankDividerUI : MonoBehaviour
{
    public TMP_Text titleText;

    private void Awake()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text != null)
                text.raycastTarget = false;
        }
    }

    public void Setup(PlayerRank requiredRank)
    {
        Setup(requiredRank, CaseShopCategory.Cases);
    }

    public void Setup(
        PlayerRank requiredRank,
        CaseShopCategory category)
    {
        if (titleText == null)
            return;

        if (requiredRank == PlayerRank.SilverI)
        {
            titleText.text = GetStarterTitle(category);
            return;
        }

        titleText.text =
            $"Unlocked at {PlayerProgressUtility.GetRankDisplayName(requiredRank)}";
    }

    private static string GetStarterTitle(CaseShopCategory category)
    {
        switch (category)
        {
            case CaseShopCategory.Collections:
                return "Starter Collections";

            case CaseShopCategory.SouvenirCollections:
                return "Starter Souvenir Packages";

            case CaseShopCategory.CustomCases:
                return "Starter Custom Cases";

            default:
                return "Starter Cases";
        }
    }
}
