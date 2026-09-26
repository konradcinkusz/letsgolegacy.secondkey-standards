using System.Text.RegularExpressions;

namespace SecondKey.Standards.Generator.Generation;

/// <summary>
/// The hand-written part of the skill: <c>catalog/skill.template.md</c>, with <c>{{placeholder}}</c>
/// tokens the generator fills from the rules. An unknown token is an error rather than text left in
/// the skill, because the agent would read it literally.
/// </summary>
internal static partial class SkillTemplate
{
    public const string RelativePath = "catalog/skill.template.md";

    public static IReadOnlyList<string> Placeholders { get; } =
    [
        "version",
        "rule-count",
        "rule-table",
        "gate-table",
        "flag-register",
        "repository",
    ];

    /// <summary>
    /// Tokens the template must use: without the rule table and the gate table, the skill would not
    /// tell the agent what the rules are or what the gate checks.
    /// </summary>
    public static IReadOnlyList<string> RequiredPlaceholders { get; } = ["rule-table", "gate-table"];

    public static IReadOnlyList<string> MissingPlaceholders(string template) =>
        RequiredPlaceholders.Where(name => !template.Contains("{{" + name + "}}", StringComparison.Ordinal)).ToList();

    /// <summary>Tokens in <paramref name="template"/> the generator does not know.</summary>
    public static IReadOnlyList<(int Line, string Token)> UnknownPlaceholders(string template)
    {
        var unknown = new List<(int, string)>();
        var lines = template.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            foreach (Match match in Token().Matches(lines[i]))
            {
                var name = match.Groups["name"].Value;
                if (!Placeholders.Contains(name, StringComparer.Ordinal))
                {
                    unknown.Add((i + 1, name));
                }
            }
        }

        return unknown;
    }

    public static string Render(string template, IReadOnlyDictionary<string, string> values) =>
        Token().Replace(template, match => values.TryGetValue(match.Groups["name"].Value, out var value)
            ? value
            : throw new InvalidOperationException($"No value for {{{{{match.Groups["name"].Value}}}}}."));

    [GeneratedRegex(@"\{\{(?<name>[a-z0-9-]+)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex Token();
}
