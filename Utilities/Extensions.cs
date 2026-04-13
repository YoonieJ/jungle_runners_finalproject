using Microsoft.Xna.Framework;

namespace jungle_runners_finalproject;

public static class Extensions
{
    public static Vector2 Center(this Rectangle rectangle)
    {
        return new Vector2(rectangle.Center.X, rectangle.Center.Y);
    }

    public static Rectangle InflateBy(this Rectangle rectangle, int amount)
    {
        rectangle.Inflate(amount, amount);
        return rectangle;
    }
}
