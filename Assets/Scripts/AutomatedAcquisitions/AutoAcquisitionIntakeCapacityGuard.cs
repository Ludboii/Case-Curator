using UnityEngine;

/// <summary>
/// Enforces the Intake Vault as a hard production cap. When the final available
/// Intake slot is filled, every active processing line is stopped immediately,
/// its due timestamp is cleared, and no offline/overdue processing backlog is
/// retained. Lines must be started manually again after items are claimed.
/// </summary>
public sealed class AutoAcquisitionIntakeCapacityGuard : MonoBehaviour
{
    private AutoAcquisitionService service;
    private float nextCheckAt;
    private bool enforcing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<AutoAcquisitionIntakeCapacityGuard>() != null)
            return;

        GameObject go = new GameObject("AutoAcquisitionIntakeCapacityGuard");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoAcquisitionIntakeCapacityGuard>();
    }

    private void Update()
    {
        if (service == null)
            BindService();

        if (service == null || Time.unscaledTime < nextCheckAt)
            return;

        nextCheckAt = Time.unscaledTime + 0.1f;
        EnforceCapacity();
    }

    private void BindService()
    {
        AutoAcquisitionService candidate =
            AutoAcquisitionService.Instance != null
                ? AutoAcquisitionService.Instance
                : FindFirstObjectByType<AutoAcquisitionService>();

        if (candidate == null || candidate == service)
            return;

        UnbindService();
        service = candidate;
        service.OnItemProcessed += HandleItemProcessed;
        EnforceCapacity();
    }

    private void UnbindService()
    {
        if (service != null)
            service.OnItemProcessed -= HandleItemProcessed;

        service = null;
    }

    private void HandleItemProcessed(
        AutoAcquisitionPendingItemSaveData pending)
    {
        // This event is raised before AutoAcquisitionService's processing loop
        // advances to another overdue completion, so stopping here prevents a
        // hidden queue from being generated behind a full Intake Vault.
        EnforceCapacity();
    }

    private void EnforceCapacity()
    {
        if (enforcing || service == null)
            return;

        AutoAcquisitionWingSnapshot snapshot = service.GetSnapshot(false);

        if (snapshot == null ||
            snapshot.intakeCapacity <= 0 ||
            snapshot.intakeCount < snapshot.intakeCapacity)
        {
            return;
        }

        enforcing = true;
        bool changed = false;

        try
        {
            int lineCount = Mathf.Clamp(snapshot.lineCount, 1, 3);

            for (int i = 0; i < lineCount; i++)
            {
                AutoAcquisitionLineSaveData line = service.GetLine(i);

                if (line == null || !line.active)
                    continue;

                // Clear the timestamp instead of retaining an overdue completion.
                // Claiming an item therefore cannot reveal a hidden production
                // buffer; the player must explicitly restart the line.
                line.active = false;
                line.nextCompletionUtcTicks = 0L;
                line.pauseReason = "Stopped: Intake Vault full.";
                changed = true;
            }

            if (changed && SaveManager.Instance != null)
                SaveManager.Instance.MarkDirty();
        }
        finally
        {
            enforcing = false;
        }
    }

    private void OnDestroy()
    {
        UnbindService();
    }
}
