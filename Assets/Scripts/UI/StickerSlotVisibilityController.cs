using UnityEngine;

/// <summary>
/// Compatibility bridge for scenes that already contain this component.
/// Sticker visibility is now handled directly by SkinInspectUI when an item is
/// opened, so no Update or LateUpdate polling is required.
/// </summary>
public class StickerSlotVisibilityController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SkinInspectUI skinInspectUI;
    [SerializeField] private GameObject stickerSlotsRoot;

    private void Awake()
    {
        if (skinInspectUI == null)
            skinInspectUI = SkinInspectUI.Instance;

        if (skinInspectUI != null && stickerSlotsRoot != null)
            skinInspectUI.stickerSlotsRoot = stickerSlotsRoot;

        enabled = false;
    }

    public void RefreshNow()
    {
        // Kept for existing Inspector bindings. SkinInspectUI refreshes the
        // sticker section automatically whenever OpenOwnedItem is called.
    }
}
