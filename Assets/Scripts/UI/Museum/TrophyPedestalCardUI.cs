using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TrophyPedestalCardUI : MonoBehaviour
{
    [Header("State Roots")]
    [SerializeField] private GameObject lockedRoot;
    [SerializeField] private GameObject emptyRoot;
    [SerializeField] private GameObject occupiedRoot;

    [Header("Presentation")]
    [SerializeField] private TMP_Text pedestalNumberText;
    [SerializeField] private TMP_Text multiplierText;
    [SerializeField] private TMP_Text contributionText;
    [SerializeField] private TMP_Text unlockText;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private Image itemIcon;

    [Header("Actions")]
    [SerializeField] private Button addButton;
    [SerializeField] private Button inspectOrReplaceButton;
    [SerializeField] private Button removeButton;

    private int slotIndex;
    private TrophyRoomPanelUI owner;

    private void Awake()
    {
        BindButton(addButton, HandleOpenSelection);
        BindButton(inspectOrReplaceButton, HandleOpenSelection);
        BindButton(removeButton, HandleRemove);
    }

    private void OnDestroy()
    {
        UnbindButton(addButton, HandleOpenSelection);
        UnbindButton(inspectOrReplaceButton, HandleOpenSelection);
        UnbindButton(removeButton, HandleRemove);
    }

    public void Bind(
        TrophyRoomSlotSnapshot snapshot,
        TrophyRoomPanelUI panel)
    {
        owner = panel;
        slotIndex = snapshot != null ? snapshot.slotIndex : -1;

        bool unlocked = snapshot != null && snapshot.unlocked;
        bool occupied = snapshot != null && snapshot.occupied;

        SetActive(lockedRoot, !unlocked);
        SetActive(emptyRoot, unlocked && !occupied);
        SetActive(occupiedRoot, unlocked && occupied);

        if (pedestalNumberText != null)
        {
            pedestalNumberText.text = slotIndex >= 0
                ? $"PEDESTAL {slotIndex + 1}"
                : "PEDESTAL";
        }

        if (multiplierText != null)
        {
            multiplierText.text = snapshot != null
                ? $"Contribution x{snapshot.pedestalMultiplier:0.##}"
                : "Contribution x1";
        }

        if (unlockText != null)
        {
            unlockText.text = slotIndex >= 0
                ? $"Purchase Trophy Pedestal {slotIndex + 1} in Upgrades"
                : "Locked";
        }

        if (contributionText != null)
        {
            contributionText.text = occupied && snapshot.power != null
                ? $"{snapshot.power.finalContribution:N0} Trophy Power"
                : unlocked ? "Empty pedestal" : "Locked";
        }

        if (itemNameText != null)
        {
            itemNameText.text = occupied && snapshot.item != null
                ? SkinDisplayUtility.GetDisplayName(snapshot.item.skin)
                : "";
        }

        if (itemIcon != null)
        {
            itemIcon.sprite = occupied && snapshot.item != null
                ? snapshot.item.skin.icon
                : null;
            itemIcon.enabled = itemIcon.sprite != null;
        }

        if (addButton != null)
            addButton.interactable = unlocked && !occupied;

        if (inspectOrReplaceButton != null)
            inspectOrReplaceButton.interactable = unlocked && occupied;

        if (removeButton != null)
            removeButton.interactable = unlocked && occupied;
    }

    private void HandleOpenSelection()
    {
        if (owner != null && slotIndex >= 0)
            owner.OpenSelection(slotIndex);
    }

    private void HandleRemove()
    {
        if (owner != null && slotIndex >= 0)
            owner.RemoveFromPedestal(slotIndex);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private static void BindButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void UnbindButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }
}
