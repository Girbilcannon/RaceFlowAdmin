using System.Text.Json.Serialization;

namespace RaceFlow.Core.Models;

public sealed class FlowMapBinding
{
    [JsonPropertyName("isBound")]
    public bool IsBound { get; set; }

    [JsonPropertyName("ignoreNode")]
    public bool IgnoreNode { get; set; }

    [JsonPropertyName("isEndOfRace")]
    public bool IsEndOfRace { get; set; }

    [JsonPropertyName("endOfRaceMode")]
    public string? EndOfRaceMode { get; set; }

    [JsonPropertyName("outputOverrideNodeIds")]
    public List<string> OutputOverrideNodeIds { get; set; } = new();
}