namespace SecondKey.Standards.Generator.Cli;

/// <summary>
/// Everything a command takes from the process — streams, working directory, environment —
/// passed in rather than read from statics, so the tests run every command in-process against
/// throwaway repositories.
/// </summary>
public sealed record CliContext(
    TextWriter Output,
    TextWriter Error,
    string CurrentDirectory,
    Func<string, string?> Environment)
{
    public static CliContext FromProcess() => new(
        Console.Out,
        Console.Error,
        Directory.GetCurrentDirectory(),
        System.Environment.GetEnvironmentVariable);

    public Reporter CreateReporter() =>
        new(Output, Error, Reporter.RunningInGitHubActions(Environment));
}
