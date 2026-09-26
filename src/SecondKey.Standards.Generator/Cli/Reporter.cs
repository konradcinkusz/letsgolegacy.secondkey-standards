using SecondKey.Standards.Generator.Diagnostics;

namespace SecondKey.Standards.Generator.Cli;

/// <summary>
/// Writes results for people and, inside GitHub Actions, also as workflow-command annotations,
/// so a broken rule is marked on the file and line in the pull request's diff instead of only
/// in a log nobody opens.
/// </summary>
public sealed class Reporter(TextWriter output, TextWriter error, bool githubAnnotations)
{
    public TextWriter Output { get; } = output;

    public TextWriter Error { get; } = error;

    public static bool RunningInGitHubActions(Func<string, string?> environment) =>
        string.Equals(environment("GITHUB_ACTIONS"), "true", StringComparison.Ordinal);

    public void Info(string message) => Output.WriteLine(message);

    public void Fail(string message)
    {
        Error.WriteLine($"error: {message}");
        if (githubAnnotations)
        {
            Output.WriteLine($"::error::{EscapeData(message)}");
        }
    }

    public void Problems(IReadOnlyList<Problem> problems)
    {
        foreach (var problem in problems)
        {
            Error.WriteLine(problem.ToString());
            if (githubAnnotations)
            {
                var location = problem.Line is { } line
                    ? $"file={EscapeProperty(problem.Path)},line={line.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                    : $"file={EscapeProperty(problem.Path)}";
                Output.WriteLine($"::error {location}::{EscapeData(problem.Message)}");
            }
        }
    }

    // https://docs.github.com/actions/reference/workflow-commands-for-github-actions — message
    // data escapes %, CR and LF; property values additionally escape : and ,.
    private static string EscapeData(string value) => value
        .Replace("%", "%25", StringComparison.Ordinal)
        .Replace("\r", "%0D", StringComparison.Ordinal)
        .Replace("\n", "%0A", StringComparison.Ordinal);

    private static string EscapeProperty(string value) => EscapeData(value)
        .Replace(":", "%3A", StringComparison.Ordinal)
        .Replace(",", "%2C", StringComparison.Ordinal);
}
