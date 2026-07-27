#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Creates the editable completion-reward balance asset and adds a full-card
/// claim overlay to every Museum skin, weapon and category card prefab.
/// Re-running is safe and preserves existing balance edits.
/// </summary>
public static class MuseumCompletionRewardSetup
{
    private const string ResourcesFolder = "Assets/Resources";
    private const string MuseumResourcesFolder =
        ResourcesFolder + "/Museum";
    private const string BalancePath =
        MuseumResourcesFolder + "/MuseumCompletionRewardBalance.asset";
    private const string OverlayName = "CompletionRewardOverlay";

    [MenuItem(
        "Tools/Case Curator/Museum/Apply Completion Rewards")]
    public static void Apply()
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder(ResourcesFolder, "Museum");

        MuseumCompletionRewardBalanceData balance =
            AssetDatabase.LoadAssetAtPath<
                MuseumCompletionRewardBalanceData>(BalancePath);

        bool createdBalance = false;

        if (balance == null)
        {
            balance = ScriptableObject.CreateInstance<
                MuseumCompletionRewardBalanceData>();
            balance.ResetToDefaults();
            AssetDatabase.CreateAsset(balance, BalancePath);
            createdBalance = true;
        }

        int updatedPrefabs = ApplyOverlaysToCardPrefabs();

        EditorUtility.SetDirty(balance);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        MuseumCompletionRewardService.InvalidateBalanceCache();

        Selection.activeObject = balance;
        EditorGUIUtility.PingObject(balance);

        Debug.Log(
            $"Museum completion rewards applied. " +
            $"Balance {(createdBalance ? "created" : "preserved")}; " +
            $"{updatedPrefabs} card prefab(s) updated.",
            balance);
    }

    [MenuItem(
        "Tools/Case Curator/Museum/Reset Completion Reward Balance")]
    public static void ResetBalance()
    {
        MuseumCompletionRewardBalanceData balance =
            AssetDatabase.LoadAssetAtPath<
                MuseumCompletionRewardBalanceData>(BalancePath);

        if (balance == null)
        {
            EditorUtility.DisplayDialog(
                "Completion Balance Missing",
                "Run Apply Completion Rewards first.",
                "OK");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Reset Completion Reward Balance",
            "Replace all completion reward tuning with the project defaults?",
            "Reset",
            "Cancel");

        if (!confirmed)
            return;

        Undo.RecordObject(balance, "Reset Museum completion rewards");
        balance.ResetToDefaults();
        EditorUtility.SetDirty(balance);
        AssetDatabase.SaveAssets();
        MuseumCompletionRewardService.InvalidateBalanceCache();
        Selection.activeObject = balance;
    }

    private static int ApplyOverlaysToCardPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int updated = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;

            try
            {
                MuseumSkinCardUI[] skinCards =
                    root.GetComponentsInChildren<MuseumSkinCardUI>(true);
                MuseumWeaponCardUI[] weaponCards =
                    root.GetComponentsInChildren<MuseumWeaponCardUI>(true);
                MuseumCategoryCardUI[] categoryCards =
                    root.GetComponentsInChildren<MuseumCategoryCardUI>(true);

                for (int cardIndex = 0;
                     cardIndex < skinCards.Length;
                     cardIndex++)
                {
                    changed |= EnsureOverlay(skinCards[cardIndex].gameObject);
                }

                for (int cardIndex = 0;
                     cardIndex < weaponCards.Length;
                     cardIndex++)
                {
                    changed |= EnsureOverlay(weaponCards[cardIndex].gameObject);
                }

                for (int cardIndex = 0;
                     cardIndex < categoryCards.Length;
                     cardIndex++)
                {
                    changed |= EnsureOverlay(categoryCards[cardIndex].gameObject);
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    updated++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return updated;
    }

    private static bool EnsureOverlay(GameObject cardRoot)
    {
        if (cardRoot == null)
            return false;

        Transform existing = cardRoot.transform.Find(OverlayName);
        GameObject overlay;
        bool created = existing == null;

        if (created)
        {
            overlay = new GameObject(
                OverlayName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(MuseumCompletionClaimOverlayUI));
            overlay.transform.SetParent(cardRoot.transform, false);
        }
        else
        {
            overlay = existing.gameObject;
        }

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.pivot = new Vector2(0.5f, 0.5f);
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlayRect.SetAsLastSibling();

        Image background = overlay.GetComponent<Image>();

        if (background == null)
            background = overlay.AddComponent<Image>();

        background.color = new Color(0.04f, 0.04f, 0.06f, 0.88f);
        background.raycastTarget = true;

        Button button = overlay.GetComponent<Button>();

        if (button == null)
            button = overlay.AddComponent<Button>();

        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;

        MuseumCompletionClaimOverlayUI overlayUI =
            overlay.GetComponent<MuseumCompletionClaimOverlayUI>();

        if (overlayUI == null)
            overlayUI = overlay.AddComponent<MuseumCompletionClaimOverlayUI>();

        Transform textTransform = overlay.transform.Find("ClaimRewardText");
        TextMeshProUGUI text;

        if (textTransform == null)
        {
            GameObject textObject = new GameObject(
                "ClaimRewardText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(overlay.transform, false);
            text = textObject.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            text = textTransform.GetComponent<TextMeshProUGUI>();

            if (text == null)
                text = textTransform.gameObject.AddComponent<TextMeshProUGUI>();
        }

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(12f, 12f);
        textRect.offsetMax = new Vector2(-12f, -12f);

        text.text = "CLAIM REWARD";
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 26f;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.raycastTarget = false;

        SerializedObject serializedOverlay =
            new SerializedObject(overlayUI);
        serializedOverlay.FindProperty("root").objectReferenceValue = overlay;
        serializedOverlay.FindProperty("claimButton").objectReferenceValue = button;
        serializedOverlay.FindProperty("claimText").objectReferenceValue = text;
        serializedOverlay.ApplyModifiedPropertiesWithoutUndo();

        overlay.SetActive(false);
        EditorUtility.SetDirty(cardRoot);
        return created;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;

        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
