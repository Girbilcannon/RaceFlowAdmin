using System.Text.Json.Serialization;

namespace RaceFlow.RuntimeHost.Models;

public sealed class BeetleRankSnapshotMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("serverTimeMs")]
    public long ServerTimeMs { get; set; }

    [JsonPropertyName("activeCount")]
    public int ActiveCount { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("sessionCount")]
    public int SessionCount { get; set; }

    [JsonPropertyName("sessionCodes")]
    public List<int> SessionCodes { get; set; } = new();

    [JsonPropertyName("users")]
    public List<BeetleRankUserSnapshot> Users { get; set; } = new();
}

public sealed class BeetleRankUserSnapshot
{
    [JsonPropertyName("user")]
    public string User { get; set; } = string.Empty;

    [JsonPropertyName("sessionCode")]
    public int SessionCode { get; set; }

    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("z")]
    public double Z { get; set; }

    [JsonPropertyName("speed")]
    public double Speed { get; set; }

    [JsonPropertyName("angle")]
    public double Angle { get; set; }

    [JsonPropertyName("option")]
    public string Option { get; set; } = string.Empty;

    [JsonPropertyName("lap")]
    public int Lap { get; set; }

    [JsonPropertyName("step")]
    public int Step { get; set; }

    [JsonPropertyName("time")]
    public double Time { get; set; }

    // Pretend this is now being populated with the real map id.
    // It may arrive as "", "15", or 15 depending on server handling.
    [JsonPropertyName("map")]
    public object? Map { get; set; }

    [JsonPropertyName("color")]
    public string Color { get; set; } = string.Empty;

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("ageMs")]
    public long AgeMs { get; set; }

    [JsonPropertyName("lastSeenMs")]
    public long LastSeenMs { get; set; }

    [JsonPropertyName("origin")]
    public BeetleRankOriginSnapshot? Origin { get; set; }
}

public sealed class BeetleRankOriginSnapshot
{
    [JsonPropertyName("ip")]
    public string Ip { get; set; } = string.Empty;

    [JsonPropertyName("port")]
    public int Port { get; set; }
}