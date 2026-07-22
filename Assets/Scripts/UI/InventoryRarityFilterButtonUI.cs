using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum InventoryRarityFilterButtonMode
{
    Only,
    Hide
}

/// <summary>
/// Visual state for one rarity-filter button. The authored rarity colour is
/// never replaced by Unity's button-state tint. Active state is represented by
/// an outline and optional marker instead.
/// </summary>
[DisallowMultipleComponent]
public class InventoryRarityFilterButtonUI : MonoBehaviour
{
    [Header("Filter")]
    public Rarity rarity;
    public InventoryRarityFilterButtonMode mode;

    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Graphic rarityBackground;
    [SerializeField] private GameObject activeIndicator;
    [SerializeField] private TMP_Text activeIndicatorText;

    [Header("Rarity Colour")]
    [Tooltip(
        "Uses the central RarityColorUtility value instead of caching the " +
        "button's current Inspector/EventSystem tint. Keep this enabled for " +
        "the standard inventory rarity buttons.")]
    [SerializeField] private bool useRarityUtilityColor = true;

    [SerializeField, Range(0f, 1f)] private float rarityColorAlpha = 1f;

    [Header("Active Appearance")]
    [SerializeField] private Color onlyOutlineColor = Color.white;
    [SerializeField] private Color hideOutlineColor =
        new Color(1f, 0.25f, 0.25f, 1f);

    [SerializeField] private Vector2 outlineDistance =
        new Vector2(3f, -3f);

    [SerializeField] private bool useGraphicAlpha = true;
    [SerializeField] private string onlyMarker = "✓";
    [SerializeField] private string hideMarker = "×";

    private Outline activeOutline;
    private Color exactRarityColor = Color.white;

    public Button Button => button;
    public bool IsActive { get; private set; }

    private void Reset()
    {
        ResolveReferences();
        RefreshExactRarityColor();
        ApplyExactRarityColor();
    }

    private void Awake()
    {
        ResolveReferences();
        RefreshExactRarityColor();
        DisableUnityColourTinting();
        EnsureOutline();
        SetActive(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshExactRarityColor();
        DisableUnityColourTinting();
        ApplyExactRarityColor();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ResolveReferences();
        RefreshExactRarityColor();
        ApplyExactRarityColor();
    }
#endif

    public void SetActive(bool active)
    {
        ResolveReferences();
        RefreshExactRarityColor();
        IsActive = active;
        ApplyExactRarityColor();

        if (activeOutline != null)
        {
            activeOutline.enabled = active;
            activeOutline.effectColor =
                mode == InventoryRarityFilterButtonMode.Only
                    ? onlyOutlineColor
                    : hideOutlineColor;

            activeOutline.effectDistance = outlineDistance;
            activeOutline.useGraphicAlpha = useGraphicAlpha;
        }

        if (activeIndicator != null)
            activeIndicator.SetActive(active);

        if (activeIndicatorText != null)
        {
            activeIndicatorText.text =
                mode == InventoryRarityFilterButtonMode.Only
                    ? onlyMarker
                    : hideMarker;

            activeIndicatorText.gameObject.SetActive(active);
        }
    }

    [ContextMenu("Refresh Rarity Colour")]
    public void RefreshRarityColourNow()
    {
        ResolveReferences();
        RefreshExactRarityColor();
        DisableUnityColourTinting();
        ApplyExactRarityColor();
    }

    private void ResolveReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (rarityBackground == null && button != null)
            rarityBackground = button.targetGraphic;

        if (rarityBackground == null)
            rarityBackground = GetComponent<Graphic>();
    }

    private void RefreshExactRarityColor()
    {
        if (useRarityUtilityColor)
        {
            exactRarityColor = RarityColorUtility.GetColor(rarity);
        }
        else if (rarityBackground != null)
        {
            exactRarityColor = rarityBackground.color;
        }

        exactRarityColor.a = rarityColorAlpha;
    }

    private void ApplyExactRarityColor()
    {
        if (rarityBackground != null)
            rarityBackground.color = exactRarityColor;
    }

    private void DisableUnityColourTinting()
    {
        if (button == null)
            return;

        button.transition = Selectable.Transition.None;

        if (button.targetGraphic == rarityBackground &&
            button.targetGraphic != null)
        {
            button.targetGraphic.color = exactRarityColor;
        }
    }

    private void EnsureOutline()
    {
        if (rarityBackground == null)
            return;

        activeOutline = rarityBackground.GetComponent<Outline>();

        if (activeOutline == null)
        {
            activeOutline =
                rarityBackground.gameObject.AddComponent<Outline>();
        }

        activeOutline.enabled = false;
    }
}
