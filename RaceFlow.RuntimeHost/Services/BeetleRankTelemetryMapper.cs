using System.Text.Json;
using RaceFlow.Core.State;
using RaceFlow.RuntimeHost.Models;

namespace RaceFlow.RuntimeHost.Services;

public sealed class BeetleRankTelemetryMapper
{
    public RacerTelemetrySample? MapUser(BeetleRankUserSnapshot user)
    {
        if (user is null)
            return null;

        if (string.IsNullOrWhiteSpace(user.User))
            return null;

        int mapId = ParseMapId(user.Map);
        if (mapId <= 0)
            return null;

        return new RacerTelemetrySample
        {
            RacerId = BuildRacerKey(user),
            RacerName = user.User,
            MapId = mapId,
            X = user.X,
            Y = user.Y,
            Z = user.Z,
            TimestampUtc = FromUnixMilliseconds(user.LastSeenMs)
        };
    }

    public static string BuildRacerKey(BeetleRankUserSnapshot user)
    {
        return $"{user.SessionCode}:{user.User}";
    }

    private static int ParseMapId(object? mapValue)
    {
        if (mapValue is null)
            return 0;

        if (mapValue is int i)
            return i;

        if (mapValue is long l && l > 0 && l <= int.MaxValue)
            return (int)l;

        if (mapValue is JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out int numericMap))
                return numericMap;

            if (json.ValueKind == JsonValueKind.String)
            {
                string? s = json.GetString();
                if (int.TryParse(s, out int parsed))
                    return parsed;
            }

            return 0;
        }

        string text = mapValue.ToString() ?? string.Empty;
        return int.TryParse(text, out int result) ? result : 0;
    }

    private static DateTime FromUnixMilliseconds(long unixMs)
    {
        if (unixMs <= 0)
            return DateTime.UtcNow;

        return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime;
    }
}