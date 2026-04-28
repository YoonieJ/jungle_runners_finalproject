using System;
using System.Collections.Generic;

namespace jungle_runners_finalproject;

public sealed class StageFactory
{
    private const float RouteColumnSpacing = 150f;
    private const int BossStageExtraColumns = 18;
    private const int BossAreaColumns = 5;
    private const int BossBufferColumns = 12;

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

    private static GridWorld GenerateWorld(StageDefinition definition, Difficulty difficulty)
    {
        int columns = Constants.DefaultStageColumns + definition.Number * 8 + (definition.Number == 3 ? BossStageExtraColumns : 0);
        GridWorld world = new(Constants.GameplayRows, columns);
        Random random = Random.Shared;
        StageLayout layout = ChooseLayout(definition.Number, random);
        StageRouteData routeData = BuildRouteData(definition.Number, world.Columns);

        foreach (StageLayoutPlacement placement in layout.Placements)
        {
            TryPlaceAuthoredContent(world, placement, difficulty, random);
        }

        MarkRouteColumn(world, routeData.BranchColumn, TileType.Branch);
        MarkRouteColumn(world, routeData.MergeColumn, TileType.Merge);

        if (definition.Number == 3)
        {
            int bossStartColumn = world.Columns - (BossAreaColumns + 3);
            for (int column = bossStartColumn; column < bossStartColumn + BossAreaColumns; column++)
            {
                for (int row = 0; row < world.Rows; row++)
                {
                    Tile bossTile = world.GetTile(column, row);
                    bossTile.Content = TileContent.Boss;
                    bossTile.Type = TileType.Hazard;
                }
            }
        }

        PlaceRecommendedCoinPath(world, layout, definition.Number);
        return world;
    }

    private static StageLayout ChooseLayout(int stageNumber, Random random)
    {
        StageLayout[] layouts = StageLayouts.ByStage(stageNumber);
        int totalWeight = 0;
        foreach (StageLayout layout in layouts)
        {
            totalWeight += layout.Weight;
        }

        int roll = random.Next(totalWeight);
        foreach (StageLayout layout in layouts)
        {
            if (roll < layout.Weight)
            {
                return layout;
            }

            roll -= layout.Weight;
        }

        return layouts[0];
    }

    private static void MarkRouteColumn(GridWorld world, int column, TileType type)
    {
        for (int row = 0; row < world.Rows; row++)
        {
            world.GetTile(column, row).Type = type;
        }
    }

    private static void TryPlaceAuthoredContent(GridWorld world, StageLayoutPlacement placement, Difficulty difficulty, Random random)
    {
        if (!world.Contains(placement.Column, placement.Row) || !random.Chance(AdjustedPlacementRate(placement, difficulty)))
        {
            return;
        }

        int rowSpan = difficulty == Difficulty.Hard && placement.Content == TileContent.Obstacle
            ? Math.Min(placement.RowSpan + 1, world.Rows)
            : placement.RowSpan;
        int startRow = Math.Min(placement.Row, world.Rows - rowSpan);
        for (int row = startRow; row < startRow + rowSpan && row < world.Rows; row++)
        {
            Tile tile = world.GetTile(placement.Column, row);
            tile.Content = placement.Content;
            if (IsHazard(placement.Content))
            {
                tile.Type = TileType.Hazard;
            }
        }
    }

    private static double AdjustedPlacementRate(StageLayoutPlacement placement, Difficulty difficulty)
    {
        bool hazard = IsHazard(placement.Content);
        double rate = difficulty switch
        {
            Difficulty.Easy when hazard => placement.Rate * 0.72,
            Difficulty.Hard when hazard => Math.Min(placement.Rate * 1.25, 1.0),
            Difficulty.Easy when !hazard => Math.Min(placement.Rate * 1.18, 1.0),
            Difficulty.Hard when !hazard => placement.Rate * 0.82,
            _ => placement.Rate
        };

        return Math.Clamp(rate, 0.0, 1.0);
    }

    private static bool IsHazard(TileContent content)
    {
        return content is TileContent.Obstacle or TileContent.Projectile or TileContent.HomingProjectile or TileContent.Meteor;
    }

    private static void PlaceRecommendedCoinPath(GridWorld world, StageLayout layout, int stageNumber)
    {
        int bossBufferStart = stageNumber == 3 ? world.Columns - BossBufferColumns : world.Columns;
        int lastPathColumn = Math.Min(world.Columns - 3, bossBufferStart - 1);

        if (layout.CoinPath.Length == 0)
        {
            return;
        }

        for (int i = 0; i < layout.CoinPath.Length - 1; i++)
        {
            StagePathPoint start = layout.CoinPath[i];
            StagePathPoint end = layout.CoinPath[i + 1];
            int direction = start.Column <= end.Column ? 1 : -1;
            int step = direction * 2;
            for (int column = start.Column; direction > 0 ? column <= end.Column : column >= end.Column; column += step)
            {
                if (column > lastPathColumn)
                {
                    break;
                }

                int row = InterpolateRow(start.Row, end.Row, Math.Abs(column - start.Column), Math.Max(Math.Abs(end.Column - start.Column), 1));
                PlaceCoinNearPath(world, column, row);
            }
        }

        foreach (StagePathPoint bend in layout.CoinPath)
        {
            AddCoinCluster(world, bend.Column, bend.Row, lastPathColumn);
        }
    }

    private static int InterpolateRow(int startRow, int endRow, int step, int stepCount)
    {
        float progress = stepCount == 0 ? 1f : Math.Clamp(step / (float)stepCount, 0f, 1f);
        return (int)Math.Round(startRow + (endRow - startRow) * progress);
    }

    private static void AddCoinCluster(GridWorld world, int centerColumn, int centerRow, int lastPathColumn)
    {
        for (int offset = -1; offset <= 1; offset++)
        {
            int column = centerColumn + offset;
            if (column <= lastPathColumn)
            {
                PlaceCoinNearPath(world, column, centerRow);
            }
        }
    }

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

    private sealed record StageLayout(
        int Weight,
        StagePathPoint[] CoinPath,
        StageLayoutPlacement[] Placements);

    private sealed record StagePathPoint(int Column, int Row);

    private sealed record StageLayoutPlacement(
        int Column,
        int Row,
        TileContent Content,
        double Rate,
        int RowSpan = 1);

    private static class StageLayouts
    {
        public static StageLayout[] ByStage(int stageNumber)
        {
            return stageNumber switch
            {
                1 => Stage1,
                2 => Stage2,
                _ => Stage3
            };
        }

        private static readonly StageLayout[] Stage1 =
        [
            new(
                55,
                [new(2, Constants.MiddleLayer), new(10, Constants.MiddleLayer), new(16, Constants.BackLayer), new(26, Constants.BackLayer), new(32, Constants.MiddleLayer), new(48, Constants.MiddleLayer), new(58, Constants.BackLayer)],
                [
                    new(6, Constants.FrontLayer, TileContent.StageItem, 0.80),
                    new(9, Constants.BackLayer, TileContent.Obstacle, 0.85),
                    new(13, Constants.MiddleLayer, TileContent.Projectile, 0.70),
                    new(18, Constants.FrontLayer, TileContent.RopeItem, 0.55),
                    new(22, Constants.MiddleLayer, TileContent.Obstacle, 0.75),
                    new(27, Constants.FrontLayer, TileContent.Meteor, 0.55),
                    new(31, Constants.BackLayer, TileContent.ScoreBooster, 0.45),
                    new(33, Constants.MiddleLayer, TileContent.Meteor, 0.50),
                    new(36, Constants.FrontLayer, TileContent.Obstacle, 0.70, 2),
                    new(42, Constants.BackLayer, TileContent.Projectile, 0.65),
                    new(49, Constants.FrontLayer, TileContent.LifeItem, 0.38),
                    new(54, Constants.MiddleLayer, TileContent.Meteor, 0.45)
                ]),
            new(
                45,
                [new(2, Constants.MiddleLayer), new(8, Constants.FrontLayer), new(18, Constants.FrontLayer), new(26, Constants.MiddleLayer), new(38, Constants.BackLayer), new(50, Constants.BackLayer), new(60, Constants.MiddleLayer)],
                [
                    new(5, Constants.BackLayer, TileContent.Projectile, 0.70),
                    new(11, Constants.MiddleLayer, TileContent.Obstacle, 0.70),
                    new(15, Constants.BackLayer, TileContent.StageItem, 0.65),
                    new(21, Constants.FrontLayer, TileContent.ScoreBooster, 0.45),
                    new(25, Constants.BackLayer, TileContent.Obstacle, 0.75, 2),
                    new(30, Constants.FrontLayer, TileContent.Projectile, 0.60),
                    new(35, Constants.MiddleLayer, TileContent.Meteor, 0.45),
                    new(39, Constants.BackLayer, TileContent.Meteor, 0.50),
                    new(43, Constants.FrontLayer, TileContent.RopeItem, 0.55),
                    new(47, Constants.MiddleLayer, TileContent.Obstacle, 0.72),
                    new(55, Constants.BackLayer, TileContent.OutOfStageItem, 0.32)
                ])
        ];

        private static readonly StageLayout[] Stage2 =
        [
            new(
                50,
                [new(2, Constants.MiddleLayer), new(12, Constants.FrontLayer), new(24, Constants.FrontLayer), new(34, Constants.MiddleLayer), new(44, Constants.BackLayer), new(58, Constants.BackLayer), new(68, Constants.MiddleLayer)],
                [
                    new(6, Constants.BackLayer, TileContent.Projectile, 0.80),
                    new(10, Constants.MiddleLayer, TileContent.Obstacle, 0.78, 2),
                    new(15, Constants.FrontLayer, TileContent.ScoreBooster, 0.42),
                    new(20, Constants.BackLayer, TileContent.HomingProjectile, 0.60),
                    new(25, Constants.MiddleLayer, TileContent.Meteor, 0.60),
                    new(29, Constants.FrontLayer, TileContent.Meteor, 0.56),
                    new(31, Constants.FrontLayer, TileContent.StageItem, 0.50),
                    new(37, Constants.BackLayer, TileContent.Obstacle, 0.70, 2),
                    new(45, Constants.FrontLayer, TileContent.Projectile, 0.75),
                    new(51, Constants.MiddleLayer, TileContent.HomingProjectile, 0.58),
                    new(54, Constants.BackLayer, TileContent.Meteor, 0.60),
                    new(57, Constants.FrontLayer, TileContent.RopeItem, 0.45),
                    new(63, Constants.BackLayer, TileContent.Meteor, 0.58)
                ]),
            new(
                50,
                [new(2, Constants.MiddleLayer), new(10, Constants.BackLayer), new(22, Constants.BackLayer), new(30, Constants.MiddleLayer), new(40, Constants.FrontLayer), new(54, Constants.FrontLayer), new(68, Constants.MiddleLayer)],
                [
                    new(7, Constants.FrontLayer, TileContent.Obstacle, 0.75),
                    new(12, Constants.MiddleLayer, TileContent.Projectile, 0.75),
                    new(17, Constants.BackLayer, TileContent.RopeItem, 0.50),
                    new(23, Constants.FrontLayer, TileContent.Meteor, 0.58),
                    new(28, Constants.MiddleLayer, TileContent.HomingProjectile, 0.58),
                    new(31, Constants.FrontLayer, TileContent.Meteor, 0.58),
                    new(33, Constants.BackLayer, TileContent.Obstacle, 0.72, 2),
                    new(39, Constants.MiddleLayer, TileContent.ScoreBooster, 0.42),
                    new(46, Constants.BackLayer, TileContent.Projectile, 0.76),
                    new(52, Constants.MiddleLayer, TileContent.Obstacle, 0.70, 2),
                    new(56, Constants.BackLayer, TileContent.Meteor, 0.60),
                    new(60, Constants.FrontLayer, TileContent.OutOfStageItem, 0.30),
                    new(66, Constants.BackLayer, TileContent.Meteor, 0.55)
                ])
        ];

        private static readonly StageLayout[] Stage3 =
        [
            new(
                60,
                [new(2, Constants.MiddleLayer), new(12, Constants.BackLayer), new(24, Constants.BackLayer), new(34, Constants.MiddleLayer), new(46, Constants.FrontLayer), new(58, Constants.FrontLayer), new(70, Constants.MiddleLayer), new(82, Constants.BackLayer)],
                [
                    new(6, Constants.FrontLayer, TileContent.Projectile, 0.82),
                    new(11, Constants.MiddleLayer, TileContent.Obstacle, 0.82, 2),
                    new(16, Constants.BackLayer, TileContent.HomingProjectile, 0.68),
                    new(22, Constants.FrontLayer, TileContent.Meteor, 0.70),
                    new(25, Constants.BackLayer, TileContent.Meteor, 0.68),
                    new(27, Constants.MiddleLayer, TileContent.ScoreBooster, 0.38),
                    new(32, Constants.BackLayer, TileContent.Obstacle, 0.80, 2),
                    new(38, Constants.FrontLayer, TileContent.HomingProjectile, 0.70),
                    new(44, Constants.MiddleLayer, TileContent.Meteor, 0.72),
                    new(47, Constants.FrontLayer, TileContent.Meteor, 0.70),
                    new(50, Constants.BackLayer, TileContent.Projectile, 0.82),
                    new(57, Constants.MiddleLayer, TileContent.RopeItem, 0.35),
                    new(63, Constants.FrontLayer, TileContent.Obstacle, 0.78, 2),
                    new(70, Constants.BackLayer, TileContent.HomingProjectile, 0.72),
                    new(76, Constants.MiddleLayer, TileContent.Meteor, 0.72),
                    new(79, Constants.FrontLayer, TileContent.Meteor, 0.70),
                    new(82, Constants.FrontLayer, TileContent.StageItem, 0.35)
                ]),
            new(
                40,
                [new(2, Constants.MiddleLayer), new(14, Constants.FrontLayer), new(26, Constants.MiddleLayer), new(38, Constants.BackLayer), new(52, Constants.BackLayer), new(64, Constants.MiddleLayer), new(78, Constants.FrontLayer), new(84, Constants.MiddleLayer)],
                [
                    new(7, Constants.BackLayer, TileContent.Obstacle, 0.82, 2),
                    new(13, Constants.MiddleLayer, TileContent.HomingProjectile, 0.70),
                    new(19, Constants.FrontLayer, TileContent.Projectile, 0.82),
                    new(25, Constants.BackLayer, TileContent.Meteor, 0.72),
                    new(28, Constants.MiddleLayer, TileContent.Meteor, 0.68),
                    new(31, Constants.FrontLayer, TileContent.Obstacle, 0.80, 2),
                    new(37, Constants.MiddleLayer, TileContent.ScoreBooster, 0.36),
                    new(43, Constants.BackLayer, TileContent.Projectile, 0.82),
                    new(49, Constants.FrontLayer, TileContent.HomingProjectile, 0.72),
                    new(55, Constants.MiddleLayer, TileContent.Meteor, 0.72),
                    new(58, Constants.BackLayer, TileContent.Meteor, 0.70),
                    new(61, Constants.BackLayer, TileContent.Obstacle, 0.78, 2),
                    new(67, Constants.FrontLayer, TileContent.RopeItem, 0.32),
                    new(73, Constants.MiddleLayer, TileContent.HomingProjectile, 0.74),
                    new(79, Constants.BackLayer, TileContent.Meteor, 0.72)
                ])
        ];
    }

    private static bool CanPlaceRecommendedCoin(Tile tile)
    {
        return tile.Type is TileType.Ground or TileType.Branch or TileType.Merge
            && tile.Content == TileContent.Empty;
    }

    private static void BuildGraph(Stage stage)
    {
        StageRouteData routeData = BuildRouteData(stage.Definition.Number, stage.World.Columns);
        Dictionary<int, StageNode> nodes = [];
        foreach (StageRouteNodeData nodeData in routeData.Nodes)
        {
            MapSegment segment = new()
            {
                Name = nodeData.Name,
                Length = Math.Max(1, nodeData.EndColumn - nodeData.StartColumn) * RouteColumnSpacing,
                PreferredRow = nodeData.PreferredRow,
                HasBoss = nodeData.HasBoss,
                PreviewStartColumn = nodeData.StartColumn,
                PreviewEndColumn = nodeData.EndColumn
            };
            stage.Segments.Add(segment);

            StageNode node = new(nodeData.Id, segment);
            nodes.Add(nodeData.Id, node);
            stage.Graph.AddNode(node);
        }

        foreach (StageRouteEdgeData edge in routeData.Edges)
        {
            stage.Graph.AddEdge(nodes[edge.FromId], nodes[edge.ToId]);
        }

        stage.CurrentNode = stage.Graph.Start;
    }

    private static StageRouteData BuildRouteData(int stageNumber, int worldColumns)
    {
        int routeEndColumn = stageNumber == 3 ? worldColumns - 8 : worldColumns - 2;
        return stageNumber switch
        {
            1 => new(
                BranchColumn: 18,
                MergeColumn: 26,
                Nodes:
                [
                    new(0, "Gate Approach", 0, 18, Constants.MiddleLayer),
                    new(1, "Canopy Bend", 18, 26, Constants.BackLayer),
                    new(2, "Overgrown Exit", 26, routeEndColumn, Constants.MiddleLayer)
                ],
                Edges:
                [
                    new(0, 1),
                    new(1, 2)
                ]),
            2 => new(
                BranchColumn: 22,
                MergeColumn: 30,
                Nodes:
                [
                    new(0, "Serpent Approach", 0, 22, Constants.MiddleLayer),
                    new(1, "Canopy Shortcut", 22, 30, Constants.BackLayer),
                    new(2, "Ruins Causeway", 22, 30, Constants.FrontLayer),
                    new(3, "Serpent Merge", 30, routeEndColumn, Constants.MiddleLayer)
                ],
                Edges:
                [
                    new(0, 1),
                    new(0, 2),
                    new(1, 3),
                    new(2, 3)
                ]),
            _ => new(
                BranchColumn: 26,
                MergeColumn: 34,
                Nodes:
                [
                    new(0, "Idol Approach", 0, 26, Constants.MiddleLayer),
                    new(1, "Sunken Canopy", 26, 34, Constants.BackLayer),
                    new(2, "Relic Floor", 26, 34, Constants.FrontLayer),
                    new(3, "Broken Stair", 34, 54, Constants.MiddleLayer),
                    new(4, "Boss Gate", 54, routeEndColumn, Constants.BackLayer, HasBoss: true)
                ],
                Edges:
                [
                    new(0, 1),
                    new(0, 2),
                    new(1, 3),
                    new(2, 3),
                    new(3, 4)
                ])
        };
    }

    private sealed record StageRouteData(
        int BranchColumn,
        int MergeColumn,
        StageRouteNodeData[] Nodes,
        StageRouteEdgeData[] Edges);

    private sealed record StageRouteNodeData(
        int Id,
        string Name,
        int StartColumn,
        int EndColumn,
        int PreferredRow,
        bool HasBoss = false);

    private sealed record StageRouteEdgeData(int FromId, int ToId);
}
