namespace RaceFlow.Core.Runtime;

public sealed class RuntimeSection
{
    public string Side { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;

    // NEW: visual overrides
    public float VisualScale { get; set; } = 1.0f;
    public float OffsetX { get; set; } = 0f;
    public float OffsetY { get; set; } = 0f;
}