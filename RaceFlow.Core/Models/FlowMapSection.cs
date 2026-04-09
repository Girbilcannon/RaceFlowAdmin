using System.Text.Json.Serialization;

namespace RaceFlow.Core.Models;

public sealed class FlowMapSection
{
    [JsonPropertyName("side")]
    public string Side { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = string.Empty;

    [JsonPropertyName("visualScale")]
    public float VisualScale { get; set; } = 1.0f;

    [JsonPropertyName("offsetX")]
    public float OffsetX { get; set; } = 0f;

    [JsonPropertyName("offsetY")]
    public float OffsetY { get; set; } = 0f;

    [JsonPropertyName("segmentCount")]
    public int SegmentCount { get; set; }

    [JsonPropertyName("segments")]
    public List<FlowMapSegment> Segments { get; set; } = new();
}