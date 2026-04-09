using System.Text.Json.Serialization;

namespace RaceFlow.Core.Models;

public sealed class FlowMapDocument
{
    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("layoutName")]
    public string LayoutName { get; set; } = string.Empty;

    [JsonPropertyName("exportedAtUtc")]
    public DateTime ExportedAtUtc { get; set; }

    [JsonPropertyName("sections")]
    public List<FlowMapSection> Sections { get; set; } = new();

    [JsonPropertyName("admin")]
    public FlowMapAdminSettings? Admin { get; set; }
}