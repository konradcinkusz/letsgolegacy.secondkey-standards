using SecondKey.Standards.Generator.Model;
using SecondKey.Standards.Generator.Tests.Support;
using Xunit;

namespace SecondKey.Standards.Generator.Tests;

public class StandardsLoaderTests
{
    private static StandardsSet Load(TestRepository repository) =>
        StandardsLoader.Load(repository.Layout, PackConfig.Load(repository.Layout.PackConfigPath));

    [Fact]
    public void Rules_are_loaded_in_id_order_and_the_readme_is_skipped()
    {
        using var repository = new TestRepository();
        repository.WriteRule("SK-MIG-002.md", RuleText.Valid("SK-MIG-002"));
        repository.WriteRule("SK-MIG-001.md", RuleText.Valid("SK-MIG-001"));
        repository.WriteRule("README.md", "# Not a rule\n");

        var set = Load(repository);

        Assert.True(set.IsValid, string.Join('\n', set.Problems));
        Assert.Equal(["SK-MIG-001", "SK-MIG-002"], set.Rules.Select(r => r.Id));
    }

    [Fact]
    public void A_duplicate_id_is_reported_on_the_second_file()
    {
        using var repository = new TestRepository();
        repository.WriteRule("SK-MIG-001-first.md", RuleText.Valid("SK-MIG-001"));
        repository.WriteRule("SK-MIG-001-second.md", RuleText.Valid("SK-MIG-001"));

        var set = Load(repository);

        var problem = Assert.Single(set.Problems);
        Assert.Equal("standards/SK-MIG-001-second.md", problem.Path);
        Assert.Contains("already used by standards/SK-MIG-001-first.md", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_diagnostic_mapped_by_two_rules_is_reported()
    {
        using var repository = new TestRepository();
        foreach (var id in new[] { "SK-MIG-001", "SK-MIG-002" })
        {
            repository.WriteRule($"{id}.md", RuleText.Valid(frontMatter: $"""
                id: {id}
                title: Mapped
                severity: warning
                category: globalization
                appliesTo:
                  - "*.cs"
                analyzers:
                  - CA1310
                """));
        }

        var set = Load(repository);

        var problem = Assert.Single(set.Problems);
        Assert.Equal("standards/SK-MIG-002.md", problem.Path);
        Assert.Contains("CA1310 is already mapped by SK-MIG-001", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_broken_file_is_reported_in_one_run()
    {
        using var repository = new TestRepository();
        repository.WriteRule("SK-MIG-001.md", RuleText.WithFrontMatterLine("severity", null));
        repository.WriteRule("SK-MIG-002.md", "no front matter\n");

        var set = Load(repository);

        Assert.Equal(["standards/SK-MIG-001.md", "standards/SK-MIG-002.md"], set.Problems.Select(p => p.Path));
    }

    [Fact]
    public void An_empty_standards_directory_is_a_problem_not_a_pass()
    {
        using var repository = new TestRepository();

        var set = Load(repository);

        Assert.False(set.IsValid);
        Assert.Contains("no rule files found", Assert.Single(set.Problems).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_pack_with_an_unknown_key_is_rejected()
    {
        using var repository = new TestRepository();
        repository.WritePack(new { name = "p", categories = new[] { "a" }, portcullisRules = Array.Empty<string>(), extra = 1 });

        var exception = Assert.Throws<PackConfigException>(() => PackConfig.Load(repository.Layout.PackConfigPath));
        Assert.Contains("extra", exception.Message, StringComparison.Ordinal);
    }
}
