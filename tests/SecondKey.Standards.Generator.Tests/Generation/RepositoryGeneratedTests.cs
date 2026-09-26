using SecondKey.Standards.Generator.Generation;
using SecondKey.Standards.Generator.Model;
using SecondKey.Standards.Generator.Tests.Support;
using Xunit;

namespace SecondKey.Standards.Generator.Tests.Generation;

/// <summary>
/// This repository's committed <c>generated/</c> tree, checked from the test suite as well as by
/// CI's <c>generate --check</c> step, so a stale tree fails <c>dotnet test</c> locally too.
/// </summary>
public class RepositoryGeneratedTests
{
    private static readonly PackConfig Pack = PackConfig.Load(RepositoryRoot.Layout.PackConfigPath);

    private static readonly GenerationResult Result = PackGenerator.Build(RepositoryRoot.Layout, Pack);

    [Fact]
    public void The_committed_generated_tree_matches_the_sources()
    {
        Assert.True(Result.Succeeded, string.Join('\n', Result.Problems));

        var differences = TreeSync.Compare(RepositoryRoot.Layout, Result.Tree!);

        Assert.True(
            differences.Count == 0,
            "generated/ is stale; run `dotnet run --project src/SecondKey.Standards.Generator -- generate`:\n"
            + string.Join('\n', differences));
    }

    [Fact]
    public void The_skill_meets_the_agent_skills_constraints()
    {
        var skill = Result.Tree!.Files[GeneratedPaths.SkillFile(Pack.Skill.Name)];
        var front = EmissionTests.FrontMatter(skill);

        // Agent Skills specification: the name matches the directory; the description is 1–1024
        // characters. GitHub Copilot upgrade's authoring rules: the body stays under 500 lines.
        Assert.Equal(Pack.Skill.Name, EmissionTests.Scalar(front, "name"));
        Assert.EndsWith($"/{Pack.Skill.Name}", GeneratedPaths.SkillDirectory(Pack.Skill.Name), StringComparison.Ordinal);
        Assert.InRange(EmissionTests.Scalar(front, "description")!.Length, 1, SkillConfig.MaxDescriptionLength);
        Assert.True(skill.Split('\n').Length < 500, "SKILL.md should stay under 500 lines; move detail to references/");
    }

    [Fact]
    public void Every_mapped_diagnostic_is_in_the_global_config_at_its_rules_severity()
    {
        var config = Result.Tree!.Files[GeneratedPaths.GlobalConfig];
        var rules = StandardsLoader.Load(RepositoryRoot.Layout, Pack).Rules;

        Assert.All(
            rules.SelectMany(rule => rule.Diagnostics.Select(diagnostic => (rule, diagnostic))),
            pair => Assert.Contains(
                $"dotnet_diagnostic.{pair.diagnostic}.severity = {pair.rule.Severity.ToName()}\n",
                config,
                StringComparison.Ordinal));
    }
}
