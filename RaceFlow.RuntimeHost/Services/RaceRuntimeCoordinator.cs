using RaceFlow.Core.Runtime;
using RaceFlow.Core.Services;
using RaceFlow.Core.State;
using RaceFlow.RuntimeHost.Models;

namespace RaceFlow.RuntimeHost.Services;

public sealed class RaceRuntimeCoordinator
{
    private readonly RuntimeGraph _graph;
    private readonly RacerProgressResolver _resolver;
    private readonly Dictionary<string, RacerRuntimeState> _racers = new(StringComparer.OrdinalIgnoreCase);

    // Cheap dedupe to avoid reprocessing identical snapshots constantly.
    private readonly Dictionary<string, string> _lastPositionHashes = new(StringComparer.OrdinalIgnoreCase);

    public RaceRuntimeCoordinator(RuntimeGraph graph, RacerProgressResolver resolver)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public IReadOnlyDictionary<string, RacerRuntimeState> Racers => _racers;

    public List<RacerRuntimeState> ApplySnapshot(
        BeetleRankSnapshotMessage snapshot,
        BeetleRankTelemetryMapper mapper,
        int? sessionFilter = null)
    {
        var changed = new List<RacerRuntimeState>();

        if (snapshot?.Users is null || snapshot.Users.Count == 0)
            return changed;

        foreach (var user in snapshot.Users)
        {
            if (sessionFilter.HasValue && user.SessionCode != sessionFilter.Value)
                continue;

            var sample = mapper.MapUser(user);
            if (sample is null)
                continue;

            string hash = BuildPositionHash(user);
            if (_lastPositionHashes.TryGetValue(sample.RacerId, out var previousHash) &&
                string.Equals(previousHash, hash, StringComparison.Ordinal))
            {
                continue;
            }

            if (!_racers.TryGetValue(sample.RacerId, out var racer))
            {
                racer = new RacerRuntimeState
                {
                    RacerId = sample.RacerId,
                    RacerName = sample.RacerName
                };

                _resolver.InitializeAtStart(_graph, racer);
                _racers[sample.RacerId] = racer;
            }

            _resolver.ApplyTelemetry(_graph, racer, sample);

            _lastPositionHashes[sample.RacerId] = hash;
            changed.Add(racer);
        }

        return changed;
    }

    private static string BuildPositionHash(BeetleRankUserSnapshot user)
    {
        return $"{user.SessionCode}|{user.User}|{user.X:F3}|{user.Y:F3}|{user.Z:F3}|{user.Map}";
    }
}