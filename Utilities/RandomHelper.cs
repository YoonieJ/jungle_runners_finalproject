using System;

namespace jungle_runners_finalproject;

public static class RandomHelper
{
    // Returns true when a random roll falls under the supplied probability.
    public static bool Chance(this Random random, double probability)
    {
        return random.NextDouble() < probability;
    }

    // Returns a random float in the half-open range from min to max.
    public static float Range(this Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }
}
