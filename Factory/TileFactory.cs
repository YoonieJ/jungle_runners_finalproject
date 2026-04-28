namespace jungle_runners_finalproject;

public sealed class TileFactory
{
    // Creates a tile with the requested grid position, type, and optional content.
    public Tile Create(int column, int row, TileType type, TileContent content = TileContent.None)
    {
        return new Tile(column, row, type)
        {
            Content = content
        };
    }
}
