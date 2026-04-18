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
        int previousContentRow = Constants.MiddleLayer;

        for (int column = 4; column < world.Columns; column += 3)
        {
            int row = ChooseContentRow(random, world.Rows, definition.Number, previousContentRow);
            previousContentRow = row;
            Tile tile = world.GetTile(column, row);
            tile.Content = RollStageContent(random, definition.Number);

            if (tile.Content == TileContent.Obstacle)
            {
                PlaceObstacleCluster(world, random, column, row, definition.Number);
                continue;
            }

            if (tile.Content is TileContent.Obstacle or TileContent.Projectile or TileContent.HomingProjectile)
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
            const int bossAreaColumns = 3;
            int bossStartColumn = world.Columns - 6;
            for (int column = bossStartColumn; column < bossStartColumn + bossAreaColumns; column++)
            {
                for (int row = 0; row < world.Rows; row++)
                {
                    Tile bossTile = world.GetTile(column, row);
                    bossTile.Content = TileContent.Boss;
                    bossTile.Type = TileType.Hazard;
                }
            }
        }

        return world;
    }

    // Biases later stages into changing rows more often so lane movement matters.
    private static int ChooseContentRow(Random random, int rowCount, int stageNumber, int previousRow)
    {
        if (stageNumber <= 1 || random.NextDouble() > 0.68)
        {
            return random.Next(0, rowCount);
        }

        int rowOffset = random.Next(1, rowCount);
        return (previousRow + rowOffset) % rowCount;
    }

    // Chooses pickups and hazards using stage-specific pressure curves.
    private static TileContent RollStageContent(Random random, int stageNumber)
    {
        double roll = random.NextDouble();

        return stageNumber switch
        {
            1 => roll switch
            {
                < 0.38 => TileContent.Coin,
                < 0.46 => TileContent.StageItem,
                < 0.51 => TileContent.OutOfStageItem,
                < 0.59 => TileContent.RopeItem,
                < 0.67 => TileContent.ScoreBooster,
                < 0.75 => TileContent.LifeItem,
                < 0.86 => TileContent.Obstacle,
                _ => TileContent.Projectile
            },
            2 => roll switch
            {
                < 0.30 => TileContent.Coin,
                < 0.37 => TileContent.StageItem,
                < 0.42 => TileContent.OutOfStageItem,
                < 0.49 => TileContent.RopeItem,
                < 0.56 => TileContent.ScoreBooster,
                < 0.61 => TileContent.LifeItem,
                < 0.74 => TileContent.Obstacle,
                < 0.93 => TileContent.Projectile,
                _ => TileContent.HomingProjectile
            },
            _ => roll switch
            {
                < 0.26 => TileContent.Coin,
                < 0.32 => TileContent.StageItem,
                < 0.36 => TileContent.OutOfStageItem,
                < 0.43 => TileContent.RopeItem,
                < 0.50 => TileContent.ScoreBooster,
                < 0.55 => TileContent.LifeItem,
                < 0.76 => TileContent.Obstacle,
                < 0.90 => TileContent.Projectile,
                _ => TileContent.HomingProjectile
            }
        };
    }

    // Expands some obstacle rolls into two-row or full-column blocks.
    private static void PlaceObstacleCluster(GridWorld world, Random random, int column, int anchorRow, int stageNumber)
    {
        double shapeRoll = random.NextDouble();
        int rowCount = stageNumber switch
        {
            1 => shapeRoll < 0.28 ? 2 : 1,
            2 => shapeRoll switch
            {
                < 0.10 => world.Rows,
                < 0.55 => 2,
                _ => 1
            },
            _ => shapeRoll switch
            {
                < 0.30 => world.Rows,
                < 0.72 => 2,
                _ => 1
            }
        };

        int startRow = rowCount switch
        {
            1 => anchorRow,
            2 when anchorRow == Constants.BackLayer => Constants.MiddleLayer,
            2 when anchorRow == Constants.FrontLayer => Constants.FrontLayer,
            2 => random.Next(Constants.FrontLayer, Constants.MiddleLayer + 1),
            _ => Constants.FrontLayer
        };

        for (int row = startRow; row < startRow + rowCount && row < world.Rows; row++)
        {
            Tile obstacleTile = world.GetTile(column, row);
            obstacleTile.Content = TileContent.Obstacle;
            obstacleTile.Type = TileType.Hazard;
        }
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
