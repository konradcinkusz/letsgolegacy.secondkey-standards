using SecondKey.Standards.Generator.Cli;
using SecondKey.Standards.Generator.Drift;
using SecondKey.Standards.Generator.Model;
using SecondKey.Standards.Generator.Tests.Support;
using Xunit;

namespace SecondKey.Standards.Generator.Tests.Cli;

public sealed class DriftCheckCommandTests : IDisposable
{
    private readonly StandardsSource source = new();

    public DriftCheckCommandTests()
    {
        source.Release("0.1.0", "# Changelog\n\n## [0.1.0] — 2026-09-01\n\n- First.\n", StandardsSource.Manifest("0.1.0", "SK-MIG-001:error"));
    }

    public void Dispose() => source.Dispose();

    [Fact]
    public async Task The_command_passes_for_a_current_pin_and_fails_for_a_stale_one()
    {
        using var current = new Consumer().WithCentralPin("0.1.0");
        using var stale = new Consumer().WithCentralPin("0.0.1");

        var (currentExit, currentOutput, _) = await CommandLineTests.RunAsync(current.Root, null, "drift-check", "--source", source.Path);
        var (staleExit, _, staleError) = await CommandLineTests.RunAsync(stale.Root, null, "drift-check", "--source", source.Path);

        Assert.Equal(ExitCodes.Success, currentExit);
        Assert.Contains("Up to date", currentOutput, StringComparison.Ordinal);
        Assert.Equal(ExitCodes.CheckFailed, staleExit);
        Assert.Contains("error: Behind: this repository pins standards 0.0.1; the latest release is 0.1.0.", staleError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inside_github_actions_the_result_is_an_annotation_a_summary_and_outputs()
    {
        using var stale = new Consumer().WithCentralPin("0.0.1");
        var summary = Path.Combine(stale.Root, "summary.md");
        var outputs = Path.Combine(stale.Root, "outputs.txt");
        var environment = new Dictionary<string, string>
        {
            ["GITHUB_ACTIONS"] = "true",
            ["GITHUB_STEP_SUMMARY"] = summary,
            ["GITHUB_OUTPUT"] = outputs,
        };

        var (_, output, _) = await CommandLineTests.RunAsync(stale.Root, environment, "drift-check", "--source", source.Path);

        Assert.Contains("::error::Behind:", output, StringComparison.Ordinal);
        Assert.Contains("### Second Key standards drift check: behind", await File.ReadAllTextAsync(summary, TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(
            "status=behind\npinned-version=0.0.1\nlatest-version=0.1.0\n",
            await File.ReadAllTextAsync(outputs, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_relative_repository_and_source_resolve_against_the_current_directory()
    {
        using var consumer = new Consumer().WithCentralPin("0.1.0");
        var parent = Path.GetDirectoryName(consumer.Root)!;
        var relativeSource = Path.GetRelativePath(parent, source.Path);

        var (exitCode, _, error) = await CommandLineTests.RunAsync(
            parent, null, "drift-check", "--repo", Path.GetFileName(consumer.Root), "--source", relativeSource);

        Assert.True(exitCode == ExitCodes.Success, error);
    }

    [Fact]
    public async Task A_version_option_that_is_not_a_version_is_a_usage_error()
    {
        using var consumer = new Consumer();

        var (exitCode, _, error) = await CommandLineTests.RunAsync(consumer.Root, null, "drift-check", "--pinned-version", "latest");

        Assert.Equal(ExitCodes.CouldNotRun, exitCode);
        Assert.Contains("MAJOR.MINOR.PATCH", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://github.com/konradcinkusz/letsgolegacy.secondkey-standards", true)]
    [InlineData("git@github.com:konradcinkusz/letsgolegacy.secondkey-standards.git", true)]
    [InlineData("file:///srv/mirrors/standards.git", true)]
    [InlineData("mirrors/standards.git", false)]
    public void Urls_are_passed_through_and_paths_are_made_absolute(string source, bool isUrl)
    {
        var resolved = CommandLineApp.ResolveSource(source, "/work");

        Assert.Equal(isUrl ? source : Path.GetFullPath("/work/mirrors/standards.git"), resolved);
    }

    [Fact]
    public void The_defaults_are_this_repositorys_pack()
    {
        var pack = PackConfig.Load(RepositoryRoot.Layout.PackConfigPath);

        Assert.Equal(DriftDefaults.Source, pack.Repository);
        Assert.Equal(DriftDefaults.PackageId, pack.Package.Id);
        Assert.Equal(DriftDefaults.SkillName, pack.Skill.Name);
    }
}
