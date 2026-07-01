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
        if (titleText == null)
            return;

        if (requiredRank == PlayerRank.SilverI)
        {
            titleText.text = "Starter Cases";
        }
        else
        {
            titleText.text =
                $"Unlocked at {PlayerProgressUtility.GetRankDisplayName(requiredRank)}";
        }
    }
}