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

    /// <summary>A YAML double-quoted scalar.</summary>
    public static string YamlQuoted(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
