using System.Text.Json;
using RaceFlow.RuntimeHost.Models;

namespace RaceFlow.RuntimeHost.Services;

public sealed class ReplaySnapshotProvider
{
    private readonly List<string> _lines;
    private int _index;

    // Map flow for your test race
    private readonly int[] _mapFlow = new[] { 15, 23, 34 };

    public ReplaySnapshotProvider(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Replay file not found", filePath);

        _lines = File.ReadAllLines(filePath)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
    }

    public BeetleRankSnapshotMessage? GetNextSnapshot()
    {
        if (_lines.Count == 0)
            return null;

        string line = _lines[_index];
        _index = (_index + 1) % _lines.Count;

        var snapshot = JsonSerializer.Deserialize<BeetleRankSnapshotMessage>(
            line,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (snapshot == null)
            return null;

        InjectMap(snapshot);

        return snapshot;
    }

    private void InjectMap(BeetleRankSnapshotMessage snapshot)
    {
        // Very simple phase-based mapping
        int phase = (_index * _mapFlow.Length) / _lines.Count;
        if (phase >= _mapFlow.Length)
            phase = _mapFlow.Length - 1;

        int mapId = _mapFlow[phase];

        foreach (var user in snapshot.Users)
        {
            user.Map = mapId;
        }
    }
}