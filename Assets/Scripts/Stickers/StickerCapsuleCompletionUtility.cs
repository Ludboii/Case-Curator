using System.Collections.Generic;

/// <summary>
/// Sticker Capsules intentionally use one permanent discovery tier. Existing
/// ContainerProgressData.foundSkinKeys remains the source of truth, so selling
/// or applying a sticker never removes completion progress.
/// </summary>
public static class StickerCapsuleCompletionUtility
{
    public static bool IsStickerCapsule(CaseData container)
    {
        return container != null &&
               container.containerType == CaseContainerType.StickerCapsule;
    }

    public static int GetTargetCount(CaseData container)
    {
        if (!IsStickerCapsule(container) || container.dropPool == null)
            return 0;

        HashSet<string> unique = new HashSet<string>();

        for (int i = 0; i < container.dropPool.Count; i++)
        {
            WeightedDrop drop = container.dropPool[i];

            if (drop == null || !(drop.skin is StickerData sticker))
                continue;

            unique.Add(!string.IsNullOrWhiteSpace(sticker.apiId)
                ? sticker.apiId
                : sticker.DisplayName);
        }

        return unique.Count;
    }

    public static int GetFoundCount(CaseData container)
    {
        if (!IsStickerCapsule(container) ||
            ContainerProgressManager.Instance == null ||
            container.dropPool == null)
        {
            return 0;
        }

        HashSet<string> found = new HashSet<string>();

        for (int i = 0; i < container.dropPool.Count; i++)
        {
            WeightedDrop drop = container.dropPool[i];

            if (drop != null &&
                drop.skin is StickerData sticker &&
                ContainerProgressManager.Instance.HasFoundSkin(
                    container,
                    sticker))
            {
                found.Add(!string.IsNullOrWhiteSpace(sticker.apiId)
                    ? sticker.apiId
                    : sticker.DisplayName);
            }
        }

        return found.Count;
    }

    public static bool IsNormalComplete(CaseData container)
    {
        int target = GetTargetCount(container);
        return target > 0 && GetFoundCount(container) >= target;
    }

    public static string GetDisplayText(CaseData container)
    {
        return IsNormalComplete(container)
            ? "Normal Completion"
            : $"Found {GetFoundCount(container)} / {GetTargetCount(container)}";
    }
}
