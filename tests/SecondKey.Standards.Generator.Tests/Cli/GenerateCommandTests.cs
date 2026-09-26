using SecondKey.Standards.Generator.Cli;
using SecondKey.Standards.Generator.Generation;
using SecondKey.Standards.Generator.Tests.Support;
using Xunit;

namespace SecondKey.Standards.Generator.Tests.Cli;

public class GenerateCommandTests
{
    private static (int ExitCode, string Output, string Error) Run(TestRepository repository, params string[] args) =>
        CommandLineTests.Run(repository.Root, null, args);

    [Fact]
    public void Check_fails_before_anything_is_generated_and_names_the_missing_files()
    {
        using var repository = new TestRepository();
        repository.WriteRule("SK-MIG-001.md", RuleText.Valid());

        var (exitCode, output, error) = Run(repository, "generate", "--check");

        Assert.Equal(ExitCodes.CheckFailed, exitCode);
        Assert.Contains($"{GeneratedPaths.SkillFile(TestRepository.SkillName)}: missing", error, StringComparison.Ordinal);
        Assert.Contains("Run: secondkey-standards generate", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_passes_after_generate_and_fails_again_when_a_rule_changes()
    {
        using var repository = new TestRepository();
        repository.WriteRule("SK-MIG-001.md", RuleText.Valid());

        Assert.Equal(ExitCodes.Success, Run(repository, "generate").ExitCode);
        Assert.Equal(ExitCodes.Success, Run(repository, "generate", "--check").ExitCode);

        repository.WriteRule("SK-MIG-001.md", RuleText.Valid().Replace("State the comparison", "State every comparison", StringComparison.Ordinal));
        var (exitCode, _, error) = Run(repository, "generate", "--check");

        Assert.Equal(ExitCodes.CheckFailed, exitCode);
        Assert.Contains($"{GeneratedPaths.Reference(TestRepository.SkillName, "SK-MIG-001")}: stale", error, StringComparison.Ordinal);
        Assert.Contains($"{GeneratedPaths.Manifest}: stale", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_reports_a_hand_edited_generated_file()
    {
        using var repository = new TestRepository();
        repository.WriteRule("SK-MIG-001.md", RuleText.Valid());
        Run(repository, "generate");

        File.AppendAllText(Path.Combine(repository.Root, "generated", "config", ".globalconfig"), "dotnet_diagnostic.CA1310.severity = none\n");
        var (exitCode, _, error) = Run(repository, "generate", "--check");

        Assert.Equal(ExitCodes.CheckFailed, exitCode);
        Assert.Contains("generated/config/.globalconfig: stale", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_standards_generate_nothing()
    {
        using var repository = new TestRepository();
        repository.WriteRule("SK-MIG-001.md", RuleText.WithFrontMatterLine("id", null));

        var (exitCode, _, error) = Run(repository, "generate");

        Assert.Equal(ExitCodes.CheckFailed, exitCode);
        Assert.Contains("standards/SK-MIG-001.md:", error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(repository.Root, "generated")));
    }

    [Fact]
    public void Check_passes_on_this_repository()
    {
        var (exitCode, output, error) = CommandLineTests.Run(RepositoryRoot.Layout.Root, null, "generate", "--check");

        Assert.True(exitCode == ExitCodes.Success, error);
        Assert.Contains("is up to date", output, StringComparison.Ordinal);
    }
}
