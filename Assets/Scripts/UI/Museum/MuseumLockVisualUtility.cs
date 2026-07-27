using UnityEngine;

/// <summary>
/// Applies a consistent greyed-out state to locked Museum browser cards without
/// requiring every prefab to contain a dedicated overlay.
/// </summary>
public static class MuseumLockVisualUtility
{
    public static void Apply(
        GameObject target,
        bool unlocked,
        float lockedAlpha = 0.45f)
    {
        if (target == null)
            return;

        CanvasGroup group = target.GetComponent<CanvasGroup>();

        if (group == null)
            group = target.AddComponent<CanvasGroup>();

        group.alpha = unlocked ? 1f : Mathf.Clamp01(lockedAlpha);
        group.interactable = unlocked;
        group.blocksRaycasts = unlocked;
    }
}
