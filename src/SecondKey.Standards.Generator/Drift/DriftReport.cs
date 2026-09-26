using System.Text;
using SecondKey.Standards.Generator.Cli;

namespace SecondKey.Standards.Generator.Drift;

/// <summary>
/// Writes a drift result for people, and — inside GitHub Actions — as an error annotation, a step
/// summary and step outputs (<c>status</c>, <c>pinned-version</c>, <c>latest-version</c>), so the
/// finding is visible on the run page and usable by later steps.
/// </summary>
internal static class DriftReport
{
    public static async Task WriteAsync(DriftResult result, CliContext context, Reporter reporter, string source)
    {
        if (result.Status == DriftStatus.Current)
        {
            reporter.Info(result.Summary);
        }
        else
        {
            reporter.Fail(result.Summary);
        }

        foreach (var line in result.Details)
        {
            reporter.Info(line);
        }

        var status = StatusName(result.Status);
        if (context.Environment("GITHUB_OUTPUT") is { Length: > 0 } outputFile)
        {
            await File.AppendAllTextAsync(outputFile,
                $"status={status}\npinned-version={result.Pinned}\nlatest-version={result.Latest}\n");
        }

        if (context.Environment("GITHUB_STEP_SUMMARY") is { Length: > 0 } summaryFile)
        {
            var summary = new StringBuilder();
            summary.Append("### Second Key standards drift check: ").Append(status).Append("\n\n");
            summary.Append(result.Summary).Append("\n\n");
            summary.Append("Releases read from `").Append(source).Append("`.\n\n");
            if (result.Details.Count > 0)
            {
                summary.Append("```text\n").AppendJoin('\n', result.Details).Append("\n```\n");
            }

            await File.AppendAllTextAsync(summaryFile, summary.ToString());
        }
    }

    public static string StatusName(DriftStatus status) => status switch
    {
        DriftStatus.Current => "current",
        DriftStatus.Behind => "behind",
        DriftStatus.Ahead => "ahead",
        DriftStatus.Disagree => "disagree",
        DriftStatus.Unpinned => "unpinned",
        DriftStatus.Unreadable => "unreadable",
        DriftStatus.NoRelease => "no-release",
        DriftStatus.SourceUnavailable => "source-unavailable",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };
}
