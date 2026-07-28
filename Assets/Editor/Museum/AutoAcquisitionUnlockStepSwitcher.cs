#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fast development switch for the hard runtime milestone guard and generated
/// UnlockDefinition. Use Step 5 while testing and restore Step 40 for production.
/// </summary>
public static class AutoAcquisitionUnlockStepSwitcher
{
    private const string ServicePath =
        "Assets/Scripts/AutomatedAcquisitions/AutoAcquisitionService.cs";
    private const string SetupPath =
        "Assets/Editor/Museum/AutomatedAcquisitionsSetup.cs";

    [MenuItem(
        "Tools/Case Curator/Museum/Automated Acquisitions/Use Test Step 5")]
    public static void UseStep5()
    {
        Apply(5);
    }

    [MenuItem(
        "Tools/Case Curator/Museum/Automated Acquisitions/Restore Production Step 40")]
    public static void UseStep40()
    {
        Apply(40);
    }

    private static void Apply(int step)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Exit Play Mode",
                "Change the Automated Acquisitions unlock step outside Play Mode.",
                "OK");
            return;
        }

        bool serviceChanged = ReplaceServiceStep(step);
        bool setupChanged = ReplaceSetupStep(step);

        string[] catalogGuids =
            AssetDatabase.FindAssets("t:AutoAcquisitionCatalogData");

        for (int i = 0; i < catalogGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(catalogGuids[i]);
            AutoAcquisitionCatalogData catalog =
                AssetDatabase.LoadAssetAtPath<AutoAcquisitionCatalogData>(path);

            if (catalog == null)
                continue;

            catalog.requiredMuseumStaircaseStep = step;
            catalog.ignoreMuseumStepRequirementForTesting = false;
            EditorUtility.SetDirty(catalog);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Automated Acquisitions now requires Museum Staircase Step {step}. " +
            $"Runtime source changed: {serviceChanged}; setup source changed: " +
            $"{setupChanged}. Re-run Apply Automated Acquisitions Wing after " +
            "Unity finishes compiling so the UnlockDefinition matches.");
    }

    private static bool ReplaceServiceStep(int step)
    {
        if (!File.Exists(ServicePath))
            return false;

        string source = File.ReadAllText(ServicePath);
        string updated = Regex.Replace(
            source,
            @"MuseumMilestoneData\s+step\d+\s*=\s*FindStaircaseStep\(\d+\);",
            $"MuseumMilestoneData requiredStep = FindStaircaseStep({step});");

        updated = Regex.Replace(
            updated,
            @"step\d+\.milestoneId",
            "requiredStep.milestoneId");
        updated = Regex.Replace(
            updated,
            @"step\d+\s*==\s*null",
            "requiredStep == null");
        updated = Regex.Replace(
            updated,
            @"Museum Staircase step \d+",
            $"Museum Staircase step {step}");
        updated = Regex.Replace(
            updated,
            @"Claim Museum Staircase step \d+",
            $"Claim Museum Staircase step {step}");

        if (updated == source)
            return false;

        File.WriteAllText(ServicePath, updated);
        return true;
    }

    private static bool ReplaceSetupStep(int step)
    {
        if (!File.Exists(SetupPath))
            return false;

        string source = File.ReadAllText(SetupPath);
        string updated = Regex.Replace(
            source,
            @"MuseumMilestoneData\s+step\d+\s*=\s*FindStep\(database,\s*\d+\);",
            $"MuseumMilestoneData requiredStep = FindStep(database, {step});");

        updated = Regex.Replace(
            updated,
            @"step\d+\.milestoneId",
            "requiredStep.milestoneId");
        updated = Regex.Replace(
            updated,
            @"step\d+\s*==\s*null",
            "requiredStep == null");
        updated = Regex.Replace(
            updated,
            @"Museum Step \d+ Missing",
            $"Museum Step {step} Missing");
        updated = Regex.Replace(
            updated,
            @"Museum Staircase step \d+",
            $"Museum Staircase step {step}");
        updated = Regex.Replace(
            updated,
            @"Claim Museum Staircase step \d+",
            $"Claim Museum Staircase step {step}");

        if (updated == source)
            return false;

        File.WriteAllText(SetupPath, updated);
        return true;
    }
}
#endif
