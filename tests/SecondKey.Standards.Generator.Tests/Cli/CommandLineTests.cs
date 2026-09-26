using SecondKey.Standards.Generator.Cli;
using SecondKey.Standards.Generator.Tests.Support;
using Xunit;

namespace SecondKey.Standards.Generator.Tests.Cli;

public class CommandLineTests
{
    internal static async Task<(int ExitCode, string Output, string Error)> RunAsync(
        string currentDirectory,
        IReadOnlyDictionary<string, string>? environment,
        params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var context = new CliContext(
            output,
            error,
            currentDirectory,
            name => environment is not null && environment.TryGetValue(name, out var value) ? value : null);

        var exitCode = await CommandLineApp.RunAsync(args, context);
        return (exitCode, output.ToString(), error.ToString());
    }

    [Fact]
    public async Task Validate_passes_on_this_repository()
    {
        var (exitCode, output, error) = await RunAsync(RepositoryRoot.Layout.Root, null, "validate");

        Assert.True(exitCode == ExitCodes.Success, error);
        Assert.Contains("rules are valid", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validate_fails_and_names_the_file_when_metadata_is_missing()
    {
        using var repository = new TestRepository();
        repository.WriteRule("SK-MIG-001.md", RuleText.WithFrontMatterLine("severity", null));

        var (exitCode, _, error) = await RunAsync(repository.Root, null, "validate");

        Assert.Equal(ExitCodes.CheckFailed, exitCode);
        Assert.Contains("standards/SK-MIG-001.md:", error, StringComparison.Ordinal);
        Assert.Contains("\"severity\" is missing", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inside_github_actions_problems_are_also_annotations()
    {
        using var repository = new TestRepository();
        repository.WriteRule("SK-MIG-001.md", RuleText.WithFrontMatterLine("severity", "severity: fatal"));

        var (_, output, _) = await RunAsync(repository.Root, new Dictionary<string, string> { ["GITHUB_ACTIONS"] = "true" }, "validate");

        Assert.Contains("::error file=standards/SK-MIG-001.md,line=4::", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Root_is_discovered_from_a_subdirectory()
    {
        using var repository = new TestRepository();
        repository.WriteRule("SK-MIG-001.md", RuleText.Valid());

        var (exitCode, _, error) = await RunAsync(Path.Combine(repository.Root, "standards"), null, "validate");

        Assert.True(exitCode == ExitCodes.Success, error);
    }

    [Fact]
    public async Task Outside_a_standards_repository_the_command_cannot_run()
    {
        var empty = Directory.CreateTempSubdirectory("secondkey-empty-");
        try
        {
            var (exitCode, _, error) = await RunAsync(empty.FullName, null, "validate");

            Assert.Equal(ExitCodes.CouldNotRun, exitCode);
            Assert.Contains("catalog/pack.json", error, StringComparison.Ordinal);
        }
        finally
        {
            empty.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task An_unknown_command_is_a_usage_error()
    {
        var (exitCode, _, error) = await RunAsync(RepositoryRoot.Layout.Root, null, "publish");

        Assert.Equal(ExitCodes.CouldNotRun, exitCode);
        Assert.Contains("publish", error, StringComparison.Ordinal);
    }
}
