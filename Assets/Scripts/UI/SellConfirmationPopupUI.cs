using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SellConfirmationPopupUI : MonoBehaviour
{
    public static SellConfirmationPopupUI Instance { get; private set; }

    [Header("Root")]
    public GameObject popupRoot;

    [Header("Text")]
    public TMP_Text titleText;
    public TMP_Text messageText;
    public TMP_Text confirmButtonText;
    public TMP_Text cancelButtonText;

    [Header("Buttons")]
    public Button confirmButton;
    public Button cancelButton;

    private Action onConfirmAction;

    private void Awake()
    {
        Instance = this;

        if (popupRoot == null)
            popupRoot = gameObject;

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(Confirm);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(Cancel);
        }

        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Show(
        string title,
        string message,
        string confirmText,
        string cancelText,
        Action onConfirm)
    {
        onConfirmAction = onConfirm;

        if (titleText != null)
            titleText.text = title;

        if (messageText != null)
            messageText.text = message;

        if (confirmButtonText != null)
            confirmButtonText.text = confirmText;

        if (cancelButtonText != null)
            cancelButtonText.text = cancelText;

        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
            popupRoot.transform.SetAsLastSibling();
        }
    }

    public void Hide()
    {
        onConfirmAction = null;

        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    private void Confirm()
    {
        Action action = onConfirmAction;

        Hide();

        action?.Invoke();
    }

    private void Cancel()
    {
        Hide();
    }
}