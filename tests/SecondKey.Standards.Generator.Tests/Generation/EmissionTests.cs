using SecondKey.Standards.Generator.Generation;
using SecondKey.Standards.Generator.Model;
using SecondKey.Standards.Generator.Tests.Support;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace SecondKey.Standards.Generator.Tests.Generation;

/// <summary>What the generator emits, checked on a small repository where every input is visible.</summary>
public class EmissionTests
{
    private static GeneratedTree Build(TestRepository repository)
    {
        var result = PackGenerator.Build(repository.Layout, PackConfig.Load(repository.Layout.PackConfigPath));
        Assert.True(result.Succeeded, string.Join('\n', result.Problems));
        return result.Tree!;
    }

    private static string MappedRule(string id, string severity, string diagnostics, string appliesTo = "  - \"*.cs\"") =>
        RuleText.Valid(frontMatter: $"""
            id: {id}
            title: Rule {id}
            severity: {severity}
            category: globalization
            appliesTo:
            {appliesTo}
            {diagnostics}
            """);

    [Fact]
    public void The_global_config_sets_every_mapped_diagnostic_to_its_rules_severity()
    {
        using var repository = new TestRepository();
        repository.WriteRule("SK-MIG-001.md", MappedRule("SK-MIG-001", "error", $"portcullisRule: {TestRepository.PortcullisRule}"));
        repository.WriteRule("SK-MIG-002.md", MappedRule("SK-MIG-002", "warning", "analyzers:\n  - CA1310\n  - CA1305"));
        repository.WriteRule("SK-MIG-003.md", MappedRule("SK-MIG-003", "error", ""));

        var config = Build(repository).Files[GeneratedPaths.GlobalConfig];

        Assert.Contains("is_global = true\nglobal_level = 0\n", config, StringComparison.Ordinal);
        Assert.Contains($"dotnet_diagnostic.{TestRepository.PortcullisRule}.severity = error", config, StringComparison.Ordinal);
        Assert.Contains("dotnet_diagnostic.CA1310.severity = warning", config, StringComparison.Ordinal);
        Assert.Contains("dotnet_diagnostic.CA1305.severity = warning", config, StringComparison.Ordinal);
        Assert.DoesNotContain("SK-MIG-003", config, StringComparison.Ordinal);
    }

    [Fact]
    public void The_editorconfig_scopes_each_diagnostic_to_its_rules_file_patterns()
    {
        using var repository = new TestRepository();
        repository.WriteRule("SK-MIG-001.md", MappedRule("SK-MIG-001", "error", $"portcullisRule: {TestRepository.PortcullisRule}"));
        repository.WriteRule("SK-MIG-002.md", MappedRule("SK-MIG-002", "suggestion", "analyzers:\n  - CA1310", "  - \"*.cs\"\n  - \"*.vb\""));

        var config = Build(repository).Files[GeneratedPaths.EditorConfig];
        var cs = Section(config, "[*.cs]");
        var vb = Section(config, "[*.vb]");

        Assert.DoesNotContain("root = true", config, StringComparison.Ordinal);
        Assert.Contains($"dotnet_diagnostic.{TestRepository.PortcullisRule}.severity = error", cs, StringComparison.Ordinal);
        Assert.Contains("dotnet_diagnostic.CA1310.severity = suggestion", cs, StringComparison.Ordinal);
        Assert.Contains("dotnet_diagnostic.CA1310.severity = suggestion", vb, StringComparison.Ordinal);
        Assert.DoesNotContain(TestRepository.PortcullisRule, vb, StringComparison.Ordinal);
    }

    [Fact]
    public void The_skill_front_matter_is_valid_agent_skills_yaml()
    {
        using var repository = new TestRepository();
        repository.WriteRule("SK-MIG-001.md", RuleText.Valid());

        var skill = Build(repository).Files[GeneratedPaths.SkillFile(TestRepository.SkillName)];
        var front = FrontMatter(skill);

        Assert.Equal(TestRepository.SkillName, Scalar(front, "name"));
        Assert.Equal("Applies the test standards. Use in tests.", Scalar(front, "description"));
        var metadata = (YamlMappingNode)front.Children[new YamlScalarNode("metadata")];
        Assert.Equal("preload", Scalar(metadata, "discovery"));
        Assert.Equal(".NET|CSharp", Scalar(metadata, "traits"));
        Assert.Equal("1.0.0", Scalar(metadata, "version"));
    }

    [Fact]
    public void Every_rule_gets_a_reference_file_and_a_row_in_the_skill()
    {
        using var repository = new TestRepository();
        repository.WriteRule("SK-MIG-001.md", RuleText.Valid("SK-MIG-001"));
        repository.WriteRule("SK-MIG-002.md", RuleText.Valid("SK-MIG-002"));

        var tree = Build(repository);
        var skill = tree.Files[GeneratedPaths.SkillFile(TestRepository.SkillName)];

        foreach (var id in new[] { "SK-MIG-001", "SK-MIG-002" })
        {
            Assert.Contains($"[{id}](references/{id}.md)", skill, StringComparison.Ordinal);
            var reference = tree.Files[GeneratedPaths.Reference(TestRepository.SkillName, id)];
            Assert.StartsWith($"# {id} — State the comparison", reference, StringComparison.Ordinal);
            Assert.Contains("## Non-compliant", reference, StringComparison.Ordinal);
            Assert.Contains("Every culture-sensitive comparison states its StringComparison.", reference, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("{{", skill, StringComparison.Ordinal);
    }

    [Fact]
    public void The_package_ships_the_global_config_through_build_props()
    {
        using var repository = new TestRepository(version: "2.3.4");
        repository.WriteRule("SK-MIG-001.md", RuleText.Valid());

        var tree = Build(repository);
        var project = tree.Files[GeneratedPaths.PackageProject(TestRepository.PackageId)];
        var props = tree.Files[GeneratedPaths.PackageProps(TestRepository.PackageId)];

        Assert.Contains("<Version>2.3.4</Version>", project, StringComparison.Ordinal);
        Assert.Contains("<IncludeBuildOutput>false</IncludeBuildOutput>", project, StringComparison.Ordinal);
        Assert.Contains($"Include=\"../config/.globalconfig\" Pack=\"true\" PackagePath=\"build/{TestRepository.PackageId}.globalconfig\"", project, StringComparison.Ordinal);
        Assert.Contains($"<EditorConfigFiles Include=\"$(MSBuildThisFileDirectory){TestRepository.PackageId}.globalconfig\" />", props, StringComparison.Ordinal);
        Assert.Contains("<SecondKeyStandardsVersion>2.3.4</SecondKeyStandardsVersion>", props, StringComparison.Ordinal);
    }

    [Fact]
    public void The_manifest_lists_every_rule_and_round_trips()
    {
        using var repository = new TestRepository();
        repository.WriteRule("SK-MIG-001.md", MappedRule("SK-MIG-001", "error", $"portcullisRule: {TestRepository.PortcullisRule}"));

        var manifest = Manifest.TryParse(Build(repository).Files[GeneratedPaths.Manifest]);

        Assert.NotNull(manifest);
        Assert.Equal("1.0.0", manifest.Version);
        Assert.StartsWith("sha256:", manifest.ContentHash, StringComparison.Ordinal);
        var rule = Assert.Single(manifest.Rules);
        Assert.Equal("SK-MIG-001", rule.Id);
        Assert.Equal([TestRepository.PortcullisRule], rule.Diagnostics);
    }

    [Fact]
    public void The_content_hash_ignores_the_version_but_not_the_content()
    {
        string HashFor(string version, string title)
        {
            using var repository = new TestRepository(version: version);
            repository.WriteRule("SK-MIG-001.md", RuleText.Valid().Replace("State the comparison", title, StringComparison.Ordinal));
            return Manifest.TryParse(Build(repository).Files[GeneratedPaths.Manifest])!.ContentHash;
        }

        Assert.Equal(HashFor("1.0.0", "State the comparison"), HashFor("1.1.0", "State the comparison"));
        Assert.NotEqual(HashFor("1.0.0", "State the comparison"), HashFor("1.0.0", "State the comparison explicitly"));
    }

    [Fact]
    public void An_unknown_template_placeholder_is_a_problem_not_literal_text()
    {
        using var repository = new TestRepository();
        repository.WriteRule("SK-MIG-001.md", RuleText.Valid());
        repository.WriteFile("catalog/skill.template.md", "# Skill\n\n{{rule-tabel}}\n");

        var result = PackGenerator.Build(repository.Layout, PackConfig.Load(repository.Layout.PackConfigPath));

        Assert.False(result.Succeeded);
        var problem = Assert.Single(result.Problems);
        Assert.Equal("catalog/skill.template.md", problem.Path);
        Assert.Equal(3, problem.Line);
        Assert.Contains("{{rule-tabel}}", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_relative_link_in_a_rule_is_rejected_because_it_would_break_in_the_skill()
    {
        using var repository = new TestRepository();
        repository.WriteRule("SK-MIG-001.md", RuleText.Valid(body: RuleText.ValidBody + "\n\n## References\n\n- [Other rule](SK-MIG-002-other.md)\n"));

        var result = PackGenerator.Build(repository.Layout, PackConfig.Load(repository.Layout.PackConfigPath));

        Assert.Contains(result.Problems, p => p.Message.Contains("relative link \"SK-MIG-002-other.md\"", StringComparison.Ordinal));
    }

    private static string Section(string editorConfig, string header)
    {
        var start = editorConfig.IndexOf(header, StringComparison.Ordinal);
        Assert.True(start >= 0, $"no {header} section");
        var next = editorConfig.IndexOf("\n[", start + header.Length, StringComparison.Ordinal);
        return next < 0 ? editorConfig[start..] : editorConfig[start..next];
    }

    internal static YamlMappingNode FrontMatter(string skill)
    {
        var end = skill.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        var stream = new YamlStream();
        stream.Load(new StringReader(skill[4..end]));
        return (YamlMappingNode)stream.Documents[0].RootNode;
    }

    internal static string? Scalar(YamlMappingNode node, string key) =>
        ((YamlScalarNode)node.Children[new YamlScalarNode(key)]).Value;
}
