using ReportingHub.Api.Contracts.Enums;
using System.Text.Json.Serialization;

namespace ReportingHub.Api.Contracts;

public sealed class NamesRules
{
    [JsonConverter(typeof(JsonStringEnumConverter))] //accepts "pattern" or "map"
    public MergeMode Mode { get; set; } = MergeMode.Pattern;

    [JsonConverter(typeof(JsonStringEnumConverter))] //accepts "dedupe" or "error"
    public CollisionPolicy Collision { get; set; } = CollisionPolicy.Dedupe;

    /// <summary>Used when Mode == Pattern. Supports tokens like {file}, {sheet}.</summary>
    public string? Pattern { get; set; } = "{file}_{sheet}";

    /// <summary>Used when Mode == Map. Keys: file names; inner keys: sheet names (or "*").</summary>
    public Dictionary<string, Dictionary<string, string>>? Map { get; set; }
}
