using System.Globalization;
using System.Text;
using SecondKey.Standards.Generator.Model;

namespace SecondKey.Standards.Generator.Generation;

/// <summary>
/// Emits the Agent Skills directory: <c>SKILL.md</c> (front matter from <c>catalog/pack.json</c>,
/// body from the hand-written template with the rule and gate tables filled in) and one reference
/// file per rule, which the agent loads when a rule applies.
/// </summary>
/// <remarks>
/// The directory is self-contained so it can be copied as-is into a target repository's
/// <c>.github/skills/</c>: every link in it is relative to the skill or absolute.
/// </remarks>
internal static class SkillEmitter
{
    /// <summary>Where GitHub Copilot upgrade and the other clients read per-rule detail from.</summary>
    public const string ReferencesDirectory = "references";

    /// <summary>The register every behaviour flag is recorded in, in the migrated repository.</summary>
    public const string FlagRegister = "docs/migration/behaviour-flags.md";

    public static void Emit(GeneratedTree tree, PackConfig pack, string version, IReadOnlyList<Rule> rules, string template)
    {
        var skill = pack.Skill;
        tree.Add(GeneratedPaths.SkillFile(skill.Name), SkillFile(pack, version, rules, template));
        foreach (var rule in rules)
        {
            tree.Add(GeneratedPaths.Reference(skill.Name, rule.Id), Reference(pack, version, rule));
        }
    }

    private static string SkillFile(PackConfig pack, string version, IReadOnlyList<Rule> rules, string template)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["version"] = version,
            ["rule-count"] = rules.Count.ToString(CultureInfo.InvariantCulture),
            ["rule-table"] = RuleTable(rules),
            ["gate-table"] = GateTable(rules),
            ["flag-register"] = FlagRegister,
            ["repository"] = pack.Repository,
        };

        var builder = new StringBuilder();
        builder.Append("---\n");
        builder.Append("name: ").Append(pack.Skill.Name).Append('\n');
        builder.Append(MarkdownText.YamlFolded("description", pack.Skill.Description)).Append('\n');
        builder.Append("metadata:\n");
        builder.Append("  discovery: ").Append(MarkdownText.YamlQuoted(pack.Skill.Discovery)).Append('\n');
        builder.Append("  traits: ").Append(MarkdownText.YamlQuoted(pack.Skill.Traits)).Append('\n');
        builder.Append("  version: ").Append(MarkdownText.YamlQuoted(version)).Append('\n');
        builder.Append("  source: ").Append(MarkdownText.YamlQuoted(pack.Repository)).Append('\n');
        builder.Append("---\n\n");
        builder.Append(SkillTemplate.Render(template, values).Trim()).Append("\n\n");
        builder.Append("---\n\n");
        builder.Append("Second Key standards ").Append(version).Append(", generated from ")
            .Append(CultureInfo.InvariantCulture, $"[`standards/`]({pack.Repository}/tree/v{version}/standards)")
            .Append(". Do not edit this copy: take a newer version from the source repository instead.\n");
        return builder.ToString();
    }

    private static string RuleTable(IReadOnlyList<Rule> rules) => MarkdownText.Table(
        ["Rule", "Severity", "What migrated code must do", "Gate diagnostic"],
        rules.Select(rule => (IReadOnlyList<string>)
        [
            $"[{rule.Id}]({ReferencesDirectory}/{rule.Id}.md)",
            rule.Severity.ToName(),
            rule.Title,
            rule.Diagnostics.Count == 0 ? "—" : string.Join(", ", rule.Diagnostics.Select(MarkdownText.Code)),
        ]));

    private static string GateTable(IReadOnlyList<Rule> rules) => MarkdownText.Table(
        ["Diagnostic", "Severity", "Rule"],
        rules.SelectMany(rule => rule.Diagnostics.Select(diagnostic => (IReadOnlyList<string>)
        [
            MarkdownText.Code(diagnostic),
            rule.Severity.ToName(),
            rule.Id,
        ])));

    private static string Reference(PackConfig pack, string version, Rule rule)
    {
        var principle = rule.Principle is null
            ? "—"
            : $"[{rule.Principle}]({pack.Constitution}#{rule.Principle.ToLowerInvariant()})";

        var builder = new StringBuilder();
        builder.Append("# ").Append(rule.Id).Append(" — ").Append(rule.Title).Append("\n\n");
        builder.Append(MarkdownText.Table(
            ["Severity", "Category", "Applies to", "Gate diagnostic", "Principle"],
            [
                [
                    rule.Severity.ToName(),
                    rule.Category,
                    string.Join(", ", rule.AppliesTo.Select(MarkdownText.Code)),
                    rule.Diagnostics.Count == 0 ? "—" : string.Join(", ", rule.Diagnostics.Select(MarkdownText.Code)),
                    principle,
                ],
            ])).Append("\n\n");
        builder.Append("Sections: ")
            .AppendJoin(" · ", rule.Sections.Select(section => section.Heading))
            .Append("\n\n");
        builder.Append(rule.Body).Append("\n\n");
        builder.Append("---\n\n");
        builder.Append("Second Key standards ").Append(version).Append(", from ")
            .Append(CultureInfo.InvariantCulture, $"[`{rule.SourcePath}`]({pack.Repository}/blob/v{version}/{rule.SourcePath})")
            .Append(". Generated; do not edit.\n");
        return builder.ToString();
    }
}
