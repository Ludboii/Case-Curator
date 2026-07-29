using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optional companion for InventoryItemCardUI. Assign four small Image children
/// to display the applied sticker craft without rendering stickers onto the
/// weapon sprite itself.
/// </summary>
public sealed class InventoryCardStickerIconsUI : MonoBehaviour
{
    [SerializeField] private InventoryItemCardUI card;
    [SerializeField] private GameObject root;
    [SerializeField] private Image[] stickerImages = new Image[4];

    private StickerApplicationService service;
    private string lastInstanceId;
    private float nextRefreshAt;

    private void Awake()
    {
        if (card == null)
            card = GetComponent<InventoryItemCardUI>();

        service = StickerApplicationService.GetOrCreate();
    }

    private void OnEnable()
    {
        if (service == null)
            service = StickerApplicationService.GetOrCreate();

        if (service != null)
        {
            service.OnStickerStateChanged -= Refresh;
            service.OnStickerStateChanged += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (service != null)
            service.OnStickerStateChanged -= Refresh;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshAt)
            return;

        nextRefreshAt = Time.unscaledTime + 0.2f;
        InventoryItem item = card != null ? card.CurrentItem : null;
        string id = item != null ? item.instanceId : "";

        if (id != lastInstanceId)
            Refresh();
    }

    public void Refresh()
    {
        InventoryItem item = card != null ? card.CurrentItem : null;
        lastInstanceId = item != null ? item.instanceId : "";
        bool eligible =
            item != null &&
            !StickerItemUtility.IsSticker(item) &&
            StickerApplicationService.SupportsStickers(item);
        bool hasAny = false;

        for (int i = 0; i < stickerImages.Length; i++)
        {
            Image image = stickerImages[i];

            if (image == null)
                continue;

            AppliedStickerSaveData applied = eligible && service != null
                ? service.GetAppliedSticker(item, i)
                : null;
            StickerData sticker = service != null
                ? service.ResolveSticker(applied)
                : null;

            image.sprite = sticker != null ? sticker.icon : null;
            image.enabled = sticker != null && sticker.icon != null;
            image.preserveAspect = true;
            image.gameObject.SetActive(image.enabled);
            hasAny |= image.enabled;
        }

        if (root != null)
            root.SetActive(hasAny);
    }
}
