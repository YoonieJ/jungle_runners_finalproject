using Microsoft.Xna.Framework;

namespace jungle_runners_finalproject;

public sealed class BossWeakPoint : Entity
{
    public float Lifetime { get; set; }
    public int Row { get; }
    public float Speed { get; }

    public BossWeakPoint(int row, Vector2 position, float speed, float lifetime)
    {
        Row = row;
        Position = position;
        Speed = speed;
        Lifetime = lifetime;
        Size = new Vector2(48f, 48f);
    }
}
