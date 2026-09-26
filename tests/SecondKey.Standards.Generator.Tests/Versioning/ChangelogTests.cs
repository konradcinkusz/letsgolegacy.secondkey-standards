using SecondKey.Standards.Generator.Model;
using SecondKey.Standards.Generator.Versioning;
using Xunit;

namespace SecondKey.Standards.Generator.Tests.Versioning;

public class ChangelogTests
{
    private const string Text = """
        # Changelog

        Intro.

        ## [1.2.0] — Unreleased

        ### Added

        - Rule three.

        ## [1.1.0] — 2026-09-10

        - Rule two.

        ## [1.0.0] — 2026-09-01

        - Rule one.
        """;

    [Fact]
    public void Entries_are_parsed_with_their_release_state_and_text()
    {
        var changelog = Changelog.Parse(Text, "CHANGELOG.md", out var problems);

        Assert.Empty(problems);
        Assert.Equal(["1.2.0", "1.1.0", "1.0.0"], changelog.Entries.Select(e => e.Version.ToString()));
        Assert.False(changelog.Entries[0].IsReleased);
        Assert.Equal(new DateOnly(2026, 9, 10), changelog.Entries[1].ReleaseDate);
        Assert.Contains("- Rule three.", changelog.Entries[0].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Rule two", changelog.Entries[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Between_returns_what_a_pinned_consumer_is_missing()
    {
        var changelog = Changelog.Parse(Text, "CHANGELOG.md", out _);

        var missing = changelog.Between(SemanticVersion.Parse("1.0.0"), SemanticVersion.Parse("1.2.0"));

        Assert.Equal(["1.2.0", "1.1.0"], missing.Select(e => e.Version.ToString()));
    }

    [Theory]
    [InlineData("## [1.0.0]\n", "a version heading must read")]
    [InlineData("## [one] — 2026-09-01\n", "a version heading must read")]
    [InlineData("## [1.0.0] — soon\n", "neither a release date")]
    [InlineData("## [1.0.0] — 2026-09-01\n\n## [1.1.0] — Unreleased\n", "keep the newest first")]
    public void Malformed_or_misordered_headings_are_reported(string entries, string expected)
    {
        Changelog.Parse("# Changelog\n\n" + entries, "CHANGELOG.md", out var problems);

        Assert.Contains(problems, p => p.Message.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void The_repository_changelog_has_an_entry_for_the_pack_version()
    {
        var layout = Support.RepositoryRoot.Layout;
        var changelog = Changelog.Parse(File.ReadAllText(layout.Combine(Changelog.RelativePath)), Changelog.RelativePath, out var problems);
        var pack = PackConfig.Load(layout.PackConfigPath);

        Assert.Empty(problems);
        Assert.NotNull(changelog.Find(pack.ParsedVersion));
    }
}
