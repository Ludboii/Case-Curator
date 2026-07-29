using UnityEngine;

/// <summary>
/// Sticker Capsules use the requested three-at-once manual limit. A future or
/// existing X-ray upgrade can raise only Sticker Capsules to ten by using the
/// stable upgrade ID below; no capsule is ever routed into automation.
/// </summary>
public static class StickerCapsuleOpeningRules
{
    public const string XRayUpgradeId = "xray-scanner";
    public const int NormalMaximum = 3;
    public const int XRayMaximum = 10;

    public static bool IsStickerCapsule(CaseData container)
    {
        return container != null &&
               container.containerType == CaseContainerType.StickerCapsule;
    }

    public static bool HasXRay()
    {
        UpgradeService service = UpgradeService.Instance != null
            ? UpgradeService.Instance
            : Object.FindFirstObjectByType<UpgradeService>();

        return service != null && service.GetLevel(XRayUpgradeId) > 0;
    }

    public static int GetMaximum(CaseData container)
    {
        if (!IsStickerCapsule(container))
            return int.MaxValue;

        return HasXRay() ? XRayMaximum : NormalMaximum;
    }

    public static int ClampOpenAmount(CaseData container, int requested)
    {
        return Mathf.Clamp(requested, 0, GetMaximum(container));
    }
}
