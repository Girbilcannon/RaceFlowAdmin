using System.Text.Json.Serialization;

namespace RaceFlow.Core.Models;

public sealed class FlowMapGraphInput
{
    [JsonPropertyName("fromNodeId")]
    public string FromNodeId { get; set; } = string.Empty;

    [JsonPropertyName("fromNodeTitle")]
    public string FromNodeTitle { get; set; } = string.Empty;

    [JsonPropertyName("fromSocketIndex")]
    public int FromSocketIndex { get; set; }

    [JsonPropertyName("toSocketIndex")]
    public int ToSocketIndex { get; set; }
}