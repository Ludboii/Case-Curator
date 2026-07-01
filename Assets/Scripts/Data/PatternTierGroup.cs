using System;
using System.Collections.Generic;

[Serializable]
public class PatternTierGroup
{
    public PatternTier tier;
    public float multiplier;
    public List<int> patternIds = new List<int>();
}