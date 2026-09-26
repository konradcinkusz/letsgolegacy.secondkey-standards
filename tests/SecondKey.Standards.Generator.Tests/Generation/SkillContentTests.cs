using System.Text.RegularExpressions;
using SecondKey.Standards.Generator.Generation;
using SecondKey.Standards.Generator.Model;
using SecondKey.Standards.Generator.Tests.Support;
using Xunit;

namespace SecondKey.Standards.Generator.Tests.Generation;

/// <summary>
/// R4: the generated skill tells the migration agent concretely what to preserve, what to change,
/// which rules the gate checks and how to flag a behaviour change — and it agrees with the rules it
/// is generated from.
/// </summary>
public partial class SkillContentTests
{
    private static readonly PackConfig Pack = PackConfig.Load(RepositoryRoot.Layout.PackConfigPath);

    private static readonly IReadOnlyList<Rule> Rules = StandardsLoader.Load(RepositoryRoot.Layout, Pack).Rules;

    private static readonly GeneratedTree Tree = PackGenerator.Build(RepositoryRoot.Layout, Pack).Tree!;

    private static string Skill => Tree.Files[GeneratedPaths.SkillFile(Pack.Skill.Name)];

    [Theory]
    [InlineData("## Two rules above all others")]
    [InlineData("## Workflow")]
    [InlineData("## Never change silently")]
    [InlineData("## Rules")]
    [InlineData("## What the gate checks")]
    [InlineData("## Flag format")]
    [InlineData("## Success criteria")]
    [InlineData("## When something goes wrong")]
    public void The_skill_has_each_part_the_agent_needs(string heading)
    {
        Assert.Contains($"\n{heading}\n", Skill, StringComparison.Ordinal);
    }

    [Fact]
    public void The_flag_protocol_in_the_skill_is_the_one_SK_MIG_011_defines()
    {
        var protocol = Rules.Single(rule => rule.Id == "SK-MIG-011").Body;
        const string Marker = "SECONDKEY-FLAG";
        const string RegisterHeader = "| Rule | Location | Legacy behaviour | What the migration did | Decision needed |";

        foreach (var text in new[] { protocol, Skill })
        {
            Assert.Contains(Marker + " <rule-id>:", text, StringComparison.Ordinal);
            Assert.Contains(SkillEmitter.FlagRegister, text, StringComparison.Ordinal);
            Assert.Contains(RegisterHeader, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Every_rule_and_every_gate_diagnostic_is_in_the_skill()
    {
        Assert.All(Rules, rule =>
        {
            Assert.Contains($"[{rule.Id}](references/{rule.Id}.md)", Skill, StringComparison.Ordinal);
            Assert.All(rule.Diagnostics, diagnostic => Assert.Contains($"| `{diagnostic}` | {rule.Severity.ToName()} | {rule.Id} |", Skill, StringComparison.Ordinal));
        });
    }

    [Fact]
    public void Every_rule_id_the_skill_cites_exists()
    {
        var known = Rules.Select(rule => rule.Id).ToHashSet(StringComparer.Ordinal);

        var cited = RuleId().Matches(Skill).Select(match => match.Value).Distinct(StringComparer.Ordinal);

        Assert.All(cited, id => Assert.Contains(id, known));
    }

    [Fact]
    public void Every_rule_id_a_rule_cites_exists()
    {
        var known = Rules.Select(rule => rule.Id).ToHashSet(StringComparer.Ordinal);

        Assert.All(Rules, rule => Assert.All(
            RuleId().Matches(rule.Body).Select(match => match.Value),
            id => Assert.True(known.Contains(id), $"{rule.Id} cites {id}, which does not exist")));
    }

    [Fact]
    public void The_skill_directory_is_self_contained()
    {
        var directory = GeneratedPaths.SkillDirectory(Pack.Skill.Name);

        foreach (var link in RelativeLink().Matches(Skill).Select(match => match.Groups["target"].Value))
        {
            Assert.True(Tree.Files.ContainsKey($"{directory}/{link}"), $"SKILL.md links to {link}, which is not in the skill directory");
        }
    }

    [Fact]
    public void The_description_routes_a_dotnet_framework_upgrade_to_the_skill()
    {
        var description = EmissionTests.Scalar(EmissionTests.FrontMatter(Skill), "description")!;

        foreach (var trigger in new[] { ".NET Framework", ".NET 10", "upgrade", "migrate", "ASP.NET Core", "EF6" })
        {
            Assert.Contains(trigger, description, StringComparison.Ordinal);
        }
    }

    [GeneratedRegex(@"\bSK-[A-Z]+-[0-9]{3}\b", RegexOptions.CultureInvariant)]
    private static partial Regex RuleId();

    [GeneratedRegex(@"\]\((?!https?://|#)(?<target>[^)\s]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex RelativeLink();
}
