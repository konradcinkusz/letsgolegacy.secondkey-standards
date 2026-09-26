using System.Globalization;
using System.Text.RegularExpressions;

namespace SecondKey.Standards.Generator.Model;

/// <summary>
/// A <c>MAJOR.MINOR.PATCH[-PRERELEASE]</c> version, the form the standards are versioned, tagged
/// (<c>v0.1.0</c>) and pinned in. Build metadata is not part of it: two builds of one version are
/// the same standards.
/// </summary>
public sealed partial record SemanticVersion(int Major, int Minor, int Patch, string? Prerelease) : IComparable<SemanticVersion>
{
    public bool IsPrerelease => Prerelease is not null;

    public static bool TryParse(string? text, out SemanticVersion version)
    {
        version = null!;
        if (text is null)
        {
            return false;
        }

        var match = Format().Match(text.Trim());
        if (!match.Success
            || !int.TryParse(match.Groups["major"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(match.Groups["minor"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(match.Groups["patch"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var patch))
        {
            return false;
        }

        var prerelease = match.Groups["pre"].Success ? match.Groups["pre"].Value : null;
        version = new SemanticVersion(major, minor, patch, prerelease);
        return true;
    }

    public static SemanticVersion Parse(string text) =>
        TryParse(text, out var version)
            ? version
            : throw new FormatException($"\"{text}\" is not a MAJOR.MINOR.PATCH version.");

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byNumber = (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch));
        if (byNumber != 0)
        {
            return byNumber;
        }

        // A pre-release sorts before its release (1.0.0-rc.1 < 1.0.0). Between two pre-releases,
        // an ordinal comparison is enough for the tags this repository produces.
        return (Prerelease, other.Prerelease) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            _ => string.CompareOrdinal(Prerelease, other.Prerelease),
        };
    }

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}{(Prerelease is null ? "" : "-" + Prerelease)}");

    [GeneratedRegex(@"^(?<major>0|[1-9][0-9]*)\.(?<minor>0|[1-9][0-9]*)\.(?<patch>0|[1-9][0-9]*)(-(?<pre>[0-9A-Za-z.-]+))?(\+[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex Format();
}
