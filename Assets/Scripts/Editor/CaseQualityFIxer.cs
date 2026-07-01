using UnityEditor;
using UnityEngine;

public static class CaseQualityFixer
{
    [MenuItem("Case Catcher/Recalculate Case Qualities")]
    public static void RecalculateCaseQualities()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:CaseData",
            new[] { "Assets/Data/Cases" });

        int updated = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CaseData caseData =
                AssetDatabase.LoadAssetAtPath<CaseData>(path);

            if (caseData == null)
                continue;

            CaseQuality correctQuality =
                CaseQualityUtility.GetQualityFromGoldPrice(caseData.priceInGold);

            if (caseData.quality != correctQuality)
            {
                caseData.quality = correctQuality;
                EditorUtility.SetDirty(caseData);
                updated++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Case qualities recalculated: {updated} updated.");
    }
}