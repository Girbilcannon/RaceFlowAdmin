using System.Text.Json.Serialization;

namespace RaceFlow.Core.Models;

public sealed class FlowMapTelemetry
{
    [JsonPropertyName("hasCheckpoint")]
    public bool HasCheckpoint { get; set; }

    [JsonPropertyName("step")]
    public int Step { get; set; }

    [JsonPropertyName("checkpointLabel")]
    public string CheckpointLabel { get; set; } = string.Empty;

    [JsonPropertyName("mapId")]
    public string MapId { get; set; } = string.Empty;

    [JsonPropertyName("position")]
    public FlowMapVector3? Position { get; set; }

    [JsonPropertyName("trigger")]
    public FlowMapTrigger? Trigger { get; set; }

    [JsonPropertyName("note")]
    public string Note { get; set; } = string.Empty;

    [JsonPropertyName("checkpointType")]
    public string CheckpointType { get; set; } = string.Empty;
}