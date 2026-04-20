using System;

namespace jungle_runners_finalproject;

public sealed class StageFactory
{
    private const float RouteColumnSpacing = 150f;

    // Builds a full stage from its definition, including world tiles and route graph.
    public Stage Create(StageDefinition definition, Difficulty difficulty)
    {
        Stage stage = new()
        {
            Definition = definition,
            World = GenerateWorld(definition, difficulty)
        };

        BuildGraph(stage);
        return stage;
    }

    // Generates the deterministic stage grid with pickups, heavier hazard rolls, route markers, and coin guides.
    private static GridWorld GenerateWorld(StageDefinition definition, Difficulty difficulty)
    {
        // TODO: Replace deterministic scatter with authored/data-driven layouts for more stages.
        // TODO: Add stage dialogue triggers and richer route metadata for RPG-style progression.
        // TODO: Playtest hazard/reward density by difficulty once authored layouts are in place.
        int columns = Constants.DefaultStageColumns + definition.Number * 8;
        GridWorld world = new(Constants.GameplayRows, columns);
        Random random = new(definition.Number * 997);
        int previousContentRow = Constants.MiddleLayer;

        for (int column = 4; column < world.Columns; column += 3)
        {
            int row = ChooseContentRow(random, world.Rows, definition.Number, previousContentRow);
            previousContentRow = row;
            Tile tile = world.GetTile(column, row);
            tile.Content = RollStageContent(random, definition.Number, difficulty);

            if (tile.Content == TileContent.Obstacle)
            {
                PlaceObstacleCluster(world, random, column, row, definition.Number, difficulty);
                continue;
            }

            if (tile.Content is TileContent.Obstacle or TileContent.Projectile or TileContent.HomingProjectile or TileContent.Meteor)
            {
                tile.Type = TileType.Hazard;
            }
        }

        int branchColumn = GetBranchColumn(world, definition.Number);
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

        PlaceRecommendedCoinPath(world, definition.Number, branchColumn);
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

    // Chooses pickups and hazards using stage-specific pressure curves biased toward arrows and meteors.
    private static TileContent RollStageContent(Random random, int stageNumber, Difficulty difficulty)
    {
        double roll = random.NextDouble();

        // Adjust roll for difficulty: easy favors rewards, hard favors hazards.
        roll = difficulty switch
        {
            Difficulty.Easy => roll * 0.85, // Lower roll, more rewards
            Difficulty.Hard => Math.Min(roll * 1.15, 0.99), // Higher roll, more hazards
            _ => roll
        };

        return stageNumber switch
        {
            1 => roll switch
            {
                < 0.32 => TileContent.Coin,
                < 0.39 => TileContent.StageItem,
                < 0.43 => TileContent.OutOfStageItem,
                < 0.50 => TileContent.RopeItem,
                < 0.56 => TileContent.ScoreBooster,
                < 0.62 => TileContent.LifeItem,
                < 0.74 => TileContent.Obstacle,
                < 0.90 => TileContent.Projectile,
                _ => TileContent.Meteor
            },
            2 => roll switch
            {
                < 0.25 => TileContent.Coin,
                < 0.31 => TileContent.StageItem,
                < 0.35 => TileContent.OutOfStageItem,
                < 0.41 => TileContent.RopeItem,
                < 0.47 => TileContent.ScoreBooster,
                < 0.52 => TileContent.LifeItem,
                < 0.66 => TileContent.Obstacle,
                < 0.82 => TileContent.Projectile,
                < 0.93 => TileContent.HomingProjectile,
                _ => TileContent.Meteor
            },
            _ => roll switch
            {
                < 0.20 => TileContent.Coin,
                < 0.25 => TileContent.StageItem,
                < 0.29 => TileContent.OutOfStageItem,
                < 0.35 => TileContent.RopeItem,
                < 0.41 => TileContent.ScoreBooster,
                < 0.46 => TileContent.LifeItem,
                < 0.63 => TileContent.Obstacle,
                < 0.79 => TileContent.Projectile,
                < 0.91 => TileContent.HomingProjectile,
                _ => TileContent.Meteor
            }
        };
    }

    // Expands some obstacle rolls into two-row or full-column blocks.
    private static void PlaceObstacleCluster(GridWorld world, Random random, int column, int anchorRow, int stageNumber, Difficulty difficulty)
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

        // Increase cluster size for hard difficulty
        if (difficulty == Difficulty.Hard && rowCount < world.Rows)
        {
            rowCount = Math.Min(rowCount + 1, world.Rows);
        }

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

    // Adds breadcrumb coins along the safest-looking route so the lane choice is readable in motion.
    private static void PlaceRecommendedCoinPath(GridWorld world, int stageNumber, int branchColumn)
    {
        int routeRow = stageNumber switch
        {
            2 => Constants.FrontLayer,
            _ => Constants.BackLayer
        };
        int mergeColumn = branchColumn + 8;
        int bossBufferStart = stageNumber == 3 ? world.Columns - 8 : world.Columns;
        int lastPathColumn = Math.Min(world.Columns - 3, bossBufferStart - 1);

        for (int column = 2; column <= lastPathColumn; column += 2)
        {
            int row = GetRecommendedPathRow(column, branchColumn, mergeColumn, routeRow);
            PlaceCoinNearPath(world, column, row);
        }

        AddTransitionCoins(world, branchColumn, Constants.MiddleLayer, routeRow);
        AddTransitionCoins(world, mergeColumn, routeRow, Constants.MiddleLayer);
    }

    // Smooths row changes with denser coins around branch and merge columns.
    private static void AddTransitionCoins(GridWorld world, int centerColumn, int startRow, int destinationRow)
    {
        for (int offset = -2; offset <= 2; offset++)
        {
            int column = centerColumn + offset;
            int row = InterpolateRow(startRow, destinationRow, offset + 2, 4);
            PlaceCoinNearPath(world, column, row);
        }
    }

    // Picks the guide lane: middle approach, chosen branch route, then middle again after the merge.
    private static int GetRecommendedPathRow(int column, int branchColumn, int mergeColumn, int routeRow)
    {
        if (column < branchColumn - 2)
        {
            return Constants.MiddleLayer;
        }

        if (column <= branchColumn + 2)
        {
            return InterpolateRow(Constants.MiddleLayer, routeRow, column - (branchColumn - 2), 4);
        }

        if (column < mergeColumn - 2)
        {
            return routeRow;
        }

        if (column <= mergeColumn + 2)
        {
            return InterpolateRow(routeRow, Constants.MiddleLayer, column - (mergeColumn - 2), 4);
        }

        return Constants.MiddleLayer;
    }

    private static int InterpolateRow(int startRow, int endRow, int step, int stepCount)
    {
        float progress = stepCount == 0 ? 1f : Math.Clamp(step / (float)stepCount, 0f, 1f);
        return (int)Math.Round(startRow + (endRow - startRow) * progress);
    }

    // Places a guide coin without overwriting hazards, boss tiles, or existing pickups.
    private static void PlaceCoinNearPath(GridWorld world, int column, int preferredRow)
    {
        if (column < 0 || column >= world.Columns)
        {
            return;
        }

        int[] candidateRows = preferredRow switch
        {
            Constants.FrontLayer => [Constants.FrontLayer, Constants.MiddleLayer, Constants.BackLayer],
            Constants.BackLayer => [Constants.BackLayer, Constants.MiddleLayer, Constants.FrontLayer],
            _ => [Constants.MiddleLayer, Constants.BackLayer, Constants.FrontLayer]
        };

        foreach (int row in candidateRows)
        {
            Tile tile = world.GetTile(column, row);
            if (CanPlaceRecommendedCoin(tile))
            {
                tile.Content = TileContent.Coin;
                return;
            }
        }
    }

    private static bool CanPlaceRecommendedCoin(Tile tile)
    {
        return tile.Type is TileType.Ground or TileType.Branch or TileType.Merge
            && tile.Content == TileContent.Empty;
    }

    private static int GetBranchColumn(GridWorld world, int stageNumber)
    {
        return Math.Min(world.Columns - 8, 14 + stageNumber * 4);
    }

    // Builds the prototype route graph that powers branch choices during a run.
    private static void BuildGraph(Stage stage)
    {
        // TODO: Replace this hardcoded graph with full graph/route selection data per stage.
        int branchColumn = GetBranchColumn(stage.World, stage.Definition.Number);
        int mergeColumn = branchColumn + 8;
        int routeEndColumn = stage.Definition.Number == 3 ? stage.World.Columns - 8 : stage.World.Columns - 2;

        MapSegment start = new()
        {
            Name = "Approach",
            Length = branchColumn * RouteColumnSpacing,
            PreferredRow = Constants.MiddleLayer,
            PreviewStartColumn = 0,
            PreviewEndColumn = branchColumn
        };
        MapSegment highRoute = new()
        {
            Name = "Canopy Route",
            Length = (mergeColumn - branchColumn) * RouteColumnSpacing,
            PreferredRow = Constants.BackLayer,
            PreviewStartColumn = branchColumn,
            PreviewEndColumn = mergeColumn
        };
        MapSegment lowRoute = new()
        {
            Name = "Ruins Route",
            Length = (mergeColumn - branchColumn) * RouteColumnSpacing,
            PreferredRow = Constants.FrontLayer,
            PreviewStartColumn = branchColumn,
            PreviewEndColumn = mergeColumn
        };
        MapSegment merge = new()
        {
            Name = "Temple Gate",
            Length = (routeEndColumn - mergeColumn) * RouteColumnSpacing,
            PreferredRow = Constants.MiddleLayer,
            HasBoss = stage.Definition.Number == 3,
            PreviewStartColumn = mergeColumn,
            PreviewEndColumn = routeEndColumn
        };

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
