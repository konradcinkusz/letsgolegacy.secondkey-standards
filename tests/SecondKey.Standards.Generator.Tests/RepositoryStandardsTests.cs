using SecondKey.Standards.Generator.Model;
using SecondKey.Standards.Generator.Parsing;
using SecondKey.Standards.Generator.Tests.Support;
using Xunit;

namespace SecondKey.Standards.Generator.Tests;

/// <summary>
/// The checks that hold this repository's own standards to their contract. The first one is the
/// C4-a acceptance criterion, asserted: every rule has an id, a severity, a rationale, a compliant
/// and a non-compliant example.
/// </summary>
public class RepositoryStandardsTests
{
    private static readonly StandardsSet Standards =
        StandardsLoader.Load(RepositoryRoot.Layout, PackConfig.Load(RepositoryRoot.Layout.PackConfigPath));

    [Fact]
    public void Every_rule_has_an_id_a_severity_a_rationale_and_both_examples()
    {
        Assert.True(Standards.IsValid, string.Join('\n', Standards.Problems));
        Assert.NotEmpty(Standards.Rules);

        Assert.All(Standards.Rules, rule =>
        {
            Assert.Matches(Patterns.RuleId(), rule.Id);
            Assert.True(Enum.IsDefined(rule.Severity));
            Assert.NotEmpty(rule.Section(RuleParser.RationaleSection)!.Content);
            Assert.True(rule.Section(RuleParser.NonCompliantSection)!.CodeBlocks > 0, rule.Id);
            Assert.True(rule.Section(RuleParser.CompliantSection)!.CodeBlocks > 0, rule.Id);
        });
    }

    [Theory]
    [InlineData("PORTCULLIS_MIG_SYSTEM_WEB")]
    [InlineData("PORTCULLIS_MIG_HTTPCONTEXT_CURRENT")]
    [InlineData("PORTCULLIS_MIG_SYNC_OVER_ASYNC")]
    [InlineData("PORTCULLIS_MIG_CONFIGURATION_MANAGER")]
    public void Each_portcullis_migration_rule_is_mapped_by_exactly_one_standard(string diagnostic)
    {
        var owners = Standards.Rules.Where(rule => string.Equals(rule.PortcullisRule, diagnostic, StringComparison.Ordinal));

        Assert.Single(owners);
    }

    [Fact]
    public void The_migration_topics_the_ticket_requires_are_covered()
    {
        // C4-a lists these topics; each must be a rule of its own, found here by the words the
        // agent would search for in its title.
        string[] requiredTitleFragments =
        [
            "System.Web",
            "HttpContext.Current",
            "never block on a task",
            "IOptions<T>",
            "StringComparison",
            "culture",
            "constructor injection",
            "IHttpClientFactory",
            "EF6",
            "Preserve behaviour",
        ];

        Assert.All(requiredTitleFragments, fragment =>
            Assert.Contains(Standards.Rules, rule => rule.Title.Contains(fragment, StringComparison.Ordinal)));
    }

    [Fact]
    public void Every_architecture_rule_restates_a_principle_and_points_at_the_constitution()
    {
        var architectureRules = Standards.Rules.Where(rule => rule.Id.StartsWith("SK-ARCH-", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(architectureRules);
        Assert.All(architectureRules, rule =>
        {
            Assert.NotNull(rule.Principle);
            Assert.Contains(
                $"00-REFERENCE-ARCHITECTURE.md#{rule.Principle!.ToLowerInvariant()}",
                rule.Statement,
                StringComparison.Ordinal);
        });
    }
}
