namespace RaceFlow.Core.Runtime;

public sealed class RuntimeGraph
{
    public string LayoutName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime ExportedAtUtc { get; set; }

    public List<RuntimeSection> Sections { get; } = new();
    public List<RuntimeSegment> Segments { get; } = new();
    public List<RuntimeNode> Nodes { get; } = new();
    public List<RuntimeEdge> Edges { get; } = new();

    public Dictionary<string, RuntimeNode> NodesById { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<RuntimeNode> StartNodes { get; } = new();
    public List<RuntimeNode> FinishNodes { get; } = new();

    public HashSet<int> MapIds { get; } = new();
}