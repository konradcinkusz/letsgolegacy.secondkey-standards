namespace SecondKey.Standards.Generator.Model;

/// <summary>
/// One migration standard, parsed from one file in <c>standards/</c>. The front matter becomes
/// the typed fields; the Markdown body is kept both whole (<see cref="Body"/>, emitted verbatim
/// into the skill's reference files) and split into its sections (for validation).
/// </summary>
public sealed record Rule(
    string Id,
    string Title,
    Severity Severity,
    string Category,
    IReadOnlyList<string> AppliesTo,
    string? PortcullisRule,
    string? Principle,
    IReadOnlyList<string> Analyzers,
    string Statement,
    IReadOnlyList<RuleSection> Sections,
    string SourcePath,
    string Body)
{
    /// <summary>
    /// Every diagnostic id the gate checks this rule with: the Portcullis rule, if any, then
    /// the .NET SDK analyzers. The generated analyzer configuration sets each one to
    /// <see cref="Severity"/>.
    /// </summary>
    public IReadOnlyList<string> Diagnostics =>
        PortcullisRule is null ? Analyzers : [PortcullisRule, .. Analyzers];

    public RuleSection? Section(string heading) =>
        Sections.FirstOrDefault(section => string.Equals(section.Heading, heading, StringComparison.Ordinal));
}

/// <summary>A level-two section of a rule's body.</summary>
/// <param name="Heading">The heading text, without the <c>##</c>.</param>
/// <param name="Content">Everything under the heading up to the next one, trimmed.</param>
/// <param name="Line">The 1-based line of the heading in the source file.</param>
/// <param name="CodeBlocks">Fenced code blocks in the section that contain at least one non-blank line.</param>
public sealed record RuleSection(string Heading, string Content, int Line, int CodeBlocks);
