using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AutoAcquisitionContainerSelectionPopupUI : MonoBehaviour
{
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text emptyText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Transform content;
    [SerializeField]
    private AutoAcquisitionContainerSelectionCardUI cardPrefab;

    private readonly List<GameObject> spawned = new List<GameObject>();

    private AutomatedAcquisitionsPanelUI owner;
    private AutoAcquisitionService service;
    private int lineIndex = -1;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    public void Open(
        int processingLineIndex,
        AutomatedAcquisitionsPanelUI panel,
        AutoAcquisitionService acquisitionService)
    {
        lineIndex = processingLineIndex;
        owner = panel;
        service = acquisitionService;

        if (popupRoot != null)
            popupRoot.SetActive(true);
        else
            gameObject.SetActive(true);

        Rebuild();
    }

    public void Close()
    {
        Clear();

        if (popupRoot != null)
            popupRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    public void Select(AutoAcquisitionContainerData entry)
    {
        if (service == null || entry == null || lineIndex < 0)
            return;

        AutoAcquisitionActionResult result = service.SelectLineTarget(
            lineIndex,
            entry.containerId);

        if (owner != null)
            owner.HandleActionResult(result);

        if (result != null && result.success)
            Close();
    }

    private void Rebuild()
    {
        Clear();

        if (titleText != null)
            titleText.text = $"SELECT CONTAINER — LINE {lineIndex + 1}";

        if (service == null || service.Catalog == null ||
            cardPrefab == null || content == null)
        {
            SetEmpty("Selection menu references are incomplete.");
            return;
        }

        int count = 0;

        if (service.Catalog.containers != null)
        {
            for (int i = 0; i < service.Catalog.containers.Count; i++)
            {
                AutoAcquisitionContainerData entry =
                    service.Catalog.containers[i];

                if (entry == null ||
                    entry.container == null ||
                    !service.IsContainerResearched(entry.containerId))
                {
                    continue;
                }

                AutoAcquisitionContainerSelectionCardUI card =
                    Instantiate(cardPrefab, content);
                card.Setup(entry, this);
                spawned.Add(card.gameObject);
                count++;
            }
        }

        SetEmpty(count == 0
            ? "Research a container in the Receiving Dock first."
            : "");
    }

    private void SetEmpty(string message)
    {
        if (emptyText == null)
            return;

        emptyText.text = message ?? "";
        emptyText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
    }

    private void Clear()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
                Destroy(spawned[i]);
        }

        spawned.Clear();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }
}
