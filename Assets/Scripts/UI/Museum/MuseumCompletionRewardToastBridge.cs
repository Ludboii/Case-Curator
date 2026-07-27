using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Reuses MuseumPanelUI's existing result text for completion-claim feedback,
/// refreshes the visible MP total and hides the notification after a short
/// unscaled delay.
/// </summary>
public sealed class MuseumCompletionRewardToastBridge : MonoBehaviour
{
    private Coroutine hideCoroutine;
    private MuseumPanelUI owner;
    private string activeMessage;

    public static void Show(
        MuseumPanelUI panel,
        string message,
        float durationSeconds)
    {
        if (panel == null)
            return;

        MuseumCompletionRewardToastBridge bridge =
            panel.GetComponent<MuseumCompletionRewardToastBridge>();

        if (bridge == null)
            bridge = panel.gameObject.AddComponent<
                MuseumCompletionRewardToastBridge>();

        bridge.ShowInternal(
            panel,
            message,
            Mathf.Max(0.25f, durationSeconds));
    }

    private void ShowInternal(
        MuseumPanelUI panel,
        string message,
        float durationSeconds)
    {
        owner = panel;
        activeMessage = message ?? "";

        owner.ShowMuseumMessage(activeMessage);
        RefreshMuseumPointsText();

        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(HideAfter(durationSeconds));
    }

    private IEnumerator HideAfter(float durationSeconds)
    {
        yield return new WaitForSecondsRealtime(durationSeconds);

        if (owner != null)
        {
            TMP_Text[] texts = owner.GetComponentsInChildren<TMP_Text>(true);

            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];

                if (text == null || text.text != activeMessage)
                    continue;

                text.text = "";
                text.gameObject.SetActive(false);
            }
        }

        hideCoroutine = null;
        activeMessage = "";
    }

    private void RefreshMuseumPointsText()
    {
        if (owner == null ||
            SaveManager.Instance == null ||
            SaveManager.Instance.Museum == null)
        {
            return;
        }

        double museumPoints = Math.Max(
            0d,
            SaveManager.Instance.Museum.museumPoints);
        TMP_Text[] texts = owner.GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];

            if (text == null)
                continue;

            string objectName = text.gameObject.name.ToLowerInvariant();

            if (objectName.Contains("museumpoints") ||
                objectName.Contains("museum_points"))
            {
                text.text = $"{museumPoints:0.##} MP";
            }
        }
    }

    private void OnDisable()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
    }
}
