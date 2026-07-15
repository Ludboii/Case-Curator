using System;
using System.Collections.Generic;

[Serializable]
public class InventoryTransactionResult
{
    public bool success;
    public string errorMessage;

    public List<InventoryItem> removedItems =
        new List<InventoryItem>();

    public List<InventoryItem> addedItems =
        new List<InventoryItem>();

    public float removedMarketValue;
    public float addedMarketValue;

    public int RemovedCount => removedItems != null
        ? removedItems.Count
        : 0;

    public int AddedCount => addedItems != null
        ? addedItems.Count
        : 0;

    public float NetMarketValueChange =>
        addedMarketValue - removedMarketValue;

    public static InventoryTransactionResult Failed(string message)
    {
        return new InventoryTransactionResult
        {
            success = false,
            errorMessage = message ?? "Inventory transaction failed."
        };
    }

    public static InventoryTransactionResult Completed(
        List<InventoryItem> removed,
        List<InventoryItem> added,
        float removedValue,
        float addedValue)
    {
        return new InventoryTransactionResult
        {
            success = true,
            errorMessage = "",
            removedItems = removed ?? new List<InventoryItem>(),
            addedItems = added ?? new List<InventoryItem>(),
            removedMarketValue = removedValue,
            addedMarketValue = addedValue
        };
    }
}
