using System;
using System.Collections.Generic;

/// <summary>
/// Cached aggregate view over ContainerProgressManager. It derives totals from
/// the existing per-container progress records, so no duplicate save state is
/// introduced for case, collection, souvenir or sticker opening counts.
/// </summary>
public static class OpenableStatisticsService
{
    public static event Action OnStatisticsChanged;

    private static readonly Dictionary<string, int> openedByContainerId =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<CaseContainerType, int> openedByType =
        new Dictionary<CaseContainerType, int>();

    private static ContainerProgressManager subscribedManager;
    private static bool cacheDirty = true;
    private static int totalOpened;

    public static int GetCount(
        OpenableProgressType progressType,
        CaseData specificContainer = null)
    {
        EnsureCache();

        switch (progressType)
        {
            case OpenableProgressType.WeaponCases:
                return GetOpenedCountByType(CaseContainerType.WeaponCase);

            case OpenableProgressType.CollectionPackages:
                return GetOpenedCountByType(
                    CaseContainerType.CollectionPackage);

            case OpenableProgressType.SouvenirPackages:
                return GetOpenedCountByType(
                    CaseContainerType.SouvenirPackage);

            case OpenableProgressType.StickerCapsules:
                return GetOpenedCountByType(
                    CaseContainerType.StickerCapsule);

            case OpenableProgressType.SpecificContainer:
                return GetOpenedCount(specificContainer);

            default:
                return totalOpened;
        }
    }

    public static int GetTotalOpenedCount()
    {
        EnsureCache();
        return totalOpened;
    }

    public static int GetOpenedCountByType(
        CaseContainerType containerType)
    {
        EnsureCache();

        return openedByType.TryGetValue(
            containerType,
            out int count)
                ? count
                : 0;
    }

    public static int GetOpenedCount(CaseData container)
    {
        if (container == null)
            return 0;

        EnsureCache();
        string id = GetContainerId(container);

        return !string.IsNullOrWhiteSpace(id) &&
               openedByContainerId.TryGetValue(id, out int count)
            ? count
            : 0;
    }

    public static void Invalidate()
    {
        cacheDirty = true;
        OnStatisticsChanged?.Invoke();
    }

    private static void EnsureCache()
    {
        EnsureSubscription();

        if (!cacheDirty)
            return;

        RebuildCache();
    }

    private static void EnsureSubscription()
    {
        ContainerProgressManager current =
            ContainerProgressManager.Instance;

        if (ReferenceEquals(current, subscribedManager))
            return;

        if (subscribedManager != null)
        {
            subscribedManager.OnContainerProgressChanged -=
                HandleContainerProgressChanged;
        }

        subscribedManager = current;

        if (subscribedManager != null)
        {
            subscribedManager.OnContainerProgressChanged +=
                HandleContainerProgressChanged;
        }

        cacheDirty = true;
    }

    private static void HandleContainerProgressChanged()
    {
        cacheDirty = true;
        OnStatisticsChanged?.Invoke();
    }

    private static void RebuildCache()
    {
        openedByContainerId.Clear();
        openedByType.Clear();
        totalOpened = 0;

        ContainerProgressManager progressManager =
            ContainerProgressManager.Instance;

        if (progressManager == null)
        {
            cacheDirty = false;
            return;
        }

        ContainerProgressSaveData state =
            progressManager.ExportSaveData();

        if (state == null || state.progressEntries == null)
        {
            cacheDirty = false;
            return;
        }

        Dictionary<string, CaseData> containersById =
            BuildContainerLookup();

        for (int i = 0; i < state.progressEntries.Count; i++)
        {
            ContainerProgressData entry = state.progressEntries[i];

            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.containerId))
            {
                continue;
            }

            int opened = Math.Max(0, entry.openedCount);
            string id = entry.containerId.Trim();

            if (openedByContainerId.ContainsKey(id))
                openedByContainerId[id] += opened;
            else
                openedByContainerId.Add(id, opened);

            totalOpened += opened;

            if (!containersById.TryGetValue(id, out CaseData container) ||
                container == null)
            {
                continue;
            }

            CaseContainerType type = container.containerType;

            if (openedByType.ContainsKey(type))
                openedByType[type] += opened;
            else
                openedByType.Add(type, opened);
        }

        cacheDirty = false;
    }

    private static Dictionary<string, CaseData> BuildContainerLookup()
    {
        Dictionary<string, CaseData> result =
            new Dictionary<string, CaseData>(
                StringComparer.OrdinalIgnoreCase);

        GameDatabase database = SaveManager.Instance != null
            ? SaveManager.Instance.database
            : null;

        if (database == null || database.allCases == null)
            return result;

        for (int i = 0; i < database.allCases.Count; i++)
        {
            CaseData container = database.allCases[i];

            if (container == null)
                continue;

            string id = GetContainerId(container);

            if (!string.IsNullOrWhiteSpace(id) &&
                !result.ContainsKey(id))
            {
                result.Add(id, container);
            }
        }

        return result;
    }

    private static string GetContainerId(CaseData container)
    {
        if (container == null)
            return "";

        return !string.IsNullOrWhiteSpace(container.apiId)
            ? container.apiId.Trim()
            : container.caseName != null
                ? container.caseName.Trim()
                : "";
    }
}
