using RaceFlow.Core.Runtime;

namespace RaceFlow.Core.State;

public sealed class RacerRuntimeState
{
    public string RacerId { get; set; } = string.Empty;
    public string RacerName { get; set; } = string.Empty;

    public RuntimeNode? LastConfirmedNode { get; set; }
    public RuntimeNode? CurrentTargetNode { get; set; }

    public RuntimeEdge? CurrentEdge { get; set; }

    public double EdgeProgress { get; set; }

    public int CurrentMapId { get; set; }

    public double WorldX { get; set; }
    public double WorldY { get; set; }
    public double WorldZ { get; set; }

    public DateTime LastUpdateUtc { get; set; }

    public bool HasFinished { get; set; }

    public string StatusText { get; set; } = "Waiting";

    // Confirmed branch lock state
    public RuntimeNode? ActiveBranchRootNode { get; set; }
    public RuntimeNode? ActiveBranchEntryNode { get; set; }
    public bool BranchLocked { get; set; }
    public string BranchLockReason { get; set; } = string.Empty;

    // Pending split state
    public RuntimeNode? PendingSplitNode { get; set; }
    public bool AwaitingBranchDecision { get; set; }
    public List<RuntimeNode> CandidateBranchEntryNodes { get; } = new();

    // Recent confirmation history for future admin/debug use
    public List<RuntimeNode> ConfirmationHistory { get; } = new();
}