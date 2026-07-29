using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Existing container progress stores Bronze/Silver/Gold/Diamond fields for all
/// CaseData assets. Sticker Capsules intentionally use only permanent unique
/// discovery (presented as Normal Completion), so wear/float/variant progress is
/// cleared whenever capsule progress changes or a save is loaded.
/// </summary>
public sealed class StickerCapsuleProgressSanitizer : MonoBehaviour
{
    private ContainerProgressManager progressManager;
    private bool subscribed;
    private bool sanitizing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<StickerCapsuleProgressSanitizer>() != null)
            return;

        GameObject go = new GameObject("StickerCapsuleProgressSanitizer");
        DontDestroyOnLoad(go);
        go.AddComponent<StickerCapsuleProgressSanitizer>();
    }

    private void Update()
    {
        if (!subscribed)
            TrySubscribe();
    }

    private void TrySubscribe()
    {
        progressManager = ContainerProgressManager.Instance != null
            ? ContainerProgressManager.Instance
            : FindFirstObjectByType<ContainerProgressManager>();

        if (progressManager == null)
            return;

        progressManager.OnContainerProgressChanged -= HandleProgressChanged;
        progressManager.OnContainerProgressChanged += HandleProgressChanged;
        subscribed = true;
        SanitizeAll();
    }

    private void HandleProgressChanged()
    {
        SanitizeAll();
    }

    private void SanitizeAll()
    {
        if (sanitizing || progressManager == null ||
            SaveManager.Instance == null || SaveManager.Instance.database == null)
        {
            return;
        }

        sanitizing = true;
        bool changed = false;

        try
        {
            List<CaseData> containers = SaveManager.Instance.database.allCases;

            if (containers == null)
                return;

            for (int i = 0; i < containers.Count; i++)
            {
                CaseData container = containers[i];

                if (!StickerCapsuleCompletionUtility.IsStickerCapsule(container))
                    continue;

                ContainerProgressData progress =
                    progressManager.GetProgress(container);

                if (progress == null)
                    continue;

                changed |= Clear(progress.bestWearSkinKeys);
                changed |= Clear(progress.variantSkinKeys);
                changed |= Clear(progress.bestWearVariantSkinKeys);
                changed |= Clear(progress.bestWearStatTrakSkinKeys);
                changed |= Clear(progress.topQuarterHighestWearSkinKeys);
                changed |= Clear(progress.topQuarterHighestWearStatTrakSkinKeys);
                changed |= Clear(progress.priceDiscoveries);
                changed |= Clear(progress.observedFloatRanges);
                progress.foundRareSpecial = false;
                changed |= Clear(progress.foundRareSpecialSkinKeys);
            }

            if (changed)
                SaveManager.Instance.MarkDirty();
        }
        finally
        {
            sanitizing = false;
        }
    }

    private static bool Clear<T>(List<T> values)
    {
        if (values == null || values.Count == 0)
            return false;

        values.Clear();
        return true;
    }

    private void OnDestroy()
    {
        if (subscribed && progressManager != null)
        {
            progressManager.OnContainerProgressChanged -= HandleProgressChanged;
        }
    }
}
