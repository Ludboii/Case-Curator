using UnityEngine;

public static class FadeUtility
{
    public static float GetFadePercent(SkinData skin, int patternId)
    {
        foreach (var entry in skin.fadeOverrides)
            if (entry.patternId == patternId) return entry.fadePercent;

        float t = patternId / 1000f;
        if (skin.reverseFadePattern) t = 1f - t;
        return Mathf.Lerp(skin.fadeRangeStart, skin.fadeRangeEnd, t);
    }

    public static string GetFadeDisplay(SkinData skin, int patternId)
    {
        return $"{GetFadePercent(skin, patternId):F2}%";
    }
}