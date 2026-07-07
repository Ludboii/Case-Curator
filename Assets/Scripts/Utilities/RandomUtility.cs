using System;

public static class RandomUtility
{
    static readonly Random rng = new Random();

    public static double RangeDouble(double min, double max)
    {
        return min + rng.NextDouble() * (max - min);
    }
}