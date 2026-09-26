using SecondKey.Standards.Generator.Model;

namespace SecondKey.Standards.Generator.Drift;

public enum PinKind
{
    /// <summary>A NuGet reference to the standards package: what the gate's configuration comes from.</summary>
    Package,

    /// <summary>An installed copy of the skill: what the migration agent is told.</summary>
    Skill,

    /// <summary>A version passed to the drift check directly.</summary>
    Explicit,
}

/// <summary>One place a consuming repository states which standards version it uses.</summary>
/// <param name="Location">The repository-relative file, or "--pinned-version".</param>
public sealed record Pin(PinKind Kind, string Location, SemanticVersion Version)
{
    public string Describe() => Kind switch
    {
        PinKind.Package => $"{Location} references the package at {Version}",
        PinKind.Skill => $"{Location} is the skill at {Version}",
        _ => $"{Location} {Version}",
    };
}

/// <summary>The defaults the drift check uses when a consumer does not override them.</summary>
public static class DriftDefaults
{
    /// <summary>Where releases are read from: this repository's git tags.</summary>
    public const string Source = "https://github.com/konradcinkusz/letsgolegacy.secondkey-standards";

    public const string PackageId = "SecondKey.Standards";

    public const string SkillName = "applying-dotnet-migration-standards";
}
