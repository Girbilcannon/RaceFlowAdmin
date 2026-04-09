using System.Text.Json;
using RaceFlow.Core.Services;
using RaceFlow.RuntimeHost.Models;
using RaceFlow.RuntimeHost.Services;

string filePath = Path.Combine(AppContext.BaseDirectory, "Samples", "flowmap.json");

// Set this to the live session/event code on race day.
// Leave null for now to observe everything.
int? sessionFilter = null;

// WebSocket (used when NOT in replay mode)
string socketUrl = "wss://www.beetlerank.com:3002";

// Toggle replay mode
bool useReplay = true;

// Replay file
string replayFile = Path.Combine(AppContext.BaseDirectory, "Samples", "replay.jsonl");

try
{
    var loader = new FlowMapLoader();
    var builder = new FlowMapGraphBuilder();
    var validator = new FlowMapValidator();
    var resolver = new RacerProgressResolver();
    var mapper = new BeetleRankTelemetryMapper();

    var document = loader.LoadFromFile(filePath);
    var graph = builder.Build(document);
    var issues = validator.Validate(graph);
    var summary = validator.BuildSummary(graph);

    Console.WriteLine("FLOW MAP LOAD SUCCESS");
    Console.WriteLine(new string('-', 60));
    Console.WriteLine(summary);

    if (issues.Count == 0)
    {
        Console.WriteLine("Validation: OK");
    }
    else
    {
        Console.WriteLine("Validation issues:");
        foreach (var issue in issues)
            Console.WriteLine($" - {issue}");
    }

    Console.WriteLine();
    Console.WriteLine(new string('=', 60));
    Console.WriteLine("RUNTIME MODE");
    Console.WriteLine(new string('=', 60));
    Console.WriteLine(useReplay ? "REPLAY MODE" : "LIVE MODE");
    Console.WriteLine();

    var coordinator = new RaceRuntimeCoordinator(graph, resolver);

    // ------------------------------------------------------------
    // REPLAY MODE
    // ------------------------------------------------------------
    if (useReplay)
    {
        var replay = new ReplaySnapshotProvider(replayFile);

        while (true)
        {
            var snapshot = replay.GetNextSnapshot();
            if (snapshot == null)
                continue;

            var changed = coordinator.ApplySnapshot(snapshot, mapper, sessionFilter);

            if (changed.Count > 0)
            {
                Console.WriteLine(new string('-', 80));
                Console.WriteLine($"[REPLAY] users={snapshot.Users.Count} changed={changed.Count}");

                foreach (var racer in changed.OrderBy(r => r.RacerName))
                {
                    PrintRacerState(racer);
                }
            }

            await Task.Delay(100); // simulate ~10 FPS
        }
    }
    // ------------------------------------------------------------
    // LIVE MODE (WebSocket)
    // ------------------------------------------------------------
    else
    {
        var socketClient = new BeetleRankSocketClient();

        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Console.WriteLine();
            Console.WriteLine("Cancellation requested. Closing WebSocket...");
        };

        await socketClient.RunAsync(
            new Uri(socketUrl),
            async rawMessage =>
            {
                try
                {
                    var snapshot = JsonSerializer.Deserialize<BeetleRankSnapshotMessage>(
                        rawMessage,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    if (snapshot is null)
                        return;

                    if (!string.Equals(snapshot.Type, "snapshot", StringComparison.OrdinalIgnoreCase))
                        return;

                    var changed = coordinator.ApplySnapshot(snapshot, mapper, sessionFilter);

                    if (changed.Count == 0)
                        return;

                    Console.WriteLine(new string('-', 80));
                    Console.WriteLine($"[LIVE] users={snapshot.Users.Count} changed={changed.Count}");

                    foreach (var racer in changed.OrderBy(r => r.RacerName))
                    {
                        PrintRacerState(racer);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to process snapshot:");
                    Console.WriteLine(ex.Message);
                }

                await Task.CompletedTask;
            },
            cts.Token);
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation canceled.");
}
catch (Exception ex)
{
    Console.WriteLine("Runtime failed.");
    Console.WriteLine(ex);
}

Console.WriteLine();
Console.WriteLine("Press any key to close...");
Console.ReadKey();


// ------------------------------------------------------------
// DEBUG PRINT
// ------------------------------------------------------------
static void PrintRacerState(RaceFlow.Core.State.RacerRuntimeState racer)
{
    string edgeText = racer.CurrentEdge is null
        ? "null"
        : $"{racer.CurrentEdge.FromNode?.Label ?? racer.CurrentEdge.FromNodeId} -> {racer.CurrentEdge.ToNode?.Label ?? racer.CurrentEdge.ToNodeId}";

    string branchText = racer.BranchLocked
        ? $"{racer.ActiveBranchRootNode?.Label ?? "null"} -> {racer.ActiveBranchEntryNode?.Label ?? "null"}"
        : "none";

    string pendingText = racer.AwaitingBranchDecision
        ? $"{racer.PendingSplitNode?.Label ?? "null"}"
        : "none";

    Console.WriteLine(
        $"Racer={racer.RacerName} | " +
        $"LastConfirmed={(racer.LastConfirmedNode?.Label ?? "null")} | " +
        $"Target={(racer.CurrentTargetNode?.Label ?? "null")} | " +
        $"Edge={edgeText} | " +
        $"Progress={racer.EdgeProgress:F3} | " +
        $"PendingSplit={pendingText} | " +
        $"BranchLocked={racer.BranchLocked} | " +
        $"Branch={branchText} | " +
        $"Status={racer.StatusText}");
}