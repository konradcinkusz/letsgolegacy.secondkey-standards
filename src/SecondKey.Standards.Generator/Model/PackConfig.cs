using System.Text.Json;
using System.Text.Json.Serialization;

namespace SecondKey.Standards.Generator.Model;

/// <summary>
/// <c>catalog/pack.json</c>: the hand-authored metadata the rules are validated against.
/// The rules carry their own content; this file carries the closed vocabularies (categories,
/// known Portcullis diagnostics) so that a typo in a rule is a build failure instead of a
/// silently unmapped diagnostic.
/// </summary>
public sealed record PackConfig
{
    [JsonPropertyName("$comment")]
    public string? Comment { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("categories")]
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>
    /// The Portcullis diagnostic ids a rule may map to — the contract with the gate
    /// (component C5). A rule naming an id that is not listed here fails validation.
    /// </summary>
    [JsonPropertyName("portcullisRules")]
    public IReadOnlyList<string> PortcullisRules { get; init; } = [];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
    };

    /// <summary>Reads and checks the pack metadata; throws <see cref="PackConfigException"/> on any defect.</summary>
    public static PackConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new PackConfigException($"{path} is missing.");
        }

        PackConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<PackConfig>(File.ReadAllText(path), SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new PackConfigException($"{path} is not valid: {ex.Message}", ex);
        }

        if (config is null)
        {
            throw new PackConfigException($"{path} is empty.");
        }

        var defects = new List<string>();
        if (string.IsNullOrWhiteSpace(config.Name))
        {
            defects.Add("\"name\" is required");
        }

        if (config.Categories.Count == 0)
        {
            defects.Add("\"categories\" must list at least one category");
        }

        if (config.Categories.Distinct(StringComparer.Ordinal).Count() != config.Categories.Count)
        {
            defects.Add("\"categories\" contains a duplicate");
        }

        foreach (var id in config.PortcullisRules)
        {
            if (!Patterns.PortcullisRuleId().IsMatch(id))
            {
                defects.Add($"\"portcullisRules\" entry \"{id}\" does not match PORTCULLIS_<SLUG>");
            }
        }

        if (defects.Count > 0)
        {
            throw new PackConfigException($"{path}: {string.Join("; ", defects)}.");
        }

        return config;
    }
}

public sealed class PackConfigException : Exception
{
    public PackConfigException(string message)
        : base(message)
    {
    }

    public PackConfigException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
