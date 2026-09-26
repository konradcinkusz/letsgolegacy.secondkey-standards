namespace SecondKey.Standards.Generator.Tests.Support;

/// <summary>
/// Builds rule files for tests: a complete, valid rule by default, with any front-matter line or
/// body replaced. Tests change exactly the part they are about.
/// </summary>
internal static class RuleText
{
    public const string ValidBody = """

        Every culture-sensitive comparison states its StringComparison.

        ## Rationale

        NLS and ICU disagree.

        ## Non-compliant

        ```csharp
        sku.StartsWith("GIFT-");
        ```

        ## Compliant

        ```csharp
        sku.StartsWith("GIFT-", StringComparison.Ordinal);
        ```
        """;

    public static string Valid(
        string id = "SK-MIG-001",
        string? frontMatter = null,
        string? body = null)
    {
        frontMatter ??= $"""
            id: {id}
            title: State the comparison
            severity: warning
            category: globalization
            appliesTo:
              - "*.cs"
            """;
        return $"---\n{frontMatter}\n---\n{body ?? ValidBody}\n";
    }

    /// <summary>The valid front matter with one key's line replaced, or removed when <paramref name="line"/> is null.</summary>
    public static string WithFrontMatterLine(string key, string? line)
    {
        var lines = new List<string>
        {
            "id: SK-MIG-001",
            "title: State the comparison",
            "severity: warning",
            "category: globalization",
            "appliesTo:",
            "  - \"*.cs\"",
        };

        var index = lines.FindIndex(l => l.StartsWith(key + ":", StringComparison.Ordinal));
        if (index < 0)
        {
            if (line is not null)
            {
                lines.Add(line);
            }
        }
        else if (line is null)
        {
            lines.RemoveAt(index);
            if (string.Equals(key, "appliesTo", StringComparison.Ordinal))
            {
                lines.RemoveAt(index);
            }
        }
        else
        {
            lines[index] = line;
        }

        return Valid(frontMatter: string.Join('\n', lines));
    }
}
