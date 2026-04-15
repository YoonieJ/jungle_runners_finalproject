using System;

namespace jungle_runners_finalproject;

public sealed class StageFactory
{
    // Builds a full stage from its definition, including world tiles and route graph.
    public Stage Create(StageDefinition definition)
    {
        Stage stage = new()
        {
            Definition = definition,
            World = GenerateWorld(definition)
        };

        BuildGraph(stage);
        return stage;
    }

    // Generates the prototype stage grid with deterministic pickups, hazards, and branch markers.
    private static GridWorld GenerateWorld(StageDefinition definition)
    {
        // TODO: Replace the simple deterministic scatter with authored/data-driven layouts,
        // then tune hazard/reward density by Difficulty.
        int columns = Constants.DefaultStageColumns + definition.Number * 8;
        GridWorld world = new(Constants.GameplayRows, columns);
        Random random = new(definition.Number * 997);

        for (int column = 4; column < world.Columns; column += 3)
        {
            int row = random.Next(0, world.Rows);
            Tile tile = world.GetTile(column, row);
            tile.Content = random.NextDouble() switch
            {
                < 0.34 => TileContent.Coin,
                < 0.52 => TileContent.ScoreBooster,
                < 0.62 => TileContent.LifeItem,
                < 0.82 => TileContent.Obstacle,
                _ => TileContent.Projectile
            };

            if (tile.Content is TileContent.Obstacle or TileContent.Projectile)
            {
                tile.Type = TileType.Hazard;
            }
        }

        int branchColumn = Math.Min(world.Columns - 8, 14 + definition.Number * 4);
        for (int row = 0; row < world.Rows; row++)
        {
            world.GetTile(branchColumn, row).Type = TileType.Branch;
            world.GetTile(branchColumn + 8, row).Type = TileType.Merge;
        }

        if (definition.Number == 3)
        {
            Tile bossTile = world.GetTile(world.Columns - 6, Constants.MiddleLayer);
            bossTile.Content = TileContent.Boss;
            bossTile.Type = TileType.Hazard;
        }

        return world;
    }

    // Builds the route graph that powers branch choices during a run.
    private static void BuildGraph(Stage stage)
    {
        MapSegment start = new() { Name = "Approach", Length = 1200f, PreferredRow = Constants.MiddleLayer };
        MapSegment highRoute = new() { Name = "Canopy Route", Length = 900f, PreferredRow = Constants.BackLayer };
        MapSegment lowRoute = new() { Name = "Ruins Route", Length = 900f, PreferredRow = Constants.FrontLayer };
        MapSegment merge = new() { Name = "Temple Gate", Length = 1200f, PreferredRow = Constants.MiddleLayer, HasBoss = stage.Definition.Number == 3 };

        stage.Segments.Add(start);
        stage.Segments.Add(highRoute);
        stage.Segments.Add(lowRoute);
        stage.Segments.Add(merge);

        StageNode startNode = new(0, start);
        StageNode highNode = new(1, highRoute);
        StageNode lowNode = new(2, lowRoute);
        StageNode mergeNode = new(3, merge);

        stage.Graph.AddEdge(startNode, highNode);
        if (stage.Definition.Number > 1)
        {
            stage.Graph.AddEdge(startNode, lowNode);
            stage.Graph.AddEdge(lowNode, mergeNode);
        }

        stage.Graph.AddEdge(highNode, mergeNode);
        stage.CurrentNode = stage.Graph.Start;
    }
}
