namespace RaceFlow.Core.State;

public sealed class RacerTelemetrySample
{
    public string RacerId { get; set; } = string.Empty;
    public string RacerName { get; set; } = string.Empty;

    public int MapId { get; set; }

    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}