using UnityEngine;

public class CaseTester : MonoBehaviour
{
    public CaseData caseToOpen;

    public void OpenCase()
{
    InventoryItem item = CaseOpener.OpenCase(caseToOpen);

    if (InventoryManager.Instance != null)
    {
        InventoryManager.Instance.AddItem(item);
    }
    else
    {
        Debug.LogWarning("No InventoryManager found in scene.");
    }

    float price = PriceCalculator.GetPrice(item);

    Debug.Log(
        $"{SkinDisplayUtility.GetDisplayName(item.skin)} | " +
        $"Vanilla: {item.isVanilla} | " +
        $"Float: {item.floatValue:F10} | " +
        $"Pattern: {item.patternId} | " +
        $"StatTrak: {item.statTrak} | " +
        $"Price: {price:F2}");
}
}