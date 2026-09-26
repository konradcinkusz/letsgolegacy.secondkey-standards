using System.Text;
using System.Text.RegularExpressions;
using SecondKey.Standards.Generator.Model;

namespace SecondKey.Standards.Generator.Parsing;

/// <summary>
/// A rule body: the statement (everything before the first <c>##</c> heading) and its
/// level-two sections, plus anything structurally wrong with it.
/// </summary>
internal sealed record MarkdownBody(
    string Statement,
    int StatementLine,
    IReadOnlyList<RuleSection> Sections,
    IReadOnlyList<(int Line, string Message)> Issues);

/// <summary>
/// Splits a Markdown body into sections without a Markdown library: the grammar a rule needs is
/// headings and fenced code blocks, and the one subtlety — a <c>## heading</c> or <c># comment</c>
/// inside a code fence is not a heading — is handled by tracking fences explicitly.
/// </summary>
internal static partial class MarkdownBodyParser
{
    public static MarkdownBody Parse(string body, int firstLine)
    {
        var lines = body.Split('\n');
        var issues = new List<(int, string)>();
        var sections = new List<RuleSection>();

        var statement = new StringBuilder();
        var statementLine = firstLine;
        var statementStarted = false;

        string? heading = null;
        var headingLine = 0;
        var content = new StringBuilder();
        var codeBlocks = 0;

        // The open fence, if any: its character, its length, the line it opened on, and whether
        // it has had a non-blank line yet. A code block only counts as an example if it does.
        char fenceChar = default;
        var fenceLength = 0;
        var fenceLine = 0;
        var fenceHasContent = false;
        var inFence = false;

        void CloseSection()
        {
            if (heading is not null)
            {
                sections.Add(new RuleSection(heading, content.ToString().Trim(), headingLine, codeBlocks));
            }

            content.Clear();
            codeBlocks = 0;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = firstLine + i;
            var fence = Fence().Match(line);

            if (inFence)
            {
                if (fence.Success
                    && fence.Groups["marker"].Value[0] == fenceChar
                    && fence.Groups["marker"].Value.Length >= fenceLength
                    && fence.Groups["info"].Value.Trim().Length == 0)
                {
                    inFence = false;
                    if (fenceHasContent && heading is not null)
                    {
                        codeBlocks++;
                    }
                }
                else if (line.Trim().Length > 0)
                {
                    fenceHasContent = true;
                }

                Append(line, i);
                continue;
            }

            if (fence.Success)
            {
                inFence = true;
                fenceChar = fence.Groups["marker"].Value[0];
                fenceLength = fence.Groups["marker"].Value.Length;
                fenceLine = lineNumber;
                fenceHasContent = false;
                Append(line, i);
                continue;
            }

            if (HeadingOne().IsMatch(line))
            {
                issues.Add((lineNumber, "a level-one (#) heading is not allowed in a rule body; the title comes "
                    + "from the front matter's \"title\""));
                continue;
            }

            var two = HeadingTwo().Match(line);
            if (two.Success)
            {
                CloseSection();
                heading = two.Groups["text"].Value.Trim();
                headingLine = lineNumber;
                continue;
            }

            // The body is copied into the skill's references/ directory, where a link relative to
            // standards/ would point nowhere.
            foreach (Match link in RelativeLink().Matches(line))
            {
                issues.Add((lineNumber, $"relative link \"{link.Groups["target"].Value}\" would break in the generated skill; "
                    + "use an absolute URL, or cite another rule by its id"));
            }

            Append(line, i);
        }

        if (inFence)
        {
            issues.Add((fenceLine, "this code fence is never closed"));
        }

        CloseSection();
        return new MarkdownBody(statement.ToString().Trim(), statementLine, sections, issues);

        void Append(string line, int index)
        {
            if (heading is null)
            {
                if (!statementStarted && line.Trim().Length > 0)
                {
                    statementStarted = true;
                    statementLine = firstLine + index;
                }

                statement.Append(line).Append('\n');
            }
            else
            {
                content.Append(line).Append('\n');
            }
        }
    }

    // CommonMark allows up to three spaces of indentation before a fence or a heading.
    [GeneratedRegex("^ {0,3}(?<marker>`{3,}|~{3,})(?<info>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex Fence();

    [GeneratedRegex("^ {0,3}#(\\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingOne();

    [GeneratedRegex("^ {0,3}##\\s+(?<text>.*?)\\s*#*\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingTwo();

    [GeneratedRegex(@"\]\((?!https?://|mailto:|#)(?<target>[^)\s]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex RelativeLink();
}
