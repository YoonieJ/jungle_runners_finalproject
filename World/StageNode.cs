using System.Collections.Generic;

namespace jungle_runners_finalproject;

public sealed class StageNode
{
    // Creates an empty node for serialization or later setup.
    public StageNode()
    {
    }

    // Creates a graph node from a map segment and uses the segment name as its label.
    public StageNode(int id, MapSegment segment)
    {
        Id = id;
        Label = segment.Name;
        Segment = segment;
    }

    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public MapSegment Segment { get; set; } = new();
    public List<StageNode> Next { get; } = [];
}
