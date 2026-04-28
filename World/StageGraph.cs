using System.Collections.Generic;
using System.Linq;

namespace jungle_runners_finalproject;

public sealed class StageGraph
{
    public List<StageNode> Nodes { get; } = [];

    public StageNode? Start => Nodes.Count > 0 ? Nodes[0] : null;

    // Adds a node if another node with the same id is not already present.
    public void AddNode(StageNode node)
    {
        if (Nodes.Any(existing => existing.Id == node.Id))
        {
            return;
        }

        Nodes.Add(node);
    }

    // Adds both route nodes and links the source to the destination.
    public void AddEdge(StageNode from, StageNode to)
    {
        AddNode(from);
        AddNode(to);

        if (!from.Next.Any(node => node.Id == to.Id))
        {
            from.Next.Add(to);
        }
    }

    // Looks up a route node by id.
    public StageNode? GetNode(int id)
    {
        return Nodes.FirstOrDefault(node => node.Id == id);
    }
}
