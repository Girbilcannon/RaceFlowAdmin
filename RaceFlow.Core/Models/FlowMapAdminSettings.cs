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
}
