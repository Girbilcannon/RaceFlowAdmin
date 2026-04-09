using System.Text.Json;
using RaceFlow.Core.Models;

namespace RaceFlow.Core.Services;

public sealed class FlowMapLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public FlowMapDocument LoadFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path is required.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Flow map JSON file not found.", filePath);

        string json = File.ReadAllText(filePath);

        FlowMapDocument? document = JsonSerializer.Deserialize<FlowMapDocument>(json, JsonOptions);

        if (document is null)
            throw new InvalidOperationException("Failed to deserialize flow map document.");

        return document;
    }
}