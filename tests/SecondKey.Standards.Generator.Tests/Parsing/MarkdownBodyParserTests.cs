using SecondKey.Standards.Generator.Parsing;
using Xunit;

namespace SecondKey.Standards.Generator.Tests.Parsing;

public class MarkdownBodyParserTests
{
    [Fact]
    public void Headings_inside_code_fences_are_content_not_sections()
    {
        const string body = """
            Statement.

            ## Compliant

            ```bash
            ## not a heading
            # not an h1 either
            ```
            """;

        var parsed = MarkdownBodyParser.Parse(body, firstLine: 10);

        Assert.Empty(parsed.Issues);
        var section = Assert.Single(parsed.Sections);
        Assert.Equal("Compliant", section.Heading);
        Assert.Equal(1, section.CodeBlocks);
        Assert.Contains("## not a heading", section.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Tilde_fences_and_longer_backtick_fences_are_recognised()
    {
        const string body = """
            Statement.

            ## Compliant

            ~~~xml
            <a/>
            ~~~

            ````markdown
            ```csharp
            nested();
            ```
            ````
            """;

        var parsed = MarkdownBodyParser.Parse(body, firstLine: 1);

        Assert.Empty(parsed.Issues);
        Assert.Equal(2, Assert.Single(parsed.Sections).CodeBlocks);
    }

    [Fact]
    public void An_unclosed_fence_is_reported_at_the_line_that_opened_it()
    {
        const string body = """
            Statement.

            ## Compliant

            ```csharp
            open();
            """;

        var parsed = MarkdownBodyParser.Parse(body, firstLine: 20);

        var issue = Assert.Single(parsed.Issues);
        Assert.Equal(24, issue.Line);
        Assert.Contains("never closed", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Section_lines_are_reported_as_file_lines()
    {
        const string body = """

            Statement.

            ## Rationale

            Because.
            """;

        var parsed = MarkdownBodyParser.Parse(body, firstLine: 9);

        Assert.Equal(10, parsed.StatementLine);
        Assert.Equal(12, Assert.Single(parsed.Sections).Line);
    }

    [Fact]
    public void Level_three_headings_stay_inside_their_section()
    {
        const string body = """
            Statement.

            ## Migration

            ### Step one

            Do it.
            """;

        var parsed = MarkdownBodyParser.Parse(body, firstLine: 1);

        Assert.Empty(parsed.Issues);
        Assert.Contains("### Step one", Assert.Single(parsed.Sections).Content, StringComparison.Ordinal);
    }
}
