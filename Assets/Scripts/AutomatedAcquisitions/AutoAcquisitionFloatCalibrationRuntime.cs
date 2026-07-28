using UnityEngine;

/// <summary>
/// Applies the Float Calibration curve to newly processed Intake Vault items.
/// This is kept outside AutoAcquisitionService so preview and runtime balancing
/// can evolve without expanding the core processing service.
/// </summary>
public sealed class AutoAcquisitionFloatCalibrationRuntime : MonoBehaviour
{
    private AutoAcquisitionService service;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<AutoAcquisitionFloatCalibrationRuntime>() != null)
            return;

        GameObject go = new GameObject("AutoAcquisitionFloatCalibrationRuntime");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoAcquisitionFloatCalibrationRuntime>();
    }

    private void Update()
    {
        if (service == null)
            Bind();
    }

    private void Bind()
    {
        AutoAcquisitionService candidate =
            AutoAcquisitionService.Instance != null
                ? AutoAcquisitionService.Instance
                : FindFirstObjectByType<AutoAcquisitionService>();

        if (candidate == null || candidate == service)
            return;

        Unbind();
        service = candidate;
        service.OnItemProcessed += HandleItemProcessed;
    }

    private void Unbind()
    {
        if (service != null)
            service.OnItemProcessed -= HandleItemProcessed;

        service = null;
    }

    private void HandleItemProcessed(
        AutoAcquisitionPendingItemSaveData pending)
    {
        if (pending == null || pending.item == null || service == null)
            return;

        InventoryItem runtime =
            AutoAcquisitionItemSerializationUtility.ToRuntimeItem(
                pending.item,
                service.Database);

        if (runtime == null || runtime.skin == null || runtime.skin.isVanilla)
            return;

        runtime.floatValue = AutoAcquisitionPreviewUtility.ApplyAutomatedFloatBias(
            runtime.skin,
            runtime.floatValue);
        runtime.marketValue = PriceCalculator.GetPrice(runtime);

        InventoryItemSaveData calibrated =
            AutoAcquisitionItemSerializationUtility.ToSaveData(runtime);

        if (calibrated != null)
            pending.item = calibrated;

        if (SaveManager.Instance != null)
            SaveManager.Instance.MarkDirty();
    }

    private void OnDestroy()
    {
        Unbind();
    }
}
