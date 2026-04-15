using System;
using System.Collections.Generic;
using System.Linq;

namespace jungle_runners_finalproject;

public sealed class GridWorld
{
    // Creates a grid using the default gameplay row and stage column counts.
    public GridWorld()
        : this(Constants.GameplayRows, Constants.DefaultStageColumns)
    {
    }

    // Creates a rectangular tile grid and fills it with ground tiles.
    public GridWorld(int rows, int columns)
    {
        if (rows <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rows), "Rows must be positive.");
        }

        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns), "Columns must be positive.");
        }

        Tiles = new Tile[rows][];
        for (int row = 0; row < rows; row++)
        {
            Tiles[row] = new Tile[columns];
            for (int column = 0; column < columns; column++)
            {
                Tiles[row][column] = new Tile(column, row, TileType.Ground);
            }
        }
    }

    public Tile[][] Tiles { get; }
    public int Rows => Tiles.Length;
    public int Columns => Tiles[0].Length;

    public IEnumerable<Tile> AllTiles => Tiles.SelectMany(row => row);

    // Returns the tile at a grid coordinate, or an empty tile when outside the grid.
    public Tile GetTile(int column, int row)
    {
        if (!Contains(column, row))
        {
            return new Tile(column, row, TileType.Empty);
        }

        return Tiles[row][column];
    }

    // Replaces an in-bounds tile in the grid.
    public void SetTile(Tile tile)
    {
        if (!Contains(tile.Column, tile.Row))
        {
            throw new ArgumentOutOfRangeException(nameof(tile), "Tile must fit inside this grid.");
        }

        Tiles[tile.Row][tile.Column] = tile;
    }

    // Checks whether a column and row are inside this grid.
    public bool Contains(int column, int row)
    {
        return row >= 0 && row < Rows && column >= 0 && column < Columns;
    }

    // Enumerates every tile in one column, or no tiles if the column is out of range.
    public IEnumerable<Tile> TilesInColumn(int column)
    {
        if (column < 0 || column >= Columns)
        {
            yield break;
        }

        for (int row = 0; row < Rows; row++)
        {
            yield return Tiles[row][column];
        }
    }
}
