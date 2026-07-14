using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows only unlocked storage buttons and places the add-storage button in the
/// first locked storage slot. This works with both layout groups and manually
/// positioned storage buttons.
/// </summary>
public class InventoryStorageButtonLayoutUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private Button storageButton1;
    [SerializeField] private Button storageButton2;
    [SerializeField] private Button storageButton3;
    [SerializeField] private Button addStorageButton;

    private InventoryManager subscribedManager;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        Refresh();
    }

    private void Start()
    {
        // Handles scenes where InventoryManager initializes after this object.
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void ResolveReferences()
    {
        if (inventoryUI == null)
            inventoryUI = InventoryUI.Instance;

        if (inventoryUI == null)
            return;

        if (storageButton1 == null)
            storageButton1 = inventoryUI.storageButton1;

        if (storageButton2 == null)
            storageButton2 = inventoryUI.storageButton2;

        if (storageButton3 == null)
            storageButton3 = inventoryUI.storageButton3;

        if (addStorageButton == null)
            addStorageButton = inventoryUI.addStorageButton;
    }

    private void Subscribe()
    {
        InventoryManager manager = InventoryManager.Instance;

        if (manager == null || subscribedManager == manager)
            return;

        Unsubscribe();

        subscribedManager = manager;
        subscribedManager.OnInventoryChanged += Refresh;
    }

    private void Unsubscribe()
    {
        if (subscribedManager != null)
            subscribedManager.OnInventoryChanged -= Refresh;

        subscribedManager = null;
    }

    public void Refresh()
    {
        ResolveReferences();

        InventoryManager manager = InventoryManager.Instance;

        if (manager == null)
            return;

        Button[] storageButtons =
        {
            storageButton1,
            storageButton2,
            storageButton3
        };

        int unlockedCount = Mathf.Clamp(
            manager.UnlockedStoragePages,
            1,
            storageButtons.Length);

        for (int i = 0; i < storageButtons.Length; i++)
        {
            Button button = storageButtons[i];

            if (button == null)
                continue;

            bool unlocked = i < unlockedCount;
            button.gameObject.SetActive(unlocked);

            if (unlocked)
                button.transform.SetSiblingIndex(i);
        }

        if (addStorageButton == null)
            return;

        bool canUnlockAnother =
            unlockedCount < storageButtons.Length;

        addStorageButton.gameObject.SetActive(canUnlockAnother);

        if (!canUnlockAnother)
            return;

        int nextStorageIndex = unlockedCount;
        Button nextLockedButton = storageButtons[nextStorageIndex];

        // Correct ordering when the parent uses a Vertical/Grid Layout Group.
        addStorageButton.transform.SetSiblingIndex(nextStorageIndex);

        // Correct placement when the buttons are positioned manually.
        if (nextLockedButton != null)
        {
            RectTransform addRect =
                addStorageButton.transform as RectTransform;

            RectTransform slotRect =
                nextLockedButton.transform as RectTransform;

            if (addRect != null && slotRect != null)
            {
                addRect.anchorMin = slotRect.anchorMin;
                addRect.anchorMax = slotRect.anchorMax;
                addRect.pivot = slotRect.pivot;
                addRect.anchoredPosition = slotRect.anchoredPosition;
            }
        }
    }
}
