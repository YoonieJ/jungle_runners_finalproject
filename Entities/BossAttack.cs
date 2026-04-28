using Microsoft.Xna.Framework;

namespace jungle_runners_finalproject;

// Runtime state for a boss attack currently crossing the screen.
public sealed class BossAttack
{
    // Creates a boss attack moving across one lane.
    public BossAttack(BossAttackKind kind, int row, Vector2 position, float speed)
    {
        Kind = kind;
        Row = row;
        Position = position;
        Speed = speed;
    }

    public BossAttackKind Kind { get; }
    public int Row { get; }
    public Vector2 Position { get; set; }
    public float Speed { get; }
    public bool HasCheckedCollision { get; set; }
}
