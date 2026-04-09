using System.Text;
using RaceFlow.Core.Enums;
using RaceFlow.Core.Runtime;

namespace RaceFlow.Core.Services;

public sealed class FlowMapValidator
{
    public List<string> Validate(RuntimeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var issues = new List<string>();

        if (graph.Nodes.Count == 0)
            issues.Add("Graph contains no nodes.");

        if (graph.Edges.Count == 0)
            issues.Add("Graph contains no edges.");

        if (graph.StartNodes.Count == 0)
            issues.Add("Graph contains no start nodes.");

        if (graph.FinishNodes.Count == 0)
            issues.Add("Graph contains no finish nodes.");

        foreach (var node in graph.Nodes)
        {
            if (node.NodeType == FlowNodeType.Start && node.Incoming.Count > 0)
                issues.Add($"Start node '{node.Label}' has incoming edges.");

            if ((node.NodeType == FlowNodeType.End || node.NodeType == FlowNodeType.Boss) && node.Outgoing.Count > 0)
                issues.Add($"Finish node '{node.Label}' has outgoing edges.");

            if (node.NodeType == FlowNodeType.Split && node.Outgoing.Count < 2)
                issues.Add($"Split node '{node.Label}' has fewer than 2 outgoing edges.");

            if (node.NodeType == FlowNodeType.Converge && node.Incoming.Count < 2)
                issues.Add($"Converge node '{node.Label}' has fewer than 2 incoming edges.");

            if (node.IsBound && node.MapId is null)
                issues.Add($"Bound node '{node.Label}' has no valid map id.");

            if (node.IsBound && node.TriggerRadius <= 0)
                issues.Add($"Bound node '{node.Label}' has non-positive trigger radius.");
        }

        return issues;
    }

    public string BuildSummary(RuntimeGraph graph)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Layout: {graph.LayoutName}");
        sb.AppendLine($"Version: {graph.Version}");
        sb.AppendLine($"Exported: {graph.ExportedAtUtc:O}");
        sb.AppendLine();

        sb.AppendLine($"Sections: {graph.Sections.Count}");
        sb.AppendLine($"Segments: {graph.Segments.Count}");
        sb.AppendLine($"Nodes: {graph.Nodes.Count}");
        sb.AppendLine($"Edges: {graph.Edges.Count}");
        sb.AppendLine();

        sb.AppendLine($"Start nodes: {graph.StartNodes.Count}");
        foreach (var node in graph.StartNodes)
            sb.AppendLine($"  - {node.Id} | {node.Label} | Segment={node.SegmentLabel}");

        sb.AppendLine();

        sb.AppendLine($"Finish nodes: {graph.FinishNodes.Count}");
        foreach (var node in graph.FinishNodes)
            sb.AppendLine($"  - {node.Id} | {node.Label} | Type={node.NodeType}");

        sb.AppendLine();

        var splitNodes = graph.Nodes.Where(n => n.NodeType == FlowNodeType.Split).ToList();
        sb.AppendLine($"Split nodes: {splitNodes.Count}");
        foreach (var node in splitNodes)
            sb.AppendLine($"  - {node.Label}: {node.Outgoing.Count} branches");

        sb.AppendLine();

        var convergeNodes = graph.Nodes.Where(n => n.NodeType == FlowNodeType.Converge).ToList();
        sb.AppendLine($"Converge nodes: {convergeNodes.Count}");
        foreach (var node in convergeNodes)
            sb.AppendLine($"  - {node.Label}: {node.Incoming.Count} inputs");

        sb.AppendLine();

        sb.AppendLine("Map IDs:");
        foreach (var mapId in graph.MapIds.OrderBy(x => x))
            sb.AppendLine($"  - {mapId}");

        return sb.ToString();
    }
}