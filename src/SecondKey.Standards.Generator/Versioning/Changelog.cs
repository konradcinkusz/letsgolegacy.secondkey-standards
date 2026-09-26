using System.Globalization;
using System.Text.RegularExpressions;
using SecondKey.Standards.Generator.Diagnostics;
using SecondKey.Standards.Generator.Model;

namespace SecondKey.Standards.Generator.Versioning;

/// <summary>One version's entry in <c>CHANGELOG.md</c>.</summary>
/// <param name="Version">The version in the heading.</param>
/// <param name="ReleaseDate">The date in the heading, or null while the version is "Unreleased".</param>
/// <param name="Text">The heading and everything under it, up to the next version.</param>
/// <param name="Line">The heading's 1-based line.</param>
public sealed record ChangelogEntry(SemanticVersion Version, DateOnly? ReleaseDate, string Text, int Line)
{
    public bool IsReleased => ReleaseDate is not null;
}

/// <summary>
/// <c>CHANGELOG.md</c>: one <c>## [MAJOR.MINOR.PATCH] — YYYY-MM-DD</c> section per version, newest
/// first, or <c>— Unreleased</c> for the version under development. The generator requires an
/// entry for the pack's version; the drift check prints the entries a consumer is missing.
/// </summary>
public sealed partial class Changelog
{
    public const string RelativePath = "CHANGELOG.md";
    public const string UnreleasedMarker = "Unreleased";

    private Changelog(IReadOnlyList<ChangelogEntry> entries) => Entries = entries;

    /// <summary>Newest first, as written.</summary>
    public IReadOnlyList<ChangelogEntry> Entries { get; }

    public ChangelogEntry? Find(SemanticVersion version) =>
        Entries.FirstOrDefault(entry => entry.Version.CompareTo(version) == 0);

    /// <summary>The entries after <paramref name="exclusiveFrom"/>, up to and including <paramref name="inclusiveTo"/>.</summary>
    public IReadOnlyList<ChangelogEntry> Between(SemanticVersion exclusiveFrom, SemanticVersion inclusiveTo) =>
        Entries.Where(entry => entry.Version > exclusiveFrom && entry.Version <= inclusiveTo).ToList();

    public static Changelog Parse(string text, string path, out IReadOnlyList<Problem> problems)
    {
        var found = new List<Problem>();
        var entries = new List<ChangelogEntry>();
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        var current = -1;
        SemanticVersion? version = null;
        DateOnly? date = null;

        void Close(int end)
        {
            if (version is not null)
            {
                var body = string.Join('\n', lines[current..end]).TrimEnd();
                entries.Add(new ChangelogEntry(version, date, body, current + 1));
            }
        }

        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].StartsWith("## ", StringComparison.Ordinal))
            {
                continue;
            }

            Close(i);
            current = i;
            version = null;
            date = null;

            var heading = Heading().Match(lines[i]);
            if (!heading.Success || !SemanticVersion.TryParse(heading.Groups["version"].Value, out var parsed))
            {
                found.Add(new Problem(path, i + 1, "a version heading must read \"## [MAJOR.MINOR.PATCH] — YYYY-MM-DD\" or \"## [MAJOR.MINOR.PATCH] — Unreleased\""));
                continue;
            }

            var status = heading.Groups["status"].Value.Trim();
            if (string.Equals(status, UnreleasedMarker, StringComparison.Ordinal))
            {
                date = null;
            }
            else if (DateOnly.TryParseExact(status, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var released))
            {
                date = released;
            }
            else
            {
                found.Add(new Problem(path, i + 1, $"\"{status}\" is neither a release date (YYYY-MM-DD) nor \"{UnreleasedMarker}\""));
                continue;
            }

            version = parsed;
        }

        Close(lines.Length);

        for (var i = 1; i < entries.Count; i++)
        {
            if (entries[i].Version >= entries[i - 1].Version)
            {
                found.Add(new Problem(path, entries[i].Line, $"version {entries[i].Version} is listed after {entries[i - 1].Version}; keep the newest first, each version once"));
            }
        }

        problems = found;
        return new Changelog(entries);
    }

    [GeneratedRegex(@"^## \[(?<version>[^\]]+)\]\s+[—–-]\s+(?<status>.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex Heading();
}
