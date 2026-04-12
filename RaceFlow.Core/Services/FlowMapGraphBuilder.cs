using RaceFlow.Core.Enums;
using RaceFlow.Core.Models;
using RaceFlow.Core.Runtime;

namespace RaceFlow.Core.Services;

public sealed class FlowMapGraphBuilder
{
    public RuntimeGraph Build(FlowMapDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        double globalTriggerScale = document.Admin?.GlobalTriggerScale ?? 1.0;
        if (globalTriggerScale <= 0)
            globalTriggerScale = 1.0;

        var graph = new RuntimeGraph
        {
            LayoutName = document.LayoutName,
            Version = document.Version,
            ExportedAtUtc = document.ExportedAtUtc
        };

        foreach (var section in document.Sections)
        {
            var runtimeSection = new RuntimeSection
            {
                Side = section.Side,
                DisplayName = section.DisplayName,
                Direction = section.Direction,
                VisualScale = section.VisualScale,
                OffsetX = section.OffsetX,
                OffsetY = section.OffsetY
            };

            graph.Sections.Add(runtimeSection);

            foreach (var segment in section.Segments)
            {
                var runtimeSegment = new RuntimeSegment
                {
                    Id = segment.Id,
                    Index = segment.Index,
                    Label = segment.Label,
                    Side = segment.Side,
                    Direction = segment.Direction,
                    VisualScale = segment.VisualScale,
                    OffsetX = segment.OffsetX,
                    OffsetY = segment.OffsetY,
                    PewFileName = segment.PewFileName,
                    CheckpointFileName = segment.CheckpointFileName
                };

                graph.Segments.Add(runtimeSegment);

                foreach (var node in segment.Nodes)
                {
                    if (string.IsNullOrWhiteSpace(node.Id))
                        throw new InvalidOperationException($"Segment '{segment.Label}' contains a node with no id.");

                    if (graph.NodesById.ContainsKey(node.Id))
                        throw new InvalidOperationException($"Duplicate node id detected: '{node.Id}'.");

                    int? mapId = null;
                    if (int.TryParse(node.Telemetry?.MapId, out var parsedMapId))
                    {
                        mapId = parsedMapId;
                        graph.MapIds.Add(parsedMapId);
                    }

                    double authoredRadius = node.Telemetry?.Trigger?.Radius ?? 0;
                    double scaledRadius = authoredRadius * globalTriggerScale;

                    var runtimeNode = new RuntimeNode
                    {
                        Id = node.Id,
                        Index = node.Index,
                        Label = node.Label,
                        RuntimeLabel = node.RuntimeLabel,
                        NodeType = ParseNodeType(node.Type),
                        SectionSideRaw = section.Side,
                        SegmentId = segment.Id,
                        SegmentLabel = segment.Label,
                        OverlayX = node.Planner?.X ?? 0,
                        OverlayY = node.Planner?.Y ?? 0,
                        IgnoreNode = node.Binding?.IgnoreNode ?? false,
                        IsBound = node.Binding?.IsBound ?? false,
                        IsEndOfRace = node.Binding?.IsEndOfRace ?? false,
                        MapId = mapId,
                        WorldX = node.Telemetry?.Position?.X ?? 0,
                        WorldY = node.Telemetry?.Position?.Y ?? 0,
                        WorldZ = node.Telemetry?.Position?.Z ?? 0,
                        TriggerRadius = scaledRadius,
                        TriggerAngle = node.Telemetry?.Trigger?.Angle ?? 0
                    };

                    graph.Nodes.Add(runtimeNode);
                    graph.NodesById[runtimeNode.Id] = runtimeNode;

                    if (runtimeNode.NodeType == FlowNodeType.Start)
                        graph.StartNodes.Add(runtimeNode);

                    if (runtimeNode.NodeType is FlowNodeType.End or FlowNodeType.Boss || runtimeNode.IsEndOfRace)
                        graph.FinishNodes.Add(runtimeNode);
                }
            }
        }

        foreach (var section in document.Sections)
        {
            foreach (var segment in section.Segments)
            {
                foreach (var node in segment.Nodes)
                {
                    if (!graph.NodesById.TryGetValue(node.Id, out var fromNode))
                        continue;

                    if (node.Graph?.GraphOutputs is null)
                        continue;

                    foreach (var output in node.Graph.GraphOutputs)
                    {
                        if (string.IsNullOrWhiteSpace(output.ToNodeId))
                            continue;

                        if (!graph.NodesById.TryGetValue(output.ToNodeId, out var toNode))
                            throw new InvalidOperationException(
                                $"Node '{node.Id}' points to missing target node '{output.ToNodeId}'.");

                        var edge = new RuntimeEdge
                        {
                            FromNodeId = fromNode.Id,
                            ToNodeId = toNode.Id,
                            FromSocketIndex = output.FromSocketIndex,
                            ToSocketIndex = output.ToSocketIndex,
                            FromNode = fromNode,
                            ToNode = toNode
                        };

                        graph.Edges.Add(edge);
                        fromNode.Outgoing.Add(edge);
                        toNode.Incoming.Add(edge);
                    }
                }
            }
        }

        return graph;
    }

    private static FlowNodeType ParseNodeType(string? rawType)
    {
        return rawType?.Trim().ToLowerInvariant() switch
        {
            "start" => FlowNodeType.Start,
            "checkpoint" => FlowNodeType.Checkpoint,
            "split" => FlowNodeType.Split,
            "converge" => FlowNodeType.Converge,
            "end" => FlowNodeType.End,
            "boss" => FlowNodeType.Boss,
            _ => FlowNodeType.Unknown
        };
    }
}