using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum InventoryRarityFilterButtonMode
{
    Only,
    Hide
}

/// <summary>
/// Visual state for one rarity-filter button. The rarity colour is preserved
/// exactly; active state is shown with an outline and optional marker instead
/// of replacing the button colour with Unity's selected/pressed tint.
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

    [Header("Active Appearance")]
    [SerializeField] private Color onlyOutlineColor = Color.white;
    [SerializeField] private Color hideOutlineColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private Vector2 outlineDistance = new Vector2(3f, -3f);
    [SerializeField] private bool useGraphicAlpha = true;
    [SerializeField] private string onlyMarker = "✓";
    [SerializeField] private string hideMarker = "×";

    private Outline activeOutline;
    private Color exactRarityColor = Color.white;

    public Button Button => button;
    public bool IsActive { get; private set; }

    private void Reset()
    {
        button = GetComponent<Button>();

        if (button != null)
            rarityBackground = button.targetGraphic;
    }

    private void Awake()
    {
        ResolveReferences();
        CacheExactRarityColour();
        DisableUnityColourTinting();
        EnsureOutline();
        SetActive(false);
    }

    public void SetActive(bool active)
    {
        ResolveReferences();
        IsActive = active;

        if (rarityBackground != null)
            rarityBackground.color = exactRarityColor;

        if (activeOutline != null)
        {
            activeOutline.enabled = active;
            activeOutline.effectColor = mode == InventoryRarityFilterButtonMode.Only
                ? onlyOutlineColor
                : hideOutlineColor;
            activeOutline.effectDistance = outlineDistance;
            activeOutline.useGraphicAlpha = useGraphicAlpha;
        }

        if (activeIndicator != null)
            activeIndicator.SetActive(active);

        if (activeIndicatorText != null)
        {
            activeIndicatorText.text = mode == InventoryRarityFilterButtonMode.Only
                ? onlyMarker
                : hideMarker;
            activeIndicatorText.gameObject.SetActive(active);
        }
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

    private void CacheExactRarityColour()
    {
        if (rarityBackground != null)
            exactRarityColor = rarityBackground.color;
    }

    private void DisableUnityColourTinting()
    {
        if (button == null)
            return;

        // Unity's Color Tint transition caused the grey idle colour, green
        // click flash and black selected state. None keeps the authored rarity
        // colour unchanged in every EventSystem state.
        button.transition = Selectable.Transition.None;
    }

    private void EnsureOutline()
    {
        if (rarityBackground == null)
            return;

        activeOutline = rarityBackground.GetComponent<Outline>();

        if (activeOutline == null)
            activeOutline = rarityBackground.gameObject.AddComponent<Outline>();

        activeOutline.enabled = false;
    }
}
