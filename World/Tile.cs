using Microsoft.Xna.Framework;

namespace jungle_runners_finalproject;

public sealed class Tile
{
    public Tile(int column, int row, TileType type)
    {
        Column = column;
        Row = row;
        Type = type;
    }

    public int Column { get; }
    public int Row { get; }
    public TileType Type { get; set; }
    public TileContent Content { get; set; } = TileContent.Empty;
    public int RowLayer => Row;
    public bool HasContent => Content != TileContent.Empty;
    public bool IsBlocked => Type is TileType.Pit or TileType.Hazard || Content is TileContent.Obstacle or TileContent.Boss;
    public bool IsCollectible => Content is TileContent.Coin or TileContent.Collectible or TileContent.Item or TileContent.LifeItem or TileContent.ScoreBooster;

    public Rectangle Bounds => new(
        Column * Constants.TileSize,
        Row * Constants.TileSize,
            Constants.TileSize,
            Constants.TileSize);
}
