using SecondKey.Standards.Generator.Cli;
using SecondKey.Standards.Generator.Drift;
using SecondKey.Standards.Generator.Model;
using SecondKey.Standards.Generator.Tests.Support;
using Xunit;

namespace SecondKey.Standards.Generator.Tests.Drift;

/// <summary>
/// The drift check against real git sources. C4-c's acceptance criterion is the first two tests: a
/// current pin passes, a stale pin fails — and says what changed.
/// </summary>
public sealed class DriftCheckerTests : IDisposable
{
    private const string Changelog = """
        # Changelog

        ## [1.1.0] — 2026-09-20

        ### Added

        - SK-MIG-002, a new rule.

        ### Changed

        - SK-MIG-001 is now an error.

        ## [1.0.0] — 2026-09-01

        - First release.
        """;

    private readonly StandardsSource source = new();

    public DriftCheckerTests()
    {
        source.Release("1.0.0", Changelog, StandardsSource.Manifest("1.0.0", "SK-MIG-001:warning", "SK-MIG-003:error", "SK-MIG-004:error:old"));
        source.Release("1.1.0", Changelog, StandardsSource.Manifest("1.1.0", "SK-MIG-001:error", "SK-MIG-002:warning", "SK-MIG-004:error:new"));
    }

    public void Dispose() => source.Dispose();

    private Task<DriftResult> CheckAsync(Consumer consumer, SemanticVersion? pinned = null, SemanticVersion? latest = null, string? sourcePath = null)
    {
        var releases = new GitReleaseSource(sourcePath ?? source.Path);
        return DriftChecker.CheckAsync(
            new DriftOptions(consumer.Root, DriftDefaults.PackageId, DriftDefaults.SkillName, pinned, latest),
            releases,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_current_pin_passes()
    {
        using var consumer = new Consumer().WithCentralPin("1.1.0").WithSkill("1.1.0");

        var result = await CheckAsync(consumer);

        Assert.Equal(DriftStatus.Current, result.Status);
        Assert.Equal(ExitCodes.Success, result.ExitCode);
    }

    [Fact]
    public async Task A_stale_pin_fails_and_says_what_changed()
    {
        using var consumer = new Consumer().WithCentralPin("1.0.0").WithSkill("1.0.0");

        var result = await CheckAsync(consumer);
        var details = string.Join('\n', result.Details);

        Assert.Equal(DriftStatus.Behind, result.Status);
        Assert.Equal(ExitCodes.CheckFailed, result.ExitCode);
        Assert.Equal("Behind: this repository pins standards 1.0.0; the latest release is 1.1.0.", result.Summary);
        Assert.Contains("added    SK-MIG-002", details, StringComparison.Ordinal);
        Assert.Contains("changed  SK-MIG-001   severity warning → error", details, StringComparison.Ordinal);
        Assert.Contains("changed  SK-MIG-004   text", details, StringComparison.Ordinal);
        Assert.Contains("removed  SK-MIG-003", details, StringComparison.Ordinal);
        Assert.Contains("## [1.1.0] — 2026-09-20", details, StringComparison.Ordinal);
        Assert.DoesNotContain("## [1.0.0]", details, StringComparison.Ordinal);
        Assert.Contains("reference SecondKey.Standards 1.1.0, and copy the skill again from tag v1.1.0", details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pins_that_disagree_fail_even_when_one_is_current()
    {
        using var consumer = new Consumer().WithCentralPin("1.1.0").WithSkill("1.0.0");

        var result = await CheckAsync(consumer);

        Assert.Equal(DriftStatus.Disagree, result.Status);
        Assert.Equal(ExitCodes.CheckFailed, result.ExitCode);
        Assert.Contains("the migration agent and the gate are working from different standards", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unpinned_repository_fails()
    {
        using var consumer = new Consumer().With("src/Shop/Shop.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var result = await CheckAsync(consumer);

        Assert.Equal(DriftStatus.Unpinned, result.Status);
        Assert.Equal(ExitCodes.CheckFailed, result.ExitCode);
    }

    [Fact]
    public async Task A_pin_newer_than_every_release_fails()
    {
        using var consumer = new Consumer().WithCentralPin("2.0.0");

        var result = await CheckAsync(consumer);

        Assert.Equal(DriftStatus.Ahead, result.Status);
        Assert.Contains("2.0.0 was never released", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pre_release_and_non_version_tags_are_not_releases()
    {
        source.Commit(Changelog, null, "Work in progress");
        source.Tag("v1.2.0-rc.1");
        source.Tag("latest");
        using var consumer = new Consumer().WithCentralPin("1.1.0");

        var result = await CheckAsync(consumer);

        Assert.Equal(DriftStatus.Current, result.Status);
    }

    [Fact]
    public async Task A_source_without_releases_cannot_be_checked_against()
    {
        using var empty = new StandardsSource();
        empty.Commit("# Changelog\n", null, "No release yet");
        using var consumer = new Consumer().WithCentralPin("1.0.0");

        var result = await CheckAsync(consumer, sourcePath: empty.Path);

        Assert.Equal(DriftStatus.NoRelease, result.Status);
        Assert.Equal(ExitCodes.CouldNotRun, result.ExitCode);
    }

    [Fact]
    public async Task An_unreachable_source_cannot_be_checked_against()
    {
        using var consumer = new Consumer().WithCentralPin("1.0.0");

        var result = await CheckAsync(consumer, sourcePath: Path.Combine(Path.GetTempPath(), "secondkey-no-such-source-" + Guid.NewGuid().ToString("N")));

        Assert.Equal(DriftStatus.SourceUnavailable, result.Status);
        Assert.Equal(ExitCodes.CouldNotRun, result.ExitCode);
    }

    [Fact]
    public async Task An_explicit_pin_replaces_detection()
    {
        using var consumer = new Consumer();

        var result = await CheckAsync(consumer, pinned: SemanticVersion.Parse("1.0.0"));

        Assert.Equal(DriftStatus.Behind, result.Status);
        Assert.Contains("--pinned-version 1.0.0", result.Details[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_given_latest_version_needs_no_tag_listing()
    {
        using var consumer = new Consumer().WithCentralPin("1.1.0");

        var result = await CheckAsync(consumer, latest: SemanticVersion.Parse("1.1.0"), sourcePath: "/nonexistent/source");

        Assert.Equal(DriftStatus.Current, result.Status);
    }

    [Fact]
    public async Task A_stale_pin_that_was_never_released_is_called_out()
    {
        using var consumer = new Consumer().WithCentralPin("1.0.5");

        var result = await CheckAsync(consumer);

        Assert.Equal(DriftStatus.Behind, result.Status);
        Assert.Contains(result.Details, line => line.Contains("1.0.5 is not a released version", StringComparison.Ordinal));
        Assert.Contains(result.Details, line => line.Contains("no manifest was found for 1.0.5", StringComparison.Ordinal));
    }
}
