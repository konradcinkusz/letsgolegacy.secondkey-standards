using System.Text.RegularExpressions;

namespace SecondKey.Standards.Generator.Model;

/// <summary>The formats a rule's identifiers must follow, in one place.</summary>
internal static partial class Patterns
{
    /// <summary>
    /// A rule id: upper-case segments separated by hyphens, ending in a three-digit number —
    /// <c>SK-MIG-001</c>, <c>SK-ARCH-004</c>. Safe in file names, anchors and code comments.
    /// </summary>
    [GeneratedRegex("^[A-Z][A-Z0-9]*(-[A-Z][A-Z0-9]*)*-[0-9]{3}$", RegexOptions.CultureInvariant)]
    public static partial Regex RuleId();

    /// <summary>The slug that may follow the id in a rule's file name: <c>SK-MIG-001-replace-system-web.md</c>.</summary>
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    public static partial Regex FileSlug();

    /// <summary>
    /// A Portcullis diagnostic id. Underscores, not hyphens: Roslyn rejects a hyphenated
    /// diagnostic id at the call that reports it (recorded in Portcullis's RuleRegistry).
    /// </summary>
    [GeneratedRegex("^PORTCULLIS_[A-Z0-9]+(_[A-Z0-9]+)*$", RegexOptions.CultureInvariant)]
    public static partial Regex PortcullisRuleId();

    /// <summary>A .NET SDK analyzer id: code-quality (CAxxxx) or code-style (IDExxxx).</summary>
    [GeneratedRegex("^(CA|IDE)[0-9]{4}$", RegexOptions.CultureInvariant)]
    public static partial Regex SdkAnalyzerId();

    /// <summary>
    /// A principle of the architecture constitution: P1–P15, plus the lettered corollaries
    /// such as P2a.
    /// </summary>
    [GeneratedRegex("^P([1-9]|1[0-5])[a-z]?$", RegexOptions.CultureInvariant)]
    public static partial Regex Principle();
}
