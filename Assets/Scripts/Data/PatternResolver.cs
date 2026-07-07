public static class PatternResolver
{
    public static PatternTier ResolveTier(SkinData skin, int patternId)
    {
        foreach (var group in skin.patternTierGroups)
        {
            if (group.patternIds.Contains(patternId))
                return group.tier;
        }
        return PatternTier.None;
    }
}