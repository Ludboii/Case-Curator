public static class SkinNameUtility
{
    public static string BuildSafeName(string weaponName, string skinName)
    {
        return $"{weaponName}_{skinName}"
            .Replace(" ", "_").Replace("|", "").Replace("™", "").Replace("★", "");
    }
}