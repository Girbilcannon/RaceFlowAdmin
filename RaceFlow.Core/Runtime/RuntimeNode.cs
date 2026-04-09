using RaceFlow.Core.Enums;

namespace RaceFlow.Core.Runtime;

public sealed class RuntimeNode
{
    public string Id { get; set; } = string.Empty;
    public int Index { get; set; }
    public string Label { get; set; } = string.Empty;
    public string RuntimeLabel { get; set; } = string.Empty;
    public FlowNodeType NodeType { get; set; }

    public string SectionSideRaw { get; set; } = string.Empty;
    public string SegmentId { get; set; } = string.Empty;
    public string SegmentLabel { get; set; } = string.Empty;

    public float OverlayX { get; set; }
    public float OverlayY { get; set; }

    public bool IgnoreNode { get; set; }
    public bool IsBound { get; set; }
    public bool IsEndOfRace { get; set; }

    public int? MapId { get; set; }

    public double WorldX { get; set; }
    public double WorldY { get; set; }
    public double WorldZ { get; set; }

    public double TriggerRadius { get; set; }
    public double TriggerAngle { get; set; }

    public List<RuntimeEdge> Incoming { get; } = new();
    public List<RuntimeEdge> Outgoing { get; } = new();
}