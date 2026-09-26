using SecondKey.Standards.Generator.Model;
using SecondKey.Standards.Generator.Parsing;
using SecondKey.Standards.Generator.Tests.Support;
using Xunit;

namespace SecondKey.Standards.Generator.Tests.Parsing;

public class RuleParserTests
{
    private const string Path = "standards/SK-MIG-001-state-the-comparison.md";

    private static readonly PackConfig Pack = new()
    {
        Name = "test-pack",
        Categories = ["globalization", "hosting"],
        PortcullisRules = [TestRepository.PortcullisRule],
    };

    private static (Rule? Rule, IReadOnlyList<Diagnostics.Problem> Problems) Parse(string text, string path = Path) =>
        RuleParser.Parse(path, text, Pack);

    [Fact]
    public void A_complete_rule_parses_into_typed_fields_and_sections()
    {
        var text = RuleText.Valid(frontMatter: $"""
            id: SK-MIG-001
            title: State the comparison
            severity: error
            category: globalization
            appliesTo:
              - "*.cs"
              - "*.cshtml"
            portcullisRule: {TestRepository.PortcullisRule}
            principle: P5
            analyzers:
              - CA1310
            """);

        var (rule, problems) = Parse(text);

        Assert.Empty(problems);
        Assert.NotNull(rule);
        Assert.Equal("SK-MIG-001", rule.Id);
        Assert.Equal("State the comparison", rule.Title);
        Assert.Equal(Severity.Error, rule.Severity);
        Assert.Equal("globalization", rule.Category);
        Assert.Equal(["*.cs", "*.cshtml"], rule.AppliesTo);
        Assert.Equal(TestRepository.PortcullisRule, rule.PortcullisRule);
        Assert.Equal("P5", rule.Principle);
        Assert.Equal([TestRepository.PortcullisRule, "CA1310"], rule.Diagnostics);
        Assert.Equal("Every culture-sensitive comparison states its StringComparison.", rule.Statement);
        Assert.Equal(["Rationale", "Non-compliant", "Compliant"], rule.Sections.Select(s => s.Heading));
        Assert.Equal(1, rule.Section("Compliant")!.CodeBlocks);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("title")]
    [InlineData("severity")]
    [InlineData("category")]
    [InlineData("appliesTo")]
    public void A_missing_required_key_is_reported_with_the_file_and_the_key(string key)
    {
        var (rule, problems) = Parse(RuleText.WithFrontMatterLine(key, line: null));

        Assert.Null(rule);
        var problem = Assert.Single(problems);
        Assert.Equal(Path, problem.Path);
        Assert.Contains($"\"{key}\" is missing", problem.Message, StringComparison.Ordinal);
        Assert.StartsWith(Path + ":", problem.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_without_front_matter_is_rejected_by_name()
    {
        var (rule, problems) = Parse("# SK-MIG-001\n\nNo metadata at all.\n");

        Assert.Null(rule);
        var problem = Assert.Single(problems);
        Assert.Equal(Path, problem.Path);
        Assert.Equal(1, problem.Line);
        Assert.Contains("front-matter block", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unclosed_front_matter_block_is_rejected()
    {
        var (_, problems) = Parse("---\nid: SK-MIG-001\ntitle: never closed\n");

        Assert.Contains(problems, p => p.Message.Contains("never closed", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("severity: fatal", "severity \"fatal\" must be one of: error, warning, suggestion")]
    [InlineData("category: security", "category \"security\" is not one of the categories")]
    [InlineData("id: MIG-1", "id \"MIG-1\" must be upper-case segments ending in a three-digit number")]
    public void A_value_outside_its_vocabulary_is_rejected(string line, string expected)
    {
        var key = line[..line.IndexOf(':', StringComparison.Ordinal)];

        var (rule, problems) = Parse(RuleText.WithFrontMatterLine(key, line));

        Assert.Null(rule);
        Assert.Contains(problems, p => p.Message.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void An_unknown_key_is_rejected_rather_than_ignored()
    {
        var (_, problems) = Parse(RuleText.WithFrontMatterLine("owner", "owner: somebody"));

        var problem = Assert.Single(problems);
        Assert.Contains("unknown front-matter key \"owner\"", problem.Message, StringComparison.Ordinal);
        Assert.Equal(8, problem.Line);
    }

    [Fact]
    public void A_portcullis_rule_the_gate_does_not_ship_is_rejected()
    {
        var (_, problems) = Parse(RuleText.WithFrontMatterLine("portcullisRule", "portcullisRule: PORTCULLIS_MIG_TYPO"));

        Assert.Contains(problems, p => p.Message.Contains("not a known Portcullis diagnostic", StringComparison.Ordinal));
    }

    [Fact]
    public void A_portcullis_rule_with_hyphens_is_rejected()
    {
        var (_, problems) = Parse(RuleText.WithFrontMatterLine("portcullisRule", "portcullisRule: PORTCULLIS-MIG-EXAMPLE"));

        Assert.Contains(problems, p => p.Message.Contains("must look like PORTCULLIS_<SLUG>", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("principle: P16")]
    [InlineData("principle: 5")]
    public void A_principle_outside_the_constitution_is_rejected(string line)
    {
        var (_, problems) = Parse(RuleText.WithFrontMatterLine("principle", line));

        Assert.Contains(problems, p => p.Message.Contains("principle of the architecture constitution", StringComparison.Ordinal));
    }

    [Fact]
    public void An_analyzer_that_is_not_an_sdk_id_is_rejected()
    {
        var (_, problems) = Parse(RuleText.WithFrontMatterLine("analyzers", $"analyzers:\n  - {TestRepository.PortcullisRule}"));

        Assert.Contains(problems, p => p.Message.Contains("must be a .NET SDK analyzer id", StringComparison.Ordinal));
    }

    [Fact]
    public void AppliesTo_must_be_a_list()
    {
        var text = RuleText.Valid(frontMatter: """
            id: SK-MIG-001
            title: State the comparison
            severity: warning
            category: globalization
            appliesTo: "*.cs"
            """);

        var (_, problems) = Parse(text);

        Assert.Contains(problems, p => p.Message.Contains("\"appliesTo\" must be a list", StringComparison.Ordinal));
    }

    [Fact]
    public void An_unquoted_glob_gets_a_hint_about_yaml_aliases()
    {
        var text = RuleText.Valid(frontMatter: """
            id: SK-MIG-001
            title: State the comparison
            severity: warning
            category: globalization
            appliesTo:
              - *.cs
            """);

        var (_, problems) = Parse(text);

        var problem = Assert.Single(problems);
        Assert.Contains("not valid YAML", problem.Message, StringComparison.Ordinal);
        Assert.Contains("quote file patterns", problem.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/src/*.cs")]
    [InlineData("[*.cs]")]
    [InlineData("*.cs # comment")]
    public void AppliesTo_entries_must_be_editorconfig_patterns(string pattern)
    {
        var (_, problems) = Parse(RuleText.WithFrontMatterLine("appliesTo", $"appliesTo:\n  - \"{pattern}\""));

        Assert.Contains(problems, p => p.Message.Contains("is not an .editorconfig file pattern", StringComparison.Ordinal));
    }

    [Fact]
    public void The_file_name_must_start_with_the_id()
    {
        var (_, problems) = Parse(RuleText.Valid(), path: "standards/SK-MIG-002-something-else.md");

        Assert.Contains(problems, p => p.Message.Contains("file name must be \"SK-MIG-001.md\"", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("standards/SK-MIG-001.md")]
    [InlineData("standards/SK-MIG-001-state-the-comparison.md")]
    public void The_file_name_may_be_the_id_or_the_id_and_a_slug(string path)
    {
        var (rule, problems) = Parse(RuleText.Valid(), path);

        Assert.Empty(problems);
        Assert.NotNull(rule);
    }

    [Fact]
    public void Windows_line_endings_and_a_byte_order_mark_are_tolerated()
    {
        var text = "﻿" + RuleText.Valid().Replace("\n", "\r\n", StringComparison.Ordinal);

        var (rule, problems) = Parse(text);

        Assert.Empty(problems);
        Assert.NotNull(rule);
    }

    [Fact]
    public void A_missing_rationale_is_reported()
    {
        var body = RuleText.ValidBody.Replace("## Rationale\n\nNLS and ICU disagree.\n\n", "", StringComparison.Ordinal);

        var (rule, problems) = Parse(RuleText.Valid(body: body));

        Assert.Null(rule);
        Assert.Contains(problems, p => p.Message.Contains("\"## Rationale\" section is missing", StringComparison.Ordinal));
    }

    [Fact]
    public void An_empty_rationale_is_reported()
    {
        var body = RuleText.ValidBody.Replace("NLS and ICU disagree.", "", StringComparison.Ordinal);

        var (_, problems) = Parse(RuleText.Valid(body: body));

        Assert.Contains(problems, p => p.Message.Contains("\"## Rationale\" section is empty", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Compliant", "sku.StartsWith(\"GIFT-\", StringComparison.Ordinal);")]
    [InlineData("Non-compliant", "sku.StartsWith(\"GIFT-\");")]
    public void An_example_section_without_a_code_block_is_reported(string section, string code)
    {
        var body = RuleText.ValidBody.Replace($"```csharp\n{code}\n```", "Described in prose instead.", StringComparison.Ordinal);

        var (_, problems) = Parse(RuleText.Valid(body: body));

        var problem = Assert.Single(problems);
        Assert.Equal($"the \"## {section}\" section has no fenced code example", problem.Message);
    }

    [Fact]
    public void An_empty_code_block_is_not_an_example()
    {
        var body = RuleText.ValidBody.Replace("sku.StartsWith(\"GIFT-\", StringComparison.Ordinal);", "   ", StringComparison.Ordinal);

        var (_, problems) = Parse(RuleText.Valid(body: body));

        Assert.Contains(problems, p => p.Message.Contains("\"## Compliant\" section has no fenced code example", StringComparison.Ordinal));
    }

    [Fact]
    public void A_level_one_heading_is_rejected()
    {
        var (_, problems) = Parse(RuleText.Valid(body: "\n# SK-MIG-001\n" + RuleText.ValidBody));

        Assert.Contains(problems, p => p.Message.Contains("level-one (#) heading is not allowed", StringComparison.Ordinal));
    }

    [Fact]
    public void An_unknown_section_is_rejected()
    {
        var (_, problems) = Parse(RuleText.Valid(body: RuleText.ValidBody + "\n\n## Examples\n\nMore.\n"));

        Assert.Contains(problems, p => p.Message.Contains("section \"## Examples\" is not one of", StringComparison.Ordinal));
    }

    [Fact]
    public void A_duplicated_section_is_rejected()
    {
        var (_, problems) = Parse(RuleText.Valid(body: RuleText.ValidBody + "\n\n## Rationale\n\nAgain.\n"));

        Assert.Contains(problems, p => p.Message.Contains("appears more than once", StringComparison.Ordinal));
    }

    [Fact]
    public void A_rule_needs_a_statement_before_its_first_section()
    {
        var body = RuleText.ValidBody.Replace("Every culture-sensitive comparison states its StringComparison.", "", StringComparison.Ordinal);

        var (_, problems) = Parse(RuleText.Valid(body: body));

        Assert.Contains(problems, p => p.Message.Contains("must open with the rule's statement", StringComparison.Ordinal));
    }

    [Fact]
    public void Optional_sections_are_accepted()
    {
        var body = RuleText.ValidBody + "\n\n## Migration\n\nSteps.\n\n## Flag instead of fixing\n\nFlags.\n\n## References\n\nLinks.\n";

        var (rule, problems) = Parse(RuleText.Valid(body: body));

        Assert.Empty(problems);
        Assert.Equal(6, rule!.Sections.Count);
    }
}
