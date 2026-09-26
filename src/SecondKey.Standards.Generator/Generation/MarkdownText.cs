using System.Text;

namespace SecondKey.Standards.Generator.Generation;

internal static class MarkdownText
{
    /// <summary>A table cell: pipes escaped so a title cannot split its row.</summary>
    public static string Cell(string text) => text.Replace("|", "\\|", StringComparison.Ordinal);

    public static string Code(string text) => $"`{text}`";

    public static string Table(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();
        builder.Append("| ").AppendJoin(" | ", headers).Append(" |\n");
        builder.Append('|').AppendJoin("", headers.Select(_ => "---|")).Append('\n');
        foreach (var row in rows)
        {
            builder.Append("| ").AppendJoin(" | ", row.Select(Cell)).Append(" |\n");
        }

        return builder.ToString().TrimEnd('\n');
    }

    /// <summary>
    /// A YAML folded block scalar (<c>key: &gt;-</c>), wrapped at about 76 columns: readable in a
    /// diff, and free of the quoting rules a plain scalar would impose on the text.
    /// </summary>
    public static string YamlFolded(string key, string value)
    {
        var lines = new List<string>();
        var line = new StringBuilder();
        foreach (var word in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > 76)
            {
                lines.Add(line.ToString());
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            lines.Add(line.ToString());
        }

        return $"{key}: >-\n" + string.Join('\n', lines.Select(l => "  " + l));
    }

    /// <summary>A YAML double-quoted scalar.</summary>
    public static string YamlQuoted(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
