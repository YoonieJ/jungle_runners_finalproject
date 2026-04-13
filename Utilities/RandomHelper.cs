using System;

namespace jungle_runners_finalproject;

public static class RandomHelper
{
    public static bool Chance(this Random random, double probability)
    {
        return random.NextDouble() < probability;
    }

    public static float Range(this Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }
}
