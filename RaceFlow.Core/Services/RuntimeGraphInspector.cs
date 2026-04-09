using RaceFlow.Core.Enums;
using RaceFlow.Core.Runtime;

namespace RaceFlow.Core.Services;

public sealed class RuntimeGraphInspector
{
    public RuntimeNode? GetNodeById(RuntimeGraph graph, string nodeId)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (string.IsNullOrWhiteSpace(nodeId))
            return null;

        graph.NodesById.TryGetValue(nodeId, out var node);
        return node;
    }

    public RuntimeNode? GetNodeByLabel(RuntimeGraph graph, string label)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (string.IsNullOrWhiteSpace(label))
            return null;

        return graph.Nodes.FirstOrDefault(n =>
            string.Equals(n.Label, label, StringComparison.OrdinalIgnoreCase));
    }

    public List<RuntimeNode> GetNodesByType(RuntimeGraph graph, FlowNodeType nodeType)
    {
        ArgumentNullException.ThrowIfNull(graph);

        return graph.Nodes
            .Where(n => n.NodeType == nodeType)
            .OrderBy(n => n.SegmentLabel)
            .ThenBy(n => n.Index)
            .ToList();
    }

    public List<RuntimeNode> GetForwardPath(RuntimeNode startNode)
    {
        ArgumentNullException.ThrowIfNull(startNode);

        var results = new List<RuntimeNode>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        WalkForward(startNode, results, visited);

        return results;
    }

    private void WalkForward(RuntimeNode node, List<RuntimeNode> results, HashSet<string> visited)
    {
        if (!visited.Add(node.Id))
            return;

        results.Add(node);

        if (node.Outgoing.Count != 1)
            return;

        var next = node.Outgoing[0].ToNode;
        if (next is null)
            return;

        WalkForward(next, results, visited);
    }

    public string DescribeNode(RuntimeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return
            $"Node: {node.Label}\n" +
            $"  Id: {node.Id}\n" +
            $"  Type: {node.NodeType}\n" +
            $"  Segment: {node.SegmentLabel}\n" +
            $"  MapId: {(node.MapId?.ToString() ?? "null")}\n" +
            $"  Overlay: ({node.OverlayX}, {node.OverlayY})\n" +
            $"  World: ({node.WorldX}, {node.WorldY}, {node.WorldZ})\n" +
            $"  Incoming: {node.Incoming.Count}\n" +
            $"  Outgoing: {node.Outgoing.Count}";
    }

    public string DescribeBranch(RuntimeNode splitNode)
    {
        ArgumentNullException.ThrowIfNull(splitNode);

        if (splitNode.NodeType != FlowNodeType.Split)
            return $"Node '{splitNode.Label}' is not a split node.";

        var lines = new List<string>
        {
            $"Split: {splitNode.Label} ({splitNode.Id})",
            $"Branches: {splitNode.Outgoing.Count}"
        };

        for (int i = 0; i < splitNode.Outgoing.Count; i++)
        {
            var edge = splitNode.Outgoing[i];
            var target = edge.ToNode;

            if (target is null)
            {
                lines.Add($"  Branch {i + 1}: [missing target]");
                continue;
            }

            var chain = GetForwardPath(target);
            string chainText = string.Join(" -> ", chain.Select(n => n.Label));
            lines.Add($"  Branch {i + 1}: {chainText}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}