using System.Text.Json.Serialization;

namespace RaceFlow.Core.Models;

public sealed class FlowMapGraph
{
    [JsonPropertyName("inputCount")]
    public int InputCount { get; set; }

    [JsonPropertyName("outputCount")]
    public int OutputCount { get; set; }

    [JsonPropertyName("graphInputs")]
    public List<FlowMapGraphInput> GraphInputs { get; set; } = new();

    [JsonPropertyName("graphOutputs")]
    public List<FlowMapGraphOutput> GraphOutputs { get; set; } = new();

    [JsonPropertyName("runtimeOutputNodeIds")]
    public List<string> RuntimeOutputNodeIds { get; set; } = new();
}