namespace RaceFlow.Core.Runtime;

public sealed class RuntimeSegment
{
    public string Id { get; set; } = string.Empty;
    public int Index { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string PewFileName { get; set; } = string.Empty;
    public string CheckpointFileName { get; set; } = string.Empty;
}