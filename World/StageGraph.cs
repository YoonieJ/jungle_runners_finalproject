using System.Collections.Generic;
using System.Linq;

namespace jungle_runners_finalproject;

public sealed class StageGraph
{
    public List<StageNode> Nodes { get; } = [];

    public StageNode? Start => Nodes.Count > 0 ? Nodes[0] : null;

    public void AddNode(StageNode node)
    {
        if (Nodes.Any(existing => existing.Id == node.Id))
        {
            return;
        }

        Nodes.Add(node);
    }

    public void AddEdge(StageNode from, StageNode to)
    {
        AddNode(from);
        AddNode(to);

        if (!from.Next.Any(node => node.Id == to.Id))
        {
            from.Next.Add(to);
        }
    }

    public StageNode? GetNode(int id)
    {
        return Nodes.FirstOrDefault(node => node.Id == id);
    }
}
