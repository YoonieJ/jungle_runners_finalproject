using Microsoft.Xna.Framework;

namespace jungle_runners_finalproject;

public static class CollisionHelper
{
    public static bool Intersects(Rectangle a, Rectangle b)
    {
        return a.Intersects(b);
    }

    public static bool Contains(Rectangle bounds, Vector2 point)
    {
        return bounds.Contains(point);
    }

    public static Rectangle FromCenter(Vector2 center, int width, int height)
    {
        return new Rectangle(
            (int)(center.X - width / 2f),
            (int)(center.Y - height / 2f),
            width,
            height);
    }
}
