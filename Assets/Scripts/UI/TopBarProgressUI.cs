using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TopBarProgressUI : MonoBehaviour
{
    [Header("Text")]
    public TMP_Text xpText;
    public TMP_Text rankText;

    [Header("XP Bar")]
    public Image xpFillImage;

    [Header("Rank Icon")]
    public Image rankIconImage;

    public Sprite silverIIcon;
    public Sprite silverIIIcon;
    public Sprite silverIIIIcon;
    public Sprite silverEliteIcon;
    public Sprite silverEliteMasterIcon;

    public Sprite goldNovaIIcon;
    public Sprite goldNovaIIIcon;
    public Sprite goldNovaIIIIcon;
    public Sprite goldNovaMasterIcon;

    public Sprite masterGuardianIIcon;
    public Sprite masterGuardianIIIcon;
    public Sprite masterGuardianEliteIcon;
    public Sprite distinguishedMasterGuardianIcon;

    public Sprite legendaryEagleIcon;
    public Sprite legendaryEagleMasterIcon;
    public Sprite supremeMasterFirstClassIcon;

    [Header("Global Elite Icons")]
public Sprite globalEliteIIcon;
public Sprite globalEliteIIIcon;
public Sprite globalEliteIIIIcon;
public Sprite globalEliteIVIcon;
public Sprite globalEliteVIcon;
public Sprite globalEliteVIIcon;
public Sprite globalEliteVIIIcon;
public Sprite globalEliteVIIIIcon;
public Sprite globalEliteIXIcon;
public Sprite globalEliteXIcon;
public Sprite theGlobalEliteIcon;

[Header("Fallbacks")]
public Sprite missingIcon;

    private SaveManager subscribedManager;

    private void OnEnable()
    {
        TrySubscribe();
        Refresh();
    }

    private void Start()
    {
        TrySubscribe();
        Refresh();

        // Small delay helps if SaveManager loads after UI appears.
        Invoke(nameof(Refresh), 0.1f);
    }

    private void OnDisable()
    {
        if (subscribedManager != null)
        {
            subscribedManager.OnProgressChanged -= Refresh;
            subscribedManager = null;
        }
    }

    private void TrySubscribe()
    {
        if (SaveManager.Instance == null)
            return;

        if (subscribedManager == SaveManager.Instance)
            return;

        if (subscribedManager != null)
            subscribedManager.OnProgressChanged -= Refresh;

        subscribedManager = SaveManager.Instance;
        subscribedManager.OnProgressChanged += Refresh;
    }

    public void Refresh()
    {
        if (SaveManager.Instance == null)
        {
            if (xpText != null)
                xpText.text = "0 / 800";

            if (rankText != null)
                rankText.text = "Silver I";

            if (xpFillImage != null)
                xpFillImage.fillAmount = 0f;

            if (rankIconImage != null)
                rankIconImage.sprite = silverIIcon != null ? silverIIcon : missingIcon;

            return;
        }

        int totalXP = SaveManager.Instance.XP;
        PlayerRank currentRank = SaveManager.Instance.CurrentRank;

        int xpIntoRank = PlayerProgressUtility.GetXPIntoCurrentRank(totalXP);
        int xpNeeded = PlayerProgressUtility.GetXPNeededForNextRank(totalXP);
        float progress01 = PlayerProgressUtility.GetRankProgress01(totalXP);

        if (rankText != null)
        {
            rankText.text = PlayerProgressUtility.GetRankDisplayName(currentRank);
        }

        if (xpText != null)
        {
            if (PlayerProgressUtility.IsMaxRank(currentRank))
            {
                xpText.text = "MAX";
            }
            else
            {
                xpText.text = $"{xpIntoRank} / {xpNeeded}";
            }
        }

        if (xpFillImage != null)
        {
            xpFillImage.fillAmount = progress01;
        }

        UpdateRankIcon(currentRank);
    }

    private void UpdateRankIcon(PlayerRank rank)
    {
        if (rankIconImage == null)
            return;

        Sprite icon = GetIconForRank(rank);

        rankIconImage.sprite = icon;
        rankIconImage.enabled = icon != null;
        rankIconImage.preserveAspect = true;
    }

    private Sprite GetIconForRank(PlayerRank rank)
    {
        switch (rank)
        {
            case PlayerRank.SilverI:
                return silverIIcon != null ? silverIIcon : missingIcon;

            case PlayerRank.SilverII:
                return silverIIIcon != null ? silverIIIcon : missingIcon;

            case PlayerRank.SilverIII:
                return silverIIIIcon != null ? silverIIIIcon : missingIcon;

            case PlayerRank.SilverElite:
                return silverEliteIcon != null ? silverEliteIcon : missingIcon;

            case PlayerRank.SilverEliteMaster:
                return silverEliteMasterIcon != null ? silverEliteMasterIcon : missingIcon;

            case PlayerRank.GoldNovaI:
                return goldNovaIIcon != null ? goldNovaIIcon : missingIcon;

            case PlayerRank.GoldNovaII:
                return goldNovaIIIcon != null ? goldNovaIIIcon : missingIcon;

            case PlayerRank.GoldNovaIII:
                return goldNovaIIIIcon != null ? goldNovaIIIIcon : missingIcon;

            case PlayerRank.GoldNovaMaster:
                return goldNovaMasterIcon != null ? goldNovaMasterIcon : missingIcon;

            case PlayerRank.MasterGuardianI:
                return masterGuardianIIcon != null ? masterGuardianIIcon : missingIcon;

            case PlayerRank.MasterGuardianII:
                return masterGuardianIIIcon != null ? masterGuardianIIIcon : missingIcon;

            case PlayerRank.MasterGuardianElite:
                return masterGuardianEliteIcon != null ? masterGuardianEliteIcon : missingIcon;

            case PlayerRank.DistinguishedMasterGuardian:
                return distinguishedMasterGuardianIcon != null ? distinguishedMasterGuardianIcon : missingIcon;

            case PlayerRank.LegendaryEagle:
                return legendaryEagleIcon != null ? legendaryEagleIcon : missingIcon;

            case PlayerRank.LegendaryEagleMaster:
                return legendaryEagleMasterIcon != null ? legendaryEagleMasterIcon : missingIcon;

            case PlayerRank.SupremeMasterFirstClass:
                return supremeMasterFirstClassIcon != null ? supremeMasterFirstClassIcon : missingIcon;

case PlayerRank.GlobalElite:
    return globalEliteIIcon != null ? globalEliteIIcon : missingIcon;

case PlayerRank.GlobalEliteII:
    return globalEliteIIIcon != null ? globalEliteIIIcon : missingIcon;

case PlayerRank.GlobalEliteIII:
    return globalEliteIIIIcon != null ? globalEliteIIIIcon : missingIcon;

case PlayerRank.GlobalEliteIV:
    return globalEliteIVIcon != null ? globalEliteIVIcon : missingIcon;

case PlayerRank.GlobalEliteV:
    return globalEliteVIcon != null ? globalEliteVIcon : missingIcon;

case PlayerRank.GlobalEliteVI:
    return globalEliteVIIcon != null ? globalEliteVIIcon : missingIcon;

case PlayerRank.GlobalEliteVII:
    return globalEliteVIIIcon != null ? globalEliteVIIIcon : missingIcon;

case PlayerRank.GlobalEliteVIII:
    return globalEliteVIIIIcon != null ? globalEliteVIIIIcon : missingIcon;

case PlayerRank.GlobalEliteIX:
    return globalEliteIXIcon != null ? globalEliteIXIcon : missingIcon;

case PlayerRank.GlobalEliteX:
    return globalEliteXIcon != null ? globalEliteXIcon : missingIcon;

case PlayerRank.TheGlobalElite:
    return theGlobalEliteIcon != null ? theGlobalEliteIcon : missingIcon;

            default:
                return missingIcon;
        }
    }
}