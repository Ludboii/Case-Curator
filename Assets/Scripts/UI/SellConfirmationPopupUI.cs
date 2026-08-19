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

    [Header("Modal Layer")]
    [Tooltip(
        "Keeps confirmations above other full-screen UI such as the sticker " +
        "picker. This is applied with a nested Canvas at runtime.")]
    public int modalSortingOrder = 32000;

    private Action onConfirmAction;
    private Action onCancelAction;
    private Canvas modalCanvas;

    private void Awake()
    {
        Instance = this;

        if (popupRoot == null)
            popupRoot = gameObject;

        EnsureButtonWiring();
        EnsureModalLayer();
        Hide();
    }

    private void OnEnable()
    {
        EnsureButtonWiring();
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
        Show(
            title,
            message,
            confirmText,
            cancelText,
            onConfirm,
            null);
    }

    public void Show(
        string title,
        string message,
        string confirmText,
        string cancelText,
        Action onConfirm,
        Action onCancel)
    {
        onConfirmAction = onConfirm;
        onCancelAction = onCancel;

        EnsureButtonWiring();
        EnsureModalLayer();

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
            EnsureModalLayer();
        }
    }

    public void Hide()
    {
        onConfirmAction = null;
        onCancelAction = null;

        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    private void Confirm()
    {
        Action action = onConfirmAction;
        onConfirmAction = null;
        onCancelAction = null;

        if (popupRoot != null)
            popupRoot.SetActive(false);

        action?.Invoke();
    }

    private void Cancel()
    {
        Action action = onCancelAction;
        onConfirmAction = null;
        onCancelAction = null;

        if (popupRoot != null)
            popupRoot.SetActive(false);

        action?.Invoke();
    }

    private void EnsureButtonWiring()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(Confirm);
            confirmButton.onClick.AddListener(Confirm);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(Cancel);
            cancelButton.onClick.AddListener(Cancel);
        }
    }

    private void EnsureModalLayer()
    {
        if (popupRoot == null)
            return;

        modalCanvas = popupRoot.GetComponent<Canvas>();

        if (modalCanvas == null)
            modalCanvas = popupRoot.AddComponent<Canvas>();

        modalCanvas.overrideSorting = true;
        modalCanvas.sortingOrder = Mathf.Clamp(modalSortingOrder, 1000, 32760);

        if (popupRoot.GetComponent<GraphicRaycaster>() == null)
            popupRoot.AddComponent<GraphicRaycaster>();
    }
}
