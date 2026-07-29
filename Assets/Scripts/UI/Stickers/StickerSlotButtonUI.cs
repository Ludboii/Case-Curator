using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StickerSlotButtonUI : MonoBehaviour
{
    [Range(0, 3)] public int slotIndex;
    public Button button;
    public Image stickerImage;
    public TMP_Text plusText;
    public GameObject occupiedRoot;
    public GameObject emptyRoot;

    private SkinInspectStickerSlotsUI owner;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    public void Bind(SkinInspectStickerSlotsUI controller)
    {
        owner = controller;

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
        }
    }

    public void SetSticker(StickerData sticker)
    {
        bool occupied = sticker != null;

        if (stickerImage != null)
        {
            stickerImage.sprite = occupied ? sticker.icon : null;
            stickerImage.enabled = occupied && sticker.icon != null;
            stickerImage.preserveAspect = true;
        }

        if (plusText != null)
        {
            plusText.text = occupied ? "" : "+";
            plusText.gameObject.SetActive(!occupied);
        }

        if (occupiedRoot != null)
            occupiedRoot.SetActive(occupied);
        if (emptyRoot != null)
            emptyRoot.SetActive(!occupied);
    }

    private void HandleClicked()
    {
        if (owner != null)
            owner.HandleSlotClicked(slotIndex);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }
}
