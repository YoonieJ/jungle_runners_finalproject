namespace jungle_runners_finalproject;

public sealed class TileFactory
{
    public Tile Create(int column, int row, TileType type, TileContent content = TileContent.None)
    {
        return new Tile(column, row, type)
        {
            Content = content
        };
    }
}
