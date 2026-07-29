using TMPro;
using UnityEngine;

/// <summary>
/// CaseShopUI falls back to Enum.ToString for newly added categories. This
/// zero-setup companion formats StickerCapsules as the requested player-facing
/// "Sticker Capsules" label without requiring a scene migration.
/// </summary>
public sealed class StickerCapsuleShopLabelRuntime : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        CaseShopUI[] shops = FindObjectsByType<CaseShopUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < shops.Length; i++)
        {
            CaseShopUI shop = shops[i];

            if (shop != null &&
                shop.GetComponent<StickerCapsuleShopLabelRuntime>() == null)
            {
                shop.gameObject.AddComponent<StickerCapsuleShopLabelRuntime>();
            }
        }
    }

    private CaseShopUI shop;
    private TMP_Text label;

    private void Awake()
    {
        shop = GetComponent<CaseShopUI>();
        label = shop != null ? shop.categoryText : null;
    }

    private void LateUpdate()
    {
        if (label != null && label.text == "StickerCapsules")
            label.text = "Sticker Capsules";
    }
}
