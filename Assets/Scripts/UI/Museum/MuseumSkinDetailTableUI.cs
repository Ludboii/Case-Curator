using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Temporary M2 presenter that replaces the compact slot summary with an aligned
/// Wear / Normal / StatTrak / Souvenir table. Attach this beside MuseumPanelUI.
/// M3 can call Format directly from the final exhibit popup.
/// </summary>
[RequireComponent(typeof(MuseumPanelUI))]
public class MuseumSkinDetailTableUI : MonoBehaviour
{
    [SerializeField] private MuseumPanelUI museumPanel;
    [SerializeField] private TMP_Text targetText;

    [Header("Column Positions")]
    [Range(0f, 100f)] [SerializeField] private float wearColumn = 0f;
    [Range(0f, 100f)] [SerializeField] private float normalColumn = 24f;
    [Range(0f, 100f)] [SerializeField] private float statTrakColumn = 55f;
    [Range(0f, 100f)] [SerializeField] private float souvenirColumn = 82f;

    [Header("Marks")]
    [Tooltip("ASCII V is used instead of a Unicode checkmark so every TMP font can display it.")]
    [SerializeField] private string collectedMark = "V";
    [SerializeField] private string missingMark = "X";
    [SerializeField] private string unavailableMark = "-";
    [SerializeField] private Color collectedColor = new Color(0.3f, 1f, 0.35f, 1f);
    [SerializeField] private Color missingColor = new Color(1f, 0.4f, 0.4f, 1f);
    [SerializeField] private Color unavailableColor = new Color(0.65f, 0.65f, 0.65f, 1f);

    private FieldInfo currentSkinField;
    private FieldInfo detailSlotsTextField;
    private MuseumSkinEntry lastEntry;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshNow();
    }

    private void LateUpdate()
    {
        MuseumSkinEntry current = GetCurrentEntry();

        if (current == lastEntry)
            return;

        lastEntry = current;
        RefreshNow();
    }

    [ContextMenu("Refresh Slot Table")]
    public void RefreshNow()
    {
        ResolveReferences();

        MuseumSkinEntry entry = GetCurrentEntry();

        if (targetText == null || entry == null)
            return;

        targetText.richText = true;
        targetText.text = Format(entry);
    }

    public string Format(MuseumSkinEntry entry)
    {
        if (entry == null || entry.slots == null || entry.slots.Count == 0)
            return "No Museum slots are available for this skin.";

        bool showNormal = HasVariant(entry, MuseumDonationVariant.Normal);
        bool showStatTrak = HasVariant(entry, MuseumDonationVariant.StatTrak);
        bool showSouvenir = HasVariant(entry, MuseumDonationVariant.Souvenir);

        StringBuilder builder = new StringBuilder();
        builder.Append("<b>");
        AppendAt(builder, wearColumn, "Wear");

        if (showNormal)
            AppendAt(builder, normalColumn, "Normal");

        if (showStatTrak)
            AppendAt(builder, statTrakColumn, "StatTrak");

        if (showSouvenir)
            AppendAt(builder, souvenirColumn, "Souvenir");

        builder.AppendLine("</b>");

        int[] wearOrder = { -1, 0, 1, 2, 3, 4 };

        for (int i = 0; i < wearOrder.Length; i++)
        {
            int wearIndex = wearOrder[i];

            if (!HasSlotForWear(entry, wearIndex))
                continue;

            AppendAt(
                builder,
                wearColumn,
                wearIndex < 0 ? "Vanilla" : GetWearAbbreviation(wearIndex));

            if (showNormal)
                AppendStatus(builder, entry, wearIndex, MuseumDonationVariant.Normal, normalColumn);

            if (showStatTrak)
                AppendStatus(builder, entry, wearIndex, MuseumDonationVariant.StatTrak, statTrakColumn);

            if (showSouvenir)
                AppendStatus(builder, entry, wearIndex, MuseumDonationVariant.Souvenir, souvenirColumn);

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private void AppendStatus(
        StringBuilder builder,
        MuseumSkinEntry entry,
        int wearIndex,
        MuseumDonationVariant variant,
        float column)
    {
        MuseumSlotEntry slot = FindSlot(entry, wearIndex, variant);

        if (slot == null)
        {
            AppendColoredAt(builder, column, unavailableMark, unavailableColor);
            return;
        }

        AppendColoredAt(
            builder,
            column,
            slot.donated ? collectedMark : missingMark,
            slot.donated ? collectedColor : missingColor);
    }

    private static MuseumSlotEntry FindSlot(
        MuseumSkinEntry entry,
        int wearIndex,
        MuseumDonationVariant variant)
    {
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

    private static bool HasVariant(
        MuseumSkinEntry entry,
        MuseumDonationVariant variant)
    {
        for (int i = 0; i < entry.slots.Count; i++)
        {
            if (entry.slots[i] != null && entry.slots[i].variant == variant)
                return true;
        }

        return false;
    }

    private static bool HasSlotForWear(MuseumSkinEntry entry, int wearIndex)
    {
        for (int i = 0; i < entry.slots.Count; i++)
        {
            if (entry.slots[i] != null && entry.slots[i].wearIndex == wearIndex)
                return true;
        }

        return false;
    }

    private static string GetWearAbbreviation(int wearIndex)
    {
        switch (wearIndex)
        {
            case 0: return "FN";
            case 1: return "MW";
            case 2: return "FT";
            case 3: return "WW";
            default: return "BS";
        }
    }

    private static void AppendAt(StringBuilder builder, float positionPercent, string value)
    {
        builder.Append($"<pos={positionPercent:0.#}%>{value}");
    }

    private static void AppendColoredAt(
        StringBuilder builder,
        float positionPercent,
        string value,
        Color color)
    {
        builder.Append(
            $"<pos={positionPercent:0.#}%><color=#{ColorUtility.ToHtmlStringRGB(color)}>{value}</color>");
    }

    private MuseumSkinEntry GetCurrentEntry()
    {
        if (museumPanel == null || currentSkinField == null)
            return null;

        return currentSkinField.GetValue(museumPanel) as MuseumSkinEntry;
    }

    private void ResolveReferences()
    {
        if (museumPanel == null)
            museumPanel = GetComponent<MuseumPanelUI>();

        if (museumPanel == null)
            return;

        if (currentSkinField == null)
        {
            currentSkinField = typeof(MuseumPanelUI).GetField(
                "currentSkin",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        if (targetText == null)
        {
            if (detailSlotsTextField == null)
            {
                detailSlotsTextField = typeof(MuseumPanelUI).GetField(
                    "detailSlotsText",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            }

            if (detailSlotsTextField != null)
                targetText = detailSlotsTextField.GetValue(museumPanel) as TMP_Text;
        }
    }
}
