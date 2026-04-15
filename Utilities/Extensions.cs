using Microsoft.Xna.Framework;

namespace jungle_runners_finalproject;

public static class Extensions
{
    // Returns the center point of a rectangle as a Vector2.
    public static Vector2 Center(this Rectangle rectangle)
    {
        return new Vector2(rectangle.Center.X, rectangle.Center.Y);
    }

    // Returns a copy of a rectangle inflated evenly on both axes.
    public static Rectangle InflateBy(this Rectangle rectangle, int amount)
    {
        rectangle.Inflate(amount, amount);
        return rectangle;
    }
}
