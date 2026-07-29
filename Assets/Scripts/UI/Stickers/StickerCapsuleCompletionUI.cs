using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optional companion for CaseInspectUI. Sticker Capsules display only their
/// silver-coloured Normal Completion and do not open the weapon-case multi-tier
/// completion popup.
/// </summary>
public sealed class StickerCapsuleCompletionUI : MonoBehaviour
{
    [SerializeField] private CaseInspectUI caseInspectUI;
    [SerializeField] private TMP_Text completionText;
    [SerializeField] private Button completionButton;
    [SerializeField] private Color incompleteColor = Color.white;
    [SerializeField] private Color completeSilverColor =
        new Color(0.78f, 0.82f, 0.88f, 1f);

    private float nextRefreshAt;
    private bool disabledCompletionButton;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Refresh();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshAt)
            return;

        nextRefreshAt = Time.unscaledTime + 0.2f;
        Refresh();
    }

    private void OnDisable()
    {
        RestoreCompletionButton();
    }

    private void Refresh()
    {
        if (caseInspectUI == null)
            return;

        CaseData container = caseInspectUI.CurrentCase;
        bool stickerCapsule =
            StickerCapsuleCompletionUtility.IsStickerCapsule(container);

        if (!stickerCapsule)
        {
            RestoreCompletionButton();
            return;
        }

        bool complete =
            StickerCapsuleCompletionUtility.IsNormalComplete(container);

        if (completionText != null)
        {
            completionText.text =
                StickerCapsuleCompletionUtility.GetDisplayText(container);
            completionText.color = complete
                ? completeSilverColor
                : incompleteColor;
        }

        if (completionButton != null)
        {
            completionButton.interactable = false;
            disabledCompletionButton = true;
        }
    }

    private void RestoreCompletionButton()
    {
        if (disabledCompletionButton && completionButton != null)
            completionButton.interactable = true;

        disabledCompletionButton = false;
    }

    private void ResolveReferences()
    {
        if (caseInspectUI == null)
            caseInspectUI = GetComponentInParent<CaseInspectUI>(true);
        if (completionText == null && caseInspectUI != null)
            completionText = caseInspectUI.completionText;
        if (completionButton == null && caseInspectUI != null)
            completionButton = caseInspectUI.completionButton;
    }
}
