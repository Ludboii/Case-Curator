using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays up to four applied sticker icons directly over the weapon image on
/// an InventoryItemCardUI. The visual hierarchy is generated automatically when
/// it has not been authored on the prefab, so existing inventory cards require
/// no manual sticker-slot setup.
/// </summary>
[DisallowMultipleComponent]
public sealed class InventoryCardStickerIconsUI : MonoBehaviour
{
    [SerializeField] private InventoryItemCardUI card;
    [SerializeField] private GameObject root;
    [SerializeField] private Image[] stickerImages = new Image[4];

    [Header("Auto Layout")]
    [Tooltip("Size of each small sticker icon on the inventory card.")]
    [SerializeField] private Vector2 iconSize = new Vector2(16f, 16f);

    [Tooltip("Spacing between the four sticker icons.")]
    [SerializeField] private float spacing = 2f;

    [Tooltip(
        "Offset from the lower-right corner of the weapon image. Negative X " +
        "moves the strip left; positive Y moves it upward.")]
    [SerializeField] private Vector2 anchoredOffset = new Vector2(-4f, 5f);

    private StickerApplicationService service;
    private string lastInstanceId;
    private float nextRefreshAt;

    private void Awake()
    {
        ResolveReferences();
        EnsureVisualHierarchy();
        service = StickerApplicationService.GetOrCreate();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsureVisualHierarchy();

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

    /// <summary>
    /// Called by InventoryItemCardUI after Setup assigns a new inventory item.
    /// </summary>
    public void Refresh()
    {
        ResolveReferences();
        EnsureVisualHierarchy();

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
            image.raycastTarget = false;
            image.gameObject.SetActive(image.enabled);
            hasAny |= image.enabled;
        }

        if (root != null)
            root.SetActive(hasAny);
    }

    private void ResolveReferences()
    {
        if (card == null)
            card = GetComponent<InventoryItemCardUI>();
    }

    private void EnsureVisualHierarchy()
    {
        if (card == null || card.skinImage == null)
            return;

        RectTransform imageParent = card.skinImage.rectTransform;

        if (root == null)
        {
            Transform existing = imageParent.Find("AppliedStickerIconsRoot");

            if (existing != null)
            {
                root = existing.gameObject;
            }
            else
            {
                root = new GameObject(
                    "AppliedStickerIconsRoot",
                    typeof(RectTransform));
                root.transform.SetParent(imageParent, false);
            }
        }

        RectTransform rootRect = root.GetComponent<RectTransform>();

        if (rootRect == null)
            rootRect = root.AddComponent<RectTransform>();

        float totalWidth =
            (iconSize.x * StickerApplicationService.StickerSlotCount) +
            (spacing * (StickerApplicationService.StickerSlotCount - 1));

        rootRect.anchorMin = new Vector2(1f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(1f, 0f);
        rootRect.sizeDelta = new Vector2(totalWidth, iconSize.y);
        rootRect.anchoredPosition = anchoredOffset;
        rootRect.SetAsLastSibling();

        if (stickerImages == null ||
            stickerImages.Length != StickerApplicationService.StickerSlotCount)
        {
            stickerImages =
                new Image[StickerApplicationService.StickerSlotCount];
        }

        for (int i = 0; i < stickerImages.Length; i++)
        {
            Image image = stickerImages[i];

            if (image == null)
            {
                Transform existing = root.transform.Find($"StickerIcon{i + 1}");

                if (existing != null)
                    image = existing.GetComponent<Image>();

                if (image == null)
                {
                    GameObject iconObject = new GameObject(
                        $"StickerIcon{i + 1}",
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image));
                    iconObject.transform.SetParent(root.transform, false);
                    image = iconObject.GetComponent<Image>();
                }

                stickerImages[i] = image;
            }

            RectTransform iconRect = image.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0f);
            iconRect.anchorMax = new Vector2(0f, 0f);
            iconRect.pivot = new Vector2(0f, 0f);
            iconRect.sizeDelta = iconSize;
            iconRect.anchoredPosition =
                new Vector2(i * (iconSize.x + spacing), 0f);

            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;
            image.gameObject.SetActive(false);
        }

        root.SetActive(false);
    }
}
