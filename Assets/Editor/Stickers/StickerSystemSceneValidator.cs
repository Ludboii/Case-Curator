#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StickerSystemSceneValidator
{
    [MenuItem("Tools/Case Curator/Stickers/Auto-wire SkinInspect Sticker Slots")]
    public static void AutoWireSkinInspectSlots()
    {
        SkinInspectUI[] inspectors = Object.FindObjectsByType<SkinInspectUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (inspectors.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "SkinInspectUI Missing",
                "No SkinInspectUI exists in the open scenes.",
                "OK");
            return;
        }

        int wired = 0;

        for (int i = 0; i < inspectors.Length; i++)
        {
            SkinInspectUI inspect = inspectors[i];
            Transform root = FindChildRecursive(
                inspect.transform,
                "StickerSlotsRoot");

            if (root == null)
            {
                Debug.LogWarning(
                    $"{inspect.name}: StickerSlotsRoot was not found.",
                    inspect);
                continue;
            }

            SkinInspectStickerSlotsUI controller =
                inspect.GetComponent<SkinInspectStickerSlotsUI>();

            if (controller == null)
            {
                controller = Undo.AddComponent<SkinInspectStickerSlotsUI>(
                    inspect.gameObject);
            }

            List<Button> buttons = new List<Button>();

            for (int childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                Button button = root.GetChild(childIndex).GetComponent<Button>();

                if (button != null)
                    buttons.Add(button);
            }

            if (buttons.Count < 4)
            {
                buttons.Clear();
                buttons.AddRange(root.GetComponentsInChildren<Button>(true));
            }

            if (buttons.Count < 4)
            {
                Debug.LogWarning(
                    $"{inspect.name}: StickerSlotsRoot contains only " +
                    $"{buttons.Count} Button component(s); four are required.",
                    root);
                continue;
            }

            StickerSlotButtonUI[] slotComponents = new StickerSlotButtonUI[4];

            for (int slot = 0; slot < 4; slot++)
            {
                Button button = buttons[slot];
                StickerSlotButtonUI slotUI =
                    button.GetComponent<StickerSlotButtonUI>();

                if (slotUI == null)
                    slotUI = Undo.AddComponent<StickerSlotButtonUI>(button.gameObject);

                Image icon = FindOrCreateStickerIcon(button.transform);
                TMP_Text plusText = FindPlusText(button.transform);
                SerializedObject slotSerialized = new SerializedObject(slotUI);
                slotSerialized.FindProperty("slotIndex").intValue = slot;
                slotSerialized.FindProperty("button").objectReferenceValue = button;
                slotSerialized.FindProperty("stickerImage").objectReferenceValue = icon;
                slotSerialized.FindProperty("plusText").objectReferenceValue = plusText;
                slotSerialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(slotUI);
                slotComponents[slot] = slotUI;
            }

            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("inspectUI").objectReferenceValue = inspect;
            serialized.FindProperty("stickerSlotsRoot").objectReferenceValue =
                root.gameObject;

            TMP_Text heading = FindSiblingText(
                root,
                "StickerSlotsText");
            serialized.FindProperty("stickerSlotsText").objectReferenceValue = heading;

            SerializedProperty slots = serialized.FindProperty("slots");
            slots.arraySize = 4;

            for (int slot = 0; slot < 4; slot++)
            {
                slots.GetArrayElementAtIndex(slot).objectReferenceValue =
                    slotComponents[slot];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            wired++;
        }

        MarkOpenScenesDirty();
        Debug.Log($"Auto-wired sticker slots on {wired} SkinInspectUI object(s).");
    }

    [MenuItem("Tools/Case Curator/Stickers/Validate Open Scene Setup")]
    public static void ValidateOpenSceneSetup()
    {
        StringBuilder report = new StringBuilder();
        int errors = 0;
        int warnings = 0;

        ValidateCount<SkinInspectStickerSlotsUI>(
            "SkinInspectStickerSlotsUI",
            true,
            report,
            ref errors,
            ref warnings);
        ValidateCount<StickerPickerPopupUI>(
            "StickerPickerPopupUI",
            true,
            report,
            ref errors,
            ref warnings);
        ValidateCount<StickerSlotActionPopupUI>(
            "StickerSlotActionPopupUI",
            true,
            report,
            ref errors,
            ref warnings);
        ValidateCount<StickerInspectUI>(
            "StickerInspectUI",
            true,
            report,
            ref errors,
            ref warnings);
        ValidateCount<InventoryItemTypeFilterUI>(
            "InventoryItemTypeFilterUI",
            false,
            report,
            ref errors,
            ref warnings);
        ValidateCount<StickerCapsuleCompletionUI>(
            "StickerCapsuleCompletionUI",
            false,
            report,
            ref errors,
            ref warnings);
        ValidateCount<TradeupStickerWarningUI>(
            "TradeupStickerWarningUI",
            false,
            report,
            ref errors,
            ref warnings);

        InventoryItemCardUI[] cardObjects =
            Object.FindObjectsByType<InventoryItemCardUI>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        bool hasCardIcons = false;

        for (int i = 0; i < cardObjects.Length; i++)
        {
            if (cardObjects[i] != null &&
                cardObjects[i].GetComponent<InventoryCardStickerIconsUI>() != null)
            {
                hasCardIcons = true;
                break;
            }
        }

        if (!hasCardIcons)
        {
            warnings++;
            report.AppendLine(
                "WARNING: No InventoryCardStickerIconsUI was found on an " +
                "InventoryItemCardUI prefab/object.");
        }

        GameDatabase database = FindDatabase();

        if (database == null)
        {
            errors++;
            report.AppendLine("ERROR: Main GameDatabase could not be resolved.");
        }
        else
        {
            report.AppendLine(
                $"Database stickers: " +
                $"{(database.allStickers != null ? database.allStickers.Count : 0):N0}");

            int capsules = 0;

            if (database.allCases != null)
            {
                for (int i = 0; i < database.allCases.Count; i++)
                {
                    CaseData data = database.allCases[i];

                    if (data != null &&
                        data.containerType == CaseContainerType.StickerCapsule)
                    {
                        capsules++;
                    }
                }
            }

            report.AppendLine($"Database Sticker Capsules: {capsules:N0}");

            if (database.allStickers == null || database.allStickers.Count == 0)
            {
                warnings++;
                report.AppendLine(
                    "WARNING: Run the ByMykel sticker importer before testing.");
            }
        }

        report.Insert(
            0,
            $"Sticker System Validation\nErrors: {errors} | " +
            $"Warnings: {warnings}\n\n");

        Debug.Log(report.ToString());
        EditorUtility.DisplayDialog(
            errors > 0 ? "Sticker Setup Has Errors" : "Sticker Setup Validation",
            report.ToString(),
            "OK");
    }

    private static void ValidateCount<T>(
        string label,
        bool required,
        StringBuilder report,
        ref int errors,
        ref int warnings)
        where T : Component
    {
        T[] components = Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (components.Length > 0)
        {
            report.AppendLine($"OK: {label} ({components.Length})");
            return;
        }

        if (required)
        {
            errors++;
            report.AppendLine($"ERROR: {label} is missing.");
        }
        else
        {
            warnings++;
            report.AppendLine($"WARNING: {label} is not configured.");
        }
    }

    private static Image FindOrCreateStickerIcon(Transform button)
    {
        Transform existing = button.Find("StickerIcon");

        if (existing != null)
        {
            Image found = existing.GetComponent<Image>();

            if (found != null)
                return found;
        }

        GameObject iconObject = new GameObject(
            "StickerIcon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        Undo.RegisterCreatedObjectUndo(iconObject, "Create Sticker Icon");
        iconObject.transform.SetParent(button, false);
        RectTransform rect = iconObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(4f, 4f);
        rect.offsetMax = new Vector2(-4f, -4f);
        Image image = iconObject.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    private static TMP_Text FindPlusText(Transform button)
    {
        TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];

            if (text != null &&
                (text.text == "+" ||
                 text.name.IndexOf("plus", System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return text;
            }
        }

        return texts.Length > 0 ? texts[0] : null;
    }

    private static TMP_Text FindSiblingText(Transform root, string objectName)
    {
        Transform parent = root.parent;

        if (parent == null)
            return null;

        Transform direct = parent.Find(objectName);
        return direct != null ? direct.GetComponent<TMP_Text>() : null;
    }

    private static Transform FindChildRecursive(
        Transform parent,
        string objectName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == objectName)
                return child;

            Transform nested = FindChildRecursive(child, objectName);

            if (nested != null)
                return nested;
        }

        return null;
    }

    private static GameDatabase FindDatabase()
    {
        if (Selection.activeObject is GameDatabase selected)
            return selected;

        string[] guids = AssetDatabase.FindAssets("t:GameDatabase");

        if (guids.Length != 1)
            return null;

        return AssetDatabase.LoadAssetAtPath<GameDatabase>(
            AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static void MarkOpenScenesDirty()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (scene.isLoaded)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif
