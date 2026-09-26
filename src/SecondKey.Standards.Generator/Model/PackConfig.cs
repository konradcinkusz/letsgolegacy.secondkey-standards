using System.Text.Json;
using System.Text.Json.Serialization;

namespace SecondKey.Standards.Generator.Model;

/// <summary>
/// <c>catalog/pack.json</c>: the hand-authored metadata of the standards pack. The rules carry
/// their own content; this file carries what is true of the pack as a whole — its version, what the
/// skill and the NuGet package are called — and the closed vocabularies (categories, known
/// Portcullis diagnostics) the rules are validated against, so that a typo in a rule is a build
/// failure instead of a silently unmapped diagnostic.
/// </summary>
public sealed record PackConfig
{
    [JsonPropertyName("$comment")]
    public string? Comment { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    /// <summary>
    /// The standards version, set by hand. It is what a git tag (<c>v0.1.0</c>), the NuGet package
    /// and the skill's metadata carry, and what a consuming repository pins.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "";

    /// <summary>The repository's URL, used for links in generated files.</summary>
    [JsonPropertyName("repository")]
    public string Repository { get; init; } = "";

    /// <summary>
    /// The architecture constitution the <c>SK-ARCH-</c> rules restate; a rule's <c>principle</c>
    /// links to its anchor here (<c>#p4</c>).
    /// </summary>
    [JsonPropertyName("constitution")]
    public string Constitution { get; init; } = "";

    [JsonPropertyName("skill")]
    public SkillConfig Skill { get; init; } = new();

    [JsonPropertyName("package")]
    public PackageConfig Package { get; init; } = new();

    [JsonPropertyName("categories")]
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>
    /// The Portcullis diagnostic ids a rule may map to — the contract with the gate
    /// (component C5). A rule naming an id that is not listed here fails validation.
    /// </summary>
    [JsonPropertyName("portcullisRules")]
    public IReadOnlyList<string> PortcullisRules { get; init; } = [];

    [JsonIgnore]
    public SemanticVersion ParsedVersion => SemanticVersion.Parse(Version);

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

        var defects = config.Validate();
        if (defects.Count > 0)
        {
            throw new PackConfigException($"{path}: {string.Join("; ", defects)}.");
        }

        return config;
    }

    internal List<string> Validate()
    {
        var defects = new List<string>();
        if (string.IsNullOrWhiteSpace(Name))
        {
            defects.Add("\"name\" is required");
        }

        if (!SemanticVersion.TryParse(Version, out var version) || version.IsPrerelease)
        {
            defects.Add($"\"version\" \"{Version}\" must be MAJOR.MINOR.PATCH");
        }

        if (!Uri.TryCreate(Repository, UriKind.Absolute, out var repository) || repository.Scheme != Uri.UriSchemeHttps)
        {
            defects.Add("\"repository\" must be the repository's https URL");
        }

        if (!Uri.TryCreate(Constitution, UriKind.Absolute, out var constitution) || constitution.Scheme != Uri.UriSchemeHttps)
        {
            defects.Add("\"constitution\" must be the https URL of the architecture constitution");
        }

        defects.AddRange(Skill.Validate());
        defects.AddRange(Package.Validate());

        if (Categories.Count == 0)
        {
            defects.Add("\"categories\" must list at least one category");
        }

        if (Categories.Distinct(StringComparer.Ordinal).Count() != Categories.Count)
        {
            defects.Add("\"categories\" contains a duplicate");
        }

        foreach (var id in PortcullisRules)
        {
            if (!Patterns.PortcullisRuleId().IsMatch(id))
            {
                defects.Add($"\"portcullisRules\" entry \"{id}\" does not match PORTCULLIS_<SLUG>");
            }
        }

        return defects;
    }
}

/// <summary>How the skill is named and routed. The constraints are the Agent Skills specification's.</summary>
public sealed record SkillConfig
{
    public const int MaxNameLength = 64;
    public const int MaxDescriptionLength = 1024;

    /// <summary>
    /// The skill's name: the directory name it is installed under and the <c>name</c> the agent
    /// sees. Lower-case letters, digits and single hyphens, at most 64 characters.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    /// <summary>The router: what the skill does and when the agent should load it. At most 1024 characters.</summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    /// <summary>
    /// GitHub Copilot upgrade's loading strategy (<c>metadata.discovery</c>): <c>preload</c> keeps
    /// the skill available to every task; <c>lazy</c> loads it when the description matches.
    /// </summary>
    [JsonPropertyName("discovery")]
    public string Discovery { get; init; } = "";

    /// <summary>GitHub Copilot upgrade's technology tags (<c>metadata.traits</c>), pipe-separated.</summary>
    [JsonPropertyName("traits")]
    public string Traits { get; init; } = "";

    internal IEnumerable<string> Validate()
    {
        if (!Patterns.SkillName().IsMatch(Name) || Name.Length > MaxNameLength)
        {
            yield return $"\"skill.name\" \"{Name}\" must be lower-case letters, digits and single hyphens, at most {MaxNameLength} characters";
        }

        if (Name.Contains("claude", StringComparison.Ordinal) || Name.Contains("anthropic", StringComparison.Ordinal))
        {
            yield return "\"skill.name\" must not contain a reserved word (claude, anthropic)";
        }

        if (string.IsNullOrWhiteSpace(Description) || Description.Length > MaxDescriptionLength)
        {
            yield return $"\"skill.description\" must be 1 to {MaxDescriptionLength} characters (it is {Description.Length})";
        }

        if (Description.IndexOfAny(['<', '>']) >= 0)
        {
            yield return "\"skill.description\" must not contain < or > (skill descriptions may not contain XML tags)";
        }

        if (Discovery is not ("preload" or "lazy"))
        {
            yield return "\"skill.discovery\" must be \"preload\" or \"lazy\"";
        }

        if (string.IsNullOrWhiteSpace(Traits))
        {
            yield return "\"skill.traits\" is required, for example \".NET|CSharp\"";
        }
    }
}

/// <summary>The NuGet package that delivers the analyzer configuration to consuming builds.</summary>
public sealed record PackageConfig
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    internal IEnumerable<string> Validate()
    {
        if (!Patterns.PackageId().IsMatch(Id))
        {
            yield return $"\"package.id\" \"{Id}\" is not a NuGet package id";
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            yield return "\"package.description\" is required";
        }
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
