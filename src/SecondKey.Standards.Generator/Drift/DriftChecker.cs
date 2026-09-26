using SecondKey.Standards.Generator.Generation;
using SecondKey.Standards.Generator.Model;
using SecondKey.Standards.Generator.Versioning;

namespace SecondKey.Standards.Generator.Drift;

public enum DriftStatus
{
    /// <summary>The pin is the latest release.</summary>
    Current,

    /// <summary>A newer release exists.</summary>
    Behind,

    /// <summary>The pin is newer than any release: it pins something that was never released.</summary>
    Ahead,

    /// <summary>The agent's skill and the gate's package pin different versions.</summary>
    Disagree,

    /// <summary>Nothing in the repository pins the standards.</summary>
    Unpinned,

    /// <summary>A pin exists but cannot be read as an exact version.</summary>
    Unreadable,

    /// <summary>The source has no release to compare with.</summary>
    NoRelease,

    /// <summary>The source could not be read.</summary>
    SourceUnavailable,
}

public sealed record DriftOptions(
    string RepositoryRoot,
    string PackageId,
    string SkillName,
    SemanticVersion? PinnedVersion = null,
    SemanticVersion? LatestVersion = null);

/// <summary>The outcome, a one-line summary, and the lines that explain it.</summary>
public sealed record DriftResult(
    DriftStatus Status,
    string Summary,
    IReadOnlyList<string> Details,
    SemanticVersion? Pinned,
    SemanticVersion? Latest)
{
    /// <summary>0 when current; 2 when the check could not be made; 1 for every finding.</summary>
    public int ExitCode => Status switch
    {
        DriftStatus.Current => Cli.ExitCodes.Success,
        DriftStatus.Unreadable or DriftStatus.NoRelease or DriftStatus.SourceUnavailable => Cli.ExitCodes.CouldNotRun,
        _ => Cli.ExitCodes.CheckFailed,
    };
}

/// <summary>
/// The drift check: a consuming repository's pinned standards version against the latest release.
/// It fails when the repository is behind — printing the rules and changelog entries it is missing —
/// and also when the repository's pins disagree with each other, because then the migration agent
/// and the gate are working from different standards, which is the drift this component exists to
/// prevent.
/// </summary>
public static class DriftChecker
{
    public static async Task<DriftResult> CheckAsync(DriftOptions options, IReleaseSource source, CancellationToken cancellationToken)
    {
        IReadOnlyList<Pin> pins;
        if (options.PinnedVersion is { } explicitVersion)
        {
            pins = [new Pin(PinKind.Explicit, "--pinned-version", explicitVersion)];
        }
        else
        {
            var scan = PinReader.Scan(options.RepositoryRoot, options.PackageId, options.SkillName);
            if (scan.Problems.Count > 0)
            {
                return new DriftResult(DriftStatus.Unreadable, "A standards pin could not be read.", scan.Problems, null, null);
            }

            pins = scan.Pins;
        }

        if (pins.Count == 0)
        {
            return new DriftResult(
                DriftStatus.Unpinned,
                "This repository does not pin the Second Key standards.",
                [
                    $"Expected a {options.PackageId} package reference (PackageVersion, GlobalPackageReference or PackageReference),",
                    $"or the skill at .github/skills/{options.SkillName}/SKILL.md (its metadata.version is the pin).",
                ],
                null,
                null);
        }

        var pinLines = pins.Select(pin => pin.Describe()).ToList();
        var versions = pins.Select(pin => pin.Version).Distinct().Order().ToList();
        if (versions.Count > 1)
        {
            return new DriftResult(
                DriftStatus.Disagree,
                $"The pins disagree ({string.Join(" and ", versions)}): the migration agent and the gate are working from different standards.",
                [.. pinLines, "Pin the skill and the package to the same version."],
                null,
                null);
        }

        var pinned = versions[0];
        IReadOnlyList<SemanticVersion> releases = [];
        SemanticVersion latest;
        if (options.LatestVersion is { } givenLatest)
        {
            latest = givenLatest;
        }
        else
        {
            try
            {
                releases = await source.ListReleasesAsync(cancellationToken);
            }
            catch (ReleaseSourceException ex)
            {
                return new DriftResult(DriftStatus.SourceUnavailable, $"The standards releases could not be read: {ex.Message}", pinLines, pinned, null);
            }

            if (releases.Count == 0)
            {
                return new DriftResult(
                    DriftStatus.NoRelease,
                    $"No release (a tag v<MAJOR.MINOR.PATCH>) was found at {source.Description}; there is nothing to compare the pin with.",
                    pinLines,
                    pinned,
                    null);
            }

            latest = releases[0];
        }

        if (pinned.CompareTo(latest) == 0)
        {
            return new DriftResult(DriftStatus.Current, $"Up to date: this repository pins standards {pinned}, the latest release.", pinLines, pinned, latest);
        }

        if (pinned > latest)
        {
            return new DriftResult(
                DriftStatus.Ahead,
                $"This repository pins standards {pinned}, but the latest release is {latest}: {pinned} was never released.",
                [.. pinLines, $"Pin a released version (the latest is {latest})."],
                pinned,
                latest);
        }

        var details = new List<string>(pinLines);
        if (releases.Count > 0 && !releases.Contains(pinned))
        {
            details.Add($"Note: {pinned} is not a released version either.");
        }

        details.AddRange(await WhatChangedAsync(source, pinned, latest, cancellationToken));
        details.Add("");
        details.Add($"To update: reference {options.PackageId} {latest}, and copy the skill again from tag v{latest}");
        details.Add($"(generated/skills/{options.SkillName}/ into .github/skills/{options.SkillName}/).");

        return new DriftResult(
            DriftStatus.Behind,
            $"Behind: this repository pins standards {pinned}; the latest release is {latest}.",
            details,
            pinned,
            latest);
    }

    private static async Task<IReadOnlyList<string>> WhatChangedAsync(
        IReleaseSource source,
        SemanticVersion pinned,
        SemanticVersion latest,
        CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        try
        {
            var latestManifest = Manifest.TryParse(await source.ReadFileAsync(latest, GeneratedPaths.Manifest, cancellationToken) ?? "");
            var pinnedManifest = Manifest.TryParse(await source.ReadFileAsync(pinned, GeneratedPaths.Manifest, cancellationToken) ?? "");

            lines.Add("");
            if (latestManifest is not null && pinnedManifest is not null)
            {
                var changes = ManifestDiff.Compare(pinnedManifest, latestManifest);
                lines.Add($"Rules changed between {pinned} and {latest}:");
                lines.AddRange(changes.Count == 0
                    ? ["  (no rule changed; the difference is in the packaging)"]
                    : changes.Select(change => $"  {change.Kind.ToString().ToLowerInvariant(),-8} {change.Id,-12} {change.Detail} — {change.Title}"));
            }
            else
            {
                lines.Add($"Rule-by-rule changes are unavailable: no manifest was found for {(pinnedManifest is null ? pinned : latest)}.");
            }

            var changelogText = await source.ReadFileAsync(latest, Changelog.RelativePath, cancellationToken);
            if (changelogText is not null)
            {
                var entries = Changelog.Parse(changelogText, Changelog.RelativePath, out _).Between(pinned, latest);
                if (entries.Count > 0)
                {
                    lines.Add("");
                    lines.Add($"Changelog from {pinned} to {latest}:");
                    lines.AddRange(entries.SelectMany(entry => entry.Text.Split('\n')).Select(line => "  " + line));
                }
            }
        }
        catch (ReleaseSourceException ex)
        {
            lines.Add("");
            lines.Add($"What changed could not be read: {ex.Message}");
        }

        return lines;
    }
}
