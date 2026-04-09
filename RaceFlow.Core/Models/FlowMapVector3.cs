using System.Text.Json.Serialization;

namespace RaceFlow.Core.Models;

public sealed class FlowMapVector3
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("z")]
    public double Z { get; set; }
}