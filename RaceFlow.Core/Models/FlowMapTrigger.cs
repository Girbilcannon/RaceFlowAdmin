using System.Text.Json.Serialization;

namespace RaceFlow.Core.Models;

public sealed class FlowMapTrigger
{
    [JsonPropertyName("radius")]
    public double Radius { get; set; }

    [JsonPropertyName("angle")]
    public double Angle { get; set; }
}