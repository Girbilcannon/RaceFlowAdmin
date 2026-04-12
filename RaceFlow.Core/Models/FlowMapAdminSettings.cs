using System.Text.Json.Serialization;

namespace RaceFlow.Core.Models;

public sealed class FlowMapAdminSettings
{
    [JsonPropertyName("globalTriggerScale")]
    public double GlobalTriggerScale { get; set; } = 1.0;

    [JsonPropertyName("themeFile")]
    public string ThemeFile { get; set; } = string.Empty;

    [JsonPropertyName("playbackDelayMs")]
    public int PlaybackDelayMs { get; set; } = 0;

    [JsonPropertyName("outputScale")]
    public double OutputScale { get; set; } = 1.0;

    [JsonPropertyName("outputOffsetX")]
    public float OutputOffsetX { get; set; } = 0f;

    [JsonPropertyName("outputOffsetY")]
    public float OutputOffsetY { get; set; } = 0f;

    [JsonPropertyName("outputNodeTextScale")]
    public double OutputNodeTextScale { get; set; } = 1.0;

    [JsonPropertyName("outputRacerTextScale")]
    public double OutputRacerTextScale { get; set; } = 1.0;
}