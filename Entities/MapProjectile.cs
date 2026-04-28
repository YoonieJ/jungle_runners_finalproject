using Microsoft.Xna.Framework;

namespace jungle_runners_finalproject;

// Runtime state for a projectile activated from a world tile.
public sealed class MapProjectile
{
    // Creates an active map projectile from a tile marker.
    public MapProjectile(TileContent kind, int row, Vector2 position, float speed, float rowShiftTimer)
    {
        Kind = kind;
        Row = row;
        Position = position;
        Speed = speed;
        RowShiftTimer = rowShiftTimer;
    }

    public TileContent Kind { get; }
    public int Row { get; set; }
    public Vector2 Position { get; set; }
    public float Speed { get; }
    public float RowShiftTimer { get; set; }
    public bool HasCheckedCollision { get; set; }
}
