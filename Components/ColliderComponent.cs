using Microsoft.Xna.Framework;

namespace jungle_runners_finalproject;

public sealed class ColliderComponent
{
    public Rectangle Bounds { get; set; }
    public bool IsTrigger { get; set; }
    public bool Enabled { get; set; } = true;

    // Checks for an overlap when both colliders are enabled.
    public bool Intersects(ColliderComponent other)
    {
        return Enabled && other.Enabled && Bounds.Intersects(other.Bounds);
    }
}
