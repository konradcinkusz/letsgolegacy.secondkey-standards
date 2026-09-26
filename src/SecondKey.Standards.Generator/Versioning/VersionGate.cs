using System.Globalization;
using SecondKey.Standards.Generator.Diagnostics;
using SecondKey.Standards.Generator.Generation;
using SecondKey.Standards.Generator.Model;

namespace SecondKey.Standards.Generator.Versioning;

/// <summary>
/// Refuses a content change that leaves a released version where it was. The version number itself
/// is a person's decision — only a person can say whether an edit reverses a rule, adds one, or only
/// rewords it — so the gate does not pick it; it enforces that somebody did. A consumer pinned to a
/// released version must get exactly what that version shipped, or the drift check is meaningless.
/// </summary>
/// <remarks>
/// A version whose changelog entry still says "Unreleased" has not been tagged, so no consumer can
/// have pinned it, and its content may change freely until it is released.
/// </remarks>
public static class VersionGate
{
    public static IReadOnlyList<Problem> Check(PackConfig pack, Manifest? committed, Manifest generated, Changelog changelog)
    {
        var problems = new List<Problem>();
        var version = pack.ParsedVersion;

        var entry = changelog.Find(version);
        if (entry is null)
        {
            problems.Add(new Problem(Changelog.RelativePath, null,
                $"there is no \"## [{version}]\" entry; every version says what changed in it"));
        }

        if (committed is null || !SemanticVersion.TryParse(committed.Version, out var committedVersion))
        {
            return problems;
        }

        if (version < committedVersion)
        {
            problems.Add(new Problem(RepositoryLayout.PackConfigRelativePath, null,
                $"version {version} is lower than the committed {GeneratedPaths.Manifest} ({committedVersion}); versions only go up"));
        }
        else if (version.CompareTo(committedVersion) == 0
            && !string.Equals(committed.ContentHash, generated.ContentHash, StringComparison.Ordinal)
            && entry is { IsReleased: true })
        {
            problems.Add(new Problem(RepositoryLayout.PackConfigRelativePath, null,
                $"the generated content changed, but version {version} is already released (CHANGELOG.md dates it {entry.ReleaseDate!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}). "
                + "Bump \"version\" (see README, \"Versioning\") and add an Unreleased entry for it to CHANGELOG.md"));
        }

        return problems;
    }
}
