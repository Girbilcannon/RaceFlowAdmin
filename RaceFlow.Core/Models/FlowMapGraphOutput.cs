using System.Text.Json.Serialization;

namespace RaceFlow.Core.Models;

public sealed class FlowMapGraphOutput
{
    [JsonPropertyName("toNodeId")]
    public string ToNodeId { get; set; } = string.Empty;

    [JsonPropertyName("toNodeTitle")]
    public string ToNodeTitle { get; set; } = string.Empty;

    [JsonPropertyName("fromSocketIndex")]
    public int FromSocketIndex { get; set; }

    [JsonPropertyName("toSocketIndex")]
    public int ToSocketIndex { get; set; }
}