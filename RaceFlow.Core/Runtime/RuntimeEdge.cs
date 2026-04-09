namespace RaceFlow.Core.Runtime;

public sealed class RuntimeEdge
{
    public string Id => $"{FromNodeId}->{ToNodeId}";

    public string FromNodeId { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;

    public int FromSocketIndex { get; set; }
    public int ToSocketIndex { get; set; }

    public RuntimeNode? FromNode { get; set; }
    public RuntimeNode? ToNode { get; set; }
}