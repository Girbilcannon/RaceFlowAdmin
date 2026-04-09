using RaceFlow.Core.Enums;
using RaceFlow.Core.Runtime;
using RaceFlow.Core.State;

namespace RaceFlow.Core.Services;

public sealed class RacerProgressResolver
{
    // Tight re-anchor rule:
    // racer must be extremely close to checkpoint center to snap to it.
    // Uses the smaller of:
    // - 35% of authored trigger radius
    // - or a hard cap of 6 world units
    private const double ReanchorRadiusFactor = 0.35;
    private const double ReanchorRadiusMax = 6.0;

    public void InitializeAtStart(RuntimeGraph graph, RacerRuntimeState racer)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(racer);

        var firstStart = graph.StartNodes.FirstOrDefault();
        if (firstStart is null)
            throw new InvalidOperationException("Graph has no start node.");

        racer.LastConfirmedNode = firstStart;
        racer.CurrentTargetNode = firstStart.Outgoing.FirstOrDefault()?.ToNode;
        racer.CurrentEdge = firstStart.Outgoing.FirstOrDefault();
        racer.EdgeProgress = 0;
        racer.StatusText = "Initialized";

        ClearBranchLock(racer);
        ClearPendingSplit(racer);
        AddHistory(racer, firstStart);
    }

    public void ApplyTelemetry(RuntimeGraph graph, RacerRuntimeState racer, RacerTelemetrySample sample)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(racer);
        ArgumentNullException.ThrowIfNull(sample);

        racer.CurrentMapId = sample.MapId;
        racer.WorldX = sample.X;
        racer.WorldY = sample.Y;
        racer.WorldZ = sample.Z;
        racer.LastUpdateUtc = sample.TimestampUtc;

        if (racer.LastConfirmedNode is null)
        {
            InitializeAtStart(graph, racer);
        }

        if (racer.LastConfirmedNode is null)
            return;

        // Global strict re-anchor pass:
        // If the racer is very close to any valid checkpoint on this map,
        // allow snapping there, even if it is backward or forward.
        var reanchorNode = FindBestReanchorNode(graph, racer, sample);
        if (reanchorNode is not null &&
            !string.Equals(reanchorNode.Id, racer.LastConfirmedNode.Id, StringComparison.OrdinalIgnoreCase))
        {
            ReanchorToNode(racer, reanchorNode);
            return;
        }

        // If racer is in pending split state, only child entry checkpoints can resolve it.
        if (racer.AwaitingBranchDecision && racer.PendingSplitNode is not null)
        {
            HandlePendingSplitDecision(racer, sample);
            return;
        }

        if (racer.CurrentTargetNode is null)
        {
            racer.StatusText = "No target node";
            return;
        }

        // Normal forward confirmation
        if (IsInsideTrigger(sample, racer.CurrentTargetNode))
        {
            ConfirmNode(racer, racer.CurrentTargetNode, $"Confirmed {racer.CurrentTargetNode.Label}");
            return;
        }

        // Otherwise, stay on current edge and update interpolated progress.
        racer.EdgeProgress = ComputeEdgeProgress(
            racer.LastConfirmedNode,
            racer.CurrentTargetNode,
            sample);

        racer.StatusText = $"Moving toward {racer.CurrentTargetNode.Label}";
    }

    private void ConfirmNode(RacerRuntimeState racer, RuntimeNode confirmedNode, string statusText)
    {
        racer.LastConfirmedNode = confirmedNode;
        AddHistory(racer, confirmedNode);

        if (confirmedNode.NodeType == FlowNodeType.Converge)
        {
            ClearBranchLock(racer);
        }

        if (confirmedNode.NodeType == FlowNodeType.Split)
        {
            EnterPendingSplit(racer, confirmedNode);
            racer.CurrentTargetNode = null;
            racer.CurrentEdge = null;
            racer.EdgeProgress = 0;
            racer.StatusText = $"{statusText} - awaiting branch decision";
            return;
        }

        if (confirmedNode.Outgoing.Count == 0)
        {
            racer.CurrentTargetNode = null;
            racer.CurrentEdge = null;
            racer.EdgeProgress = 1.0;
            racer.HasFinished = true;
            racer.StatusText = "Finished";
            return;
        }

        var nextEdge = confirmedNode.Outgoing.FirstOrDefault();
        racer.CurrentEdge = nextEdge;
        racer.CurrentTargetNode = nextEdge?.ToNode;
        racer.EdgeProgress = 0;
        racer.StatusText = statusText;
    }

    private void HandlePendingSplitDecision(RacerRuntimeState racer, RacerTelemetrySample sample)
    {
        foreach (var candidate in racer.CandidateBranchEntryNodes)
        {
            if (IsInsideTrigger(sample, candidate))
            {
                racer.LastConfirmedNode = candidate;
                AddHistory(racer, candidate);

                racer.ActiveBranchRootNode = racer.PendingSplitNode;
                racer.ActiveBranchEntryNode = candidate;
                racer.BranchLocked = true;
                racer.BranchLockReason = $"Locked by first child checkpoint {candidate.Label}";

                ClearPendingSplit(racer);

                if (candidate.Outgoing.Count == 0)
                {
                    racer.CurrentTargetNode = null;
                    racer.CurrentEdge = null;
                    racer.EdgeProgress = 1.0;
                    racer.HasFinished = true;
                    racer.StatusText = "Finished";
                    return;
                }

                var nextEdge = candidate.Outgoing.FirstOrDefault();
                racer.CurrentEdge = nextEdge;
                racer.CurrentTargetNode = nextEdge?.ToNode;
                racer.EdgeProgress = 0;
                racer.StatusText = $"Branch confirmed at {candidate.Label}";
                return;
            }
        }

        racer.CurrentTargetNode = null;
        racer.CurrentEdge = null;
        racer.EdgeProgress = 0;
        racer.StatusText = $"Awaiting branch decision at {racer.PendingSplitNode?.Label}";
    }

    private RuntimeNode? FindBestReanchorNode(RuntimeGraph graph, RacerRuntimeState racer, RacerTelemetrySample sample)
    {
        RuntimeNode? bestNode = null;
        double bestDistance = double.MaxValue;

        foreach (var node in graph.Nodes)
        {
            if (!node.IsBound)
                continue;

            if (!node.MapId.HasValue || node.MapId.Value != sample.MapId)
                continue;

            double distance = Distance3D(
                sample.X, sample.Y, sample.Z,
                node.WorldX, node.WorldY, node.WorldZ);

            double reanchorRadius = GetReanchorRadius(node);

            if (distance > reanchorRadius)
                continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestNode = node;
            }
        }

        return bestNode;
    }

    private void ReanchorToNode(RacerRuntimeState racer, RuntimeNode node)
    {
        ClearPendingSplit(racer);

        // Rebuild lock state based on where we landed.
        RebuildBranchStateForNode(racer, node);

        racer.LastConfirmedNode = node;
        AddHistory(racer, node);

        if (node.NodeType == FlowNodeType.Split)
        {
            EnterPendingSplit(racer, node);
            racer.CurrentTargetNode = null;
            racer.CurrentEdge = null;
            racer.EdgeProgress = 0;
            racer.StatusText = $"Re-anchored to split {node.Label} - awaiting branch decision";
            return;
        }

        if (node.Outgoing.Count == 0)
        {
            racer.CurrentTargetNode = null;
            racer.CurrentEdge = null;
            racer.EdgeProgress = 1.0;
            racer.HasFinished = true;
            racer.StatusText = $"Re-anchored to finish node {node.Label}";
            return;
        }

        var nextEdge = node.Outgoing.FirstOrDefault();
        racer.CurrentEdge = nextEdge;
        racer.CurrentTargetNode = nextEdge?.ToNode;
        racer.EdgeProgress = 0;
        racer.StatusText = $"Re-anchored to {node.Label}";
    }

    private void RebuildBranchStateForNode(RacerRuntimeState racer, RuntimeNode node)
    {
        ClearBranchLock(racer);

        // If this is a direct child of a split, it is a branch-entry checkpoint.
        if (node.Incoming.Count == 1 && node.Incoming[0].FromNode?.NodeType == FlowNodeType.Split)
        {
            racer.ActiveBranchRootNode = node.Incoming[0].FromNode;
            racer.ActiveBranchEntryNode = node;
            racer.BranchLocked = true;
            racer.BranchLockReason = $"Rebuilt from branch entry node {node.Label}";
            return;
        }

        // If deeper in a branch, walk backward to find a split ancestor.
        var splitAncestor = FindNearestSplitAncestor(node, out var entryNode);
        if (splitAncestor is not null && entryNode is not null)
        {
            racer.ActiveBranchRootNode = splitAncestor;
            racer.ActiveBranchEntryNode = entryNode;
            racer.BranchLocked = true;
            racer.BranchLockReason = $"Rebuilt from descendant node {node.Label}";
        }
    }

    private RuntimeNode? FindNearestSplitAncestor(RuntimeNode node, out RuntimeNode? entryNode)
    {
        entryNode = null;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(RuntimeNode Node, RuntimeNode? FirstDescendant)>();

        queue.Enqueue((node, null));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current.Node.Id))
                continue;

            foreach (var incoming in current.Node.Incoming)
            {
                var parent = incoming.FromNode;
                if (parent is null)
                    continue;

                if (parent.NodeType == FlowNodeType.Split)
                {
                    entryNode = current.Node;
                    return parent;
                }

                queue.Enqueue((parent, current.Node));
            }
        }

        return null;
    }

    private void EnterPendingSplit(RacerRuntimeState racer, RuntimeNode splitNode)
    {
        ClearPendingSplit(racer);

        racer.PendingSplitNode = splitNode;
        racer.AwaitingBranchDecision = true;

        foreach (var edge in splitNode.Outgoing)
        {
            if (edge.ToNode is not null)
            {
                racer.CandidateBranchEntryNodes.Add(edge.ToNode);
            }
        }
    }

    private bool IsInsideTrigger(RacerTelemetrySample sample, RuntimeNode node)
    {
        if (node.MapId.HasValue && node.MapId.Value != sample.MapId)
            return false;

        double distance = Distance3D(
            sample.X, sample.Y, sample.Z,
            node.WorldX, node.WorldY, node.WorldZ);

        return distance <= node.TriggerRadius;
    }

    private double GetReanchorRadius(RuntimeNode node)
    {
        double authored = Math.Max(0, node.TriggerRadius);
        double scaled = authored * ReanchorRadiusFactor;

        if (scaled <= 0)
            scaled = ReanchorRadiusMax;

        return Math.Min(scaled, ReanchorRadiusMax);
    }

    private double ComputeEdgeProgress(RuntimeNode fromNode, RuntimeNode toNode, RacerTelemetrySample sample)
    {
        double ax = fromNode.WorldX;
        double ay = fromNode.WorldY;
        double az = fromNode.WorldZ;

        double bx = toNode.WorldX;
        double by = toNode.WorldY;
        double bz = toNode.WorldZ;

        double px = sample.X;
        double py = sample.Y;
        double pz = sample.Z;

        double abx = bx - ax;
        double aby = by - ay;
        double abz = bz - az;

        double apx = px - ax;
        double apy = py - ay;
        double apz = pz - az;

        double abLengthSquared = (abx * abx) + (aby * aby) + (abz * abz);
        if (abLengthSquared <= 0.000001)
            return 0;

        double dot = (apx * abx) + (apy * aby) + (apz * abz);
        double t = dot / abLengthSquared;

        if (t < 0) t = 0;
        if (t > 1) t = 1;

        return t;
    }

    private double Distance3D(double ax, double ay, double az, double bx, double by, double bz)
    {
        double dx = bx - ax;
        double dy = by - ay;
        double dz = bz - az;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private void AddHistory(RacerRuntimeState racer, RuntimeNode node)
    {
        if (racer.ConfirmationHistory.Count == 0 ||
            !string.Equals(racer.ConfirmationHistory[^1].Id, node.Id, StringComparison.OrdinalIgnoreCase))
        {
            racer.ConfirmationHistory.Add(node);
        }

        if (racer.ConfirmationHistory.Count > 20)
        {
            racer.ConfirmationHistory.RemoveAt(0);
        }
    }

    private void ClearBranchLock(RacerRuntimeState racer)
    {
        racer.ActiveBranchRootNode = null;
        racer.ActiveBranchEntryNode = null;
        racer.BranchLocked = false;
        racer.BranchLockReason = string.Empty;
    }

    private void ClearPendingSplit(RacerRuntimeState racer)
    {
        racer.PendingSplitNode = null;
        racer.AwaitingBranchDecision = false;
        racer.CandidateBranchEntryNodes.Clear();
    }
}