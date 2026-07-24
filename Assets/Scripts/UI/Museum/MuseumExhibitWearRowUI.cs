using TMPro;
using UnityEngine;

/// <summary>
/// One aligned wear row: Wear | Normal | StatTrak | Souvenir.
/// </summary>
public class MuseumExhibitWearRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text wearText;
    [SerializeField] private MuseumExhibitSlotUI normalSlot;
    [SerializeField] private MuseumExhibitSlotUI statTrakSlot;
    [SerializeField] private MuseumExhibitSlotUI souvenirSlot;

    public void Setup(
        MuseumSkinEntry entry,
        int wearIndex,
        MuseumPanelUI owner,
        MuseumService service)
    {
        if (wearText != null)
            wearText.text = GetWearLabel(wearIndex);

        SetupCell(
            normalSlot,
            FindSlot(entry, wearIndex, MuseumDonationVariant.Normal),
            owner,
            service);
        SetupCell(
            statTrakSlot,
            FindSlot(entry, wearIndex, MuseumDonationVariant.StatTrak),
            owner,
            service);
        SetupCell(
            souvenirSlot,
            FindSlot(entry, wearIndex, MuseumDonationVariant.Souvenir),
            owner,
            service);
    }

    private static void SetupCell(
        MuseumExhibitSlotUI cell,
        MuseumSlotEntry slot,
        MuseumPanelUI owner,
        MuseumService service)
    {
        if (cell == null)
            return;

        if (slot != null)
            cell.Setup(slot, owner, service);
        else
            cell.SetupUnavailable();
    }

    private static MuseumSlotEntry FindSlot(
        MuseumSkinEntry entry,
        int wearIndex,
        MuseumDonationVariant variant)
    {
        if (entry == null || entry.slots == null)
            return null;

        for (int i = 0; i < entry.slots.Count; i++)
        {
            MuseumSlotEntry slot = entry.slots[i];

            if (slot != null &&
                slot.wearIndex == wearIndex &&
                slot.variant == variant)
            {
                return slot;
            }
        }

        return null;
    }

    private static string GetWearLabel(int wearIndex)
    {
        switch (wearIndex)
        {
            case -1: return "Vanilla";
            case 0: return "FN";
            case 1: return "MW";
            case 2: return "FT";
            case 3: return "WW";
            default: return "BS";
        }
    }
}
