using System;
using UnityEngine;

[Serializable]
public class FadeEntry
{
    public int patternId;
    [Range(0f, 100f)]
    public float fadePercent;
}