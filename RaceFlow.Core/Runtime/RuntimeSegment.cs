namespace RaceFlow.Core.Runtime;

public sealed class RuntimeSegment
{
    public string Id { get; set; } = string.Empty;
    public int Index { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;

    public float VisualScale { get; set; } = 1.0f;
    public float OffsetX { get; set; } = 0f;
    public float OffsetY { get; set; } = 0f;

    public string PewFileName { get; set; } = string.Empty;
    public string CheckpointFileName { get; set; } = string.Empty;
}