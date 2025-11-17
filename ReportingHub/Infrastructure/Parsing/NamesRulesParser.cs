using ReportingHub.Api.Contracts;
using ReportingHub.Api.Contracts.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReportingHub.Api.Infrastructure.Parsing;

public static class NamesRulesParser
{
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static NamesRules Parse(string? namesField)
    {
        if (string.IsNullOrWhiteSpace(namesField))
            return Defaults();

        try
        {
            var rules = JsonSerializer.Deserialize<NamesRules>(namesField, _jsonOpts) ?? new NamesRules();

            rules.Pattern ??= "{file}_{sheet}";

            if (rules.Mode == MergeMode.Map && (rules.Map is null || rules.Map.Count == 0))
            {
                rules.Mode = MergeMode.Pattern;
                rules.Pattern = "{file}_{sheet}";
                rules.Map = null;
            }

            return rules;
        }
        catch
        {
            return Defaults();
        }
    }

    private static NamesRules Defaults() => new()
    {
        Mode = MergeMode.Pattern,
        Pattern = "{file}_{sheet}",
        Collision = CollisionPolicy.Dedupe
    };
}
