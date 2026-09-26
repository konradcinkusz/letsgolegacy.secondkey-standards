namespace SecondKey.Standards.Generator.Parsing;

/// <summary>A rule file split into its YAML front matter and its Markdown body.</summary>
/// <param name="Yaml">The text between the two <c>---</c> delimiters.</param>
/// <param name="YamlFirstLine">The 1-based file line of the first YAML line.</param>
/// <param name="Body">Everything after the closing delimiter.</param>
/// <param name="BodyFirstLine">The 1-based file line of the first body line.</param>
internal sealed record FrontMatterSplit(string Yaml, int YamlFirstLine, string Body, int BodyFirstLine);

internal static class FrontMatter
{
    private const string Delimiter = "---";

    /// <summary>
    /// Splits normalised text (see <see cref="SourceText.Normalise"/>). Returns null and an
    /// explanation when the file does not open with a front-matter block.
    /// </summary>
    public static FrontMatterSplit? Split(string text, out string? error, out int errorLine)
    {
        var lines = text.Split('\n');
        errorLine = 1;

        if (lines.Length == 0 || !IsDelimiter(lines[0]))
        {
            error = "the file must start with a YAML front-matter block: a line containing only ---, "
                + "the metadata, and a closing --- line";
            return null;
        }

        for (var i = 1; i < lines.Length; i++)
        {
            if (IsDelimiter(lines[i]))
            {
                error = null;
                var yaml = string.Join('\n', lines[1..i]);
                var body = i + 1 < lines.Length ? string.Join('\n', lines[(i + 1)..]) : "";
                return new FrontMatterSplit(yaml, YamlFirstLine: 2, body, BodyFirstLine: i + 2);
            }
        }

        error = "the front-matter block opened on line 1 is never closed with a --- line";
        return null;
    }

    private static bool IsDelimiter(string line) => string.Equals(line.TrimEnd(), Delimiter, StringComparison.Ordinal);
}
