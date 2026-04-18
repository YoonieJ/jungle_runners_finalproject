using System.Collections.Generic;

namespace jungle_runners_finalproject;

public sealed class Stage
{
    public StageDefinition Definition { get; set; } = new(1, "Stage 1", string.Empty, 0, 0, 0);
    public GridWorld World { get; set; } = new();
    public StageGraph Graph { get; set; } = new();
    public List<MapSegment> Segments { get; } = [];
    public StageNode? CurrentNode { get; set; }
    public bool HasBranchingRoutes => Graph.Nodes.Exists(node => node.Next.Count > 1);
}

// Describes a playable stage and its score thresholds.
public sealed record StageDefinition(
    int Number,
    string Name,
    string Description,
    int BronzeScore,
    int SilverScore,
    int GoldScore);

// Describes one route segment in the stage graph.
public sealed class MapSegment
{
    public string Name { get; set; } = string.Empty;
    public float Length { get; set; } = 1200f;
    public bool HasBoss { get; set; }
    public GridWorld Grid { get; set; } = new(Constants.GameplayRows, 12);
    public int PreferredRow { get; set; } = Constants.MiddleLayer;
}
