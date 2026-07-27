using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable full-card overlay shown when a completed Museum skin, weapon or
/// category has an unclaimed one-time MP reward.
/// </summary>
public sealed class MuseumCompletionClaimOverlayUI : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Button claimButton;
    [SerializeField] private TMP_Text claimText;

    private Action claimAction;

    private void Awake()
    {
        ResolveReferences();
        RegisterButton();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    public void Setup(
        MuseumCompletionRewardPreview preview,
        Action onClaim)
    {
        ResolveReferences();
        RegisterButton();

        claimAction = onClaim;
        bool visible = preview != null && preview.CanClaim;

        if (claimText != null)
        {
            claimText.text = visible
                ? $"CLAIM REWARD\n{preview.rewardMuseumPoints:N0} MP"
                : "";
        }

        if (claimButton != null)
            claimButton.interactable = visible;

        if (root != null)
            root.SetActive(visible);
        else
            gameObject.SetActive(visible);
    }

    public void Hide()
    {
        claimAction = null;

        if (root != null)
            root.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    private void HandleClaimClicked()
    {
        claimAction?.Invoke();
    }

    private void RegisterButton()
    {
        if (claimButton == null)
            return;

        claimButton.onClick.RemoveListener(HandleClaimClicked);
        claimButton.onClick.AddListener(HandleClaimClicked);
    }

    private void ResolveReferences()
    {
        if (root == null)
            root = gameObject;

        if (claimButton == null)
            claimButton = GetComponent<Button>();

        if (claimButton == null)
            claimButton = GetComponentInChildren<Button>(true);

        if (claimText == null)
            claimText = GetComponentInChildren<TMP_Text>(true);
    }

    private void OnDestroy()
    {
        if (claimButton != null)
            claimButton.onClick.RemoveListener(HandleClaimClicked);
    }
}
