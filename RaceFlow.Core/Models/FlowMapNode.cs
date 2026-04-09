using System.Text.Json.Serialization;

namespace RaceFlow.Core.Models;

public sealed class FlowMapNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("runtimeLabel")]
    public string RuntimeLabel { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("planner")]
    public FlowMapPlanner? Planner { get; set; }

    [JsonPropertyName("binding")]
    public FlowMapBinding? Binding { get; set; }

    [JsonPropertyName("telemetry")]
    public FlowMapTelemetry? Telemetry { get; set; }

    [JsonPropertyName("graph")]
    public FlowMapGraph? Graph { get; set; }
}