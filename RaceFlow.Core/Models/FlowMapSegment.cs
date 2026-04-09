using System.Text.Json.Serialization;

namespace RaceFlow.Core.Models;

public sealed class FlowMapSegment
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; set; } = string.Empty;

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = string.Empty;

    [JsonPropertyName("pewFileName")]
    public string PewFileName { get; set; } = string.Empty;

    [JsonPropertyName("checkpointFileName")]
    public string CheckpointFileName { get; set; } = string.Empty;

    [JsonPropertyName("nodeCount")]
    public int NodeCount { get; set; }

    [JsonPropertyName("checkpointRecordCount")]
    public int CheckpointRecordCount { get; set; }

    [JsonPropertyName("nodes")]
    public List<FlowMapNode> Nodes { get; set; } = new();
}