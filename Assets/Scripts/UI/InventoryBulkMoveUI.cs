using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds a Move action to InventoryUI selection mode and presents every unlocked
/// storage container as a destination. Attach this component to an object that
/// remains active while the skin inventory panel is open, not to popupRoot.
/// </summary>
public class InventoryBulkMoveUI : MonoBehaviour
{
    [Header("Inventory")]
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private Transform inventoryGridContent;

    [Header("Selection Move Button")]
    [SerializeField] private Button moveSelectedButton;
    [SerializeField] private TMP_Text moveSelectedButtonText;
    [SerializeField] private string moveButtonLabel = "Move";
    [SerializeField, Min(0.02f)] private float selectionPollInterval = 0.05f;

    [Header("Destination Popup")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Transform destinationButtonsContent;
    [Tooltip("Inactive scene template or prefab used for each storage option.")]
    [SerializeField] private Button destinationButtonTemplate;
    [SerializeField] private Button closeButton;

    [Header("Display")]
    [SerializeField] private string popupTitleFormat = "Move {0} Item(s)";
    [SerializeField] private string storageLabelFormat =
        "Storage {0}\n{1} / {2}";
    [SerializeField] private string currentStorageSuffix = "  CURRENT";
    [SerializeField] private string insufficientSpaceSuffix = "  FULL";

    private readonly List<Button> destinationButtonPool =
        new List<Button>();

    private readonly List<InventoryItem> pendingItems =
        new List<InventoryItem>();

    private readonly HashSet<string> pendingInstanceIds =
        new HashSet<string>();

    private bool lastSelectionModeState;
    private int lastSelectedCount = -1;
    private float nextSelectionPollTime;
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        RefreshMoveButtonState(true);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextSelectionPollTime)
            return;

        nextSelectionPollTime =
            Time.unscaledTime + selectionPollInterval;

        RefreshMoveButtonState(false);
    }

    private void OnDisable()
    {
        ClosePopup();
    }

    private void Initialize()
    {
        if (initialized)
            return;

        if (inventoryUI == null)
            inventoryUI = InventoryUI.Instance;

        if (inventoryGridContent == null && inventoryUI != null)
            inventoryGridContent = inventoryUI.gridContent;

        if (moveSelectedButton != null)
        {
            moveSelectedButton.onClick.RemoveListener(OpenDestinationPopup);
            moveSelectedButton.onClick.AddListener(OpenDestinationPopup);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePopup);
            closeButton.onClick.AddListener(ClosePopup);
        }

        if (destinationButtonTemplate != null)
            destinationButtonTemplate.gameObject.SetActive(false);

        if (popupRoot != null)
            popupRoot.SetActive(false);

        if (moveSelectedButtonText != null)
            moveSelectedButtonText.text = $"{moveButtonLabel} (0)";

        initialized = true;
    }

    private void RefreshMoveButtonState(bool force)
    {
        if (inventoryUI == null)
            inventoryUI = InventoryUI.Instance;

        if (inventoryGridContent == null && inventoryUI != null)
            inventoryGridContent = inventoryUI.gridContent;

        bool selectionMode =
            inventoryUI != null && inventoryUI.SelectionModeActive;

        int selectedCount = selectionMode
            ? CountSelectedCards()
            : 0;

        if (!force &&
            selectionMode == lastSelectionModeState &&
            selectedCount == lastSelectedCount)
        {
            return;
        }

        lastSelectionModeState = selectionMode;
        lastSelectedCount = selectedCount;

        if (moveSelectedButton != null)
        {
            moveSelectedButton.gameObject.SetActive(selectionMode);
            moveSelectedButton.interactable =
                selectionMode && selectedCount > 0;
        }

        if (moveSelectedButtonText != null)
        {
            moveSelectedButtonText.text =
                $"{moveButtonLabel} ({selectedCount})";
        }

        if (!selectionMode)
            ClosePopup();
    }

    private int CountSelectedCards()
    {
        if (inventoryGridContent == null)
            return 0;

        int selectedCount = 0;

        for (int i = 0; i < inventoryGridContent.childCount; i++)
        {
            Transform child = inventoryGridContent.GetChild(i);

            if (child == null || !child.gameObject.activeInHierarchy)
                continue;

            InventoryItemCardUI card =
                child.GetComponent<InventoryItemCardUI>();

            if (card != null && card.IsSelected)
                selectedCount++;
        }

        return selectedCount;
    }

    public void OpenDestinationPopup()
    {
        Initialize();
        CollectSelectedItems();

        if (pendingItems.Count == 0)
        {
            if (statusText != null)
                statusText.text = "Select at least one item first.";

            Debug.LogWarning(
                "InventoryBulkMoveUI: Move was pressed with no selected items.");
            return;
        }

        InventoryManager manager = InventoryManager.Instance;

        if (manager == null)
        {
            Debug.LogWarning(
                "InventoryBulkMoveUI: InventoryManager is missing.");
            return;
        }

        if (popupRoot != null)
            popupRoot.SetActive(true);

        if (titleText != null)
        {
            titleText.text = string.Format(
                popupTitleFormat,
                pendingItems.Count);
        }

        if (statusText != null)
        {
            statusText.text =
                $"Choose a destination with space for all " +
                $"{pendingItems.Count} selected item(s).";
        }

        BuildDestinationButtons(manager);
    }

    public void ClosePopup()
    {
        pendingItems.Clear();
        pendingInstanceIds.Clear();

        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    private void CollectSelectedItems()
    {
        pendingItems.Clear();
        pendingInstanceIds.Clear();

        if (inventoryGridContent == null && inventoryUI != null)
            inventoryGridContent = inventoryUI.gridContent;

        if (inventoryGridContent == null)
            return;

        for (int i = 0; i < inventoryGridContent.childCount; i++)
        {
            Transform child = inventoryGridContent.GetChild(i);

            if (child == null || !child.gameObject.activeInHierarchy)
                continue;

            InventoryItemCardUI card =
                child.GetComponent<InventoryItemCardUI>();

            if (card == null || !card.IsSelected)
                continue;

            InventoryItem item = card.GetItem();

            if (item == null ||
                item.skin == null ||
                string.IsNullOrWhiteSpace(item.instanceId) ||
                !pendingInstanceIds.Add(item.instanceId))
            {
                continue;
            }

            pendingItems.Add(item);
        }
    }

    private void BuildDestinationButtons(InventoryManager manager)
    {
        if (manager == null ||
            destinationButtonsContent == null ||
            destinationButtonTemplate == null)
        {
            return;
        }

        EnsureDestinationButtonPool(manager.UnlockedStoragePages);

        int currentStorage = inventoryUI != null
            ? inventoryUI.CurrentStorageIndex
            : manager.ActiveStorageIndex;

        for (int storageIndex = 0;
             storageIndex < destinationButtonPool.Count;
             storageIndex++)
        {
            Button button = destinationButtonPool[storageIndex];

            if (button == null)
                continue;

            bool shouldShow = storageIndex < manager.UnlockedStoragePages;
            button.gameObject.SetActive(shouldShow);

            if (!shouldShow)
                continue;

            int used = manager.GetStorageItemCount(storageIndex);
            int capacity = manager.ItemsPerStoragePage;
            int free = Mathf.Max(0, capacity - used);
            bool isCurrent = storageIndex == currentStorage;
            bool hasSpace = free >= pendingItems.Count;
            bool canMove = !isCurrent && hasSpace;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
            {
                string text = string.Format(
                    storageLabelFormat,
                    storageIndex + 1,
                    used,
                    capacity);

                if (isCurrent)
                    text += currentStorageSuffix;
                else if (!hasSpace)
                    text += insufficientSpaceSuffix;

                label.text = text;
                label.raycastTarget = false;
            }

            button.interactable = canMove;
            button.onClick.RemoveAllListeners();

            int capturedStorageIndex = storageIndex;
            button.onClick.AddListener(
                () => MovePendingItemsTo(capturedStorageIndex));
        }
    }

    private void EnsureDestinationButtonPool(int requiredCount)
    {
        while (destinationButtonPool.Count < requiredCount)
        {
            Button button = Instantiate(
                destinationButtonTemplate,
                destinationButtonsContent);

            button.gameObject.SetActive(false);
            destinationButtonPool.Add(button);
        }
    }

    private void MovePendingItemsTo(int destinationStorageIndex)
    {
        InventoryManager manager = InventoryManager.Instance;

        if (manager == null || pendingItems.Count == 0)
            return;

        int itemsNeedingMove = 0;

        for (int i = 0; i < pendingItems.Count; i++)
        {
            InventoryItem item = pendingItems[i];

            if (item != null &&
                item.storageIndex != destinationStorageIndex)
            {
                itemsNeedingMove++;
            }
        }

        if (itemsNeedingMove <= 0)
        {
            ClosePopup();
            return;
        }

        if (!manager.HasSpaceInStorage(
                destinationStorageIndex,
                itemsNeedingMove))
        {
            if (statusText != null)
                statusText.text = "That storage no longer has enough space.";

            BuildDestinationButtons(manager);
            return;
        }

        // MoveItemToStorage raises the inventory event. Temporarily detach the
        // expensive full InventoryUI refresh, then refresh it once after all
        // selected items have moved. Other lightweight inventory listeners keep
        // receiving their normal notifications.
        if (inventoryUI != null)
            manager.OnInventoryChanged -= inventoryUI.Refresh;

        int movedCount = 0;

        try
        {
            for (int i = 0; i < pendingItems.Count; i++)
            {
                InventoryItem item = pendingItems[i];

                if (item == null ||
                    item.storageIndex == destinationStorageIndex)
                {
                    continue;
                }

                if (manager.MoveItemToStorage(
                        item,
                        destinationStorageIndex))
                {
                    movedCount++;
                }
            }
        }
        finally
        {
            if (inventoryUI != null)
                manager.OnInventoryChanged += inventoryUI.Refresh;
        }

        ClosePopup();

        if (inventoryUI != null)
        {
            inventoryUI.CancelSelectionMode();
            inventoryUI.Refresh();
        }

        Debug.Log(
            $"Moved {movedCount} selected item(s) to " +
            $"Storage {destinationStorageIndex + 1}.");
    }
}
