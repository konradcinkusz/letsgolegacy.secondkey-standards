using SecondKey.Standards.Generator.Diagnostics;
using SecondKey.Standards.Generator.Model;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace SecondKey.Standards.Generator.Parsing;

/// <summary>
/// Parses one rule file and checks everything that can be checked from the file alone: the
/// front-matter fields, their formats and vocabularies, the file name, and the body's required
/// sections. Cross-file checks (duplicate ids, a diagnostic mapped twice) live in
/// <see cref="StandardsLoader"/>.
/// </summary>
internal static class RuleParser
{
    public const string RationaleSection = "Rationale";
    public const string NonCompliantSection = "Non-compliant";
    public const string CompliantSection = "Compliant";
    public const string MigrationSection = "Migration";
    public const string FlagSection = "Flag instead of fixing";
    public const string ReferencesSection = "References";

    public static IReadOnlyList<string> RequiredKeys { get; } = ["id", "title", "severity", "category", "appliesTo"];

    public static IReadOnlyList<string> OptionalKeys { get; } = ["portcullisRule", "principle", "analyzers"];

    public static IReadOnlyList<string> RequiredSections { get; } = [RationaleSection, NonCompliantSection, CompliantSection];

    public static IReadOnlyList<string> OptionalSections { get; } = [MigrationSection, FlagSection, ReferencesSection];

    private const int MaxTitleLength = 120;

    public static (Rule? Rule, IReadOnlyList<Problem> Problems) Parse(string path, string rawText, PackConfig pack)
    {
        var problems = new List<Problem>();
        void Report(int? line, string message) => problems.Add(new Problem(path, line, message));

        var text = SourceText.Normalise(rawText);
        var split = FrontMatter.Split(text, out var splitError, out var splitErrorLine);
        if (split is null)
        {
            Report(splitErrorLine, splitError!);
            return (null, problems);
        }

        var fields = ReadFields(split, Report);
        if (fields is null)
        {
            return (null, problems);
        }

        var id = fields.Scalar("id", required: true);
        var title = fields.Scalar("title", required: true);
        var severityName = fields.Scalar("severity", required: true);
        var category = fields.Scalar("category", required: true);
        var appliesTo = fields.List("appliesTo", required: true);
        var portcullisRule = fields.Scalar("portcullisRule", required: false);
        var principle = fields.Scalar("principle", required: false);
        var analyzers = fields.List("analyzers", required: false) ?? [];

        if (id is not null)
        {
            if (!Patterns.RuleId().IsMatch(id))
            {
                Report(fields.LineOf("id"), $"id \"{id}\" must be upper-case segments ending in a three-digit number, such as SK-MIG-001");
            }
            else
            {
                CheckFileName(path, id, Report);
            }
        }

        if (title is not null && title.Length > MaxTitleLength)
        {
            Report(fields.LineOf("title"), $"title is {title.Length} characters; keep it to {MaxTitleLength} so it fits a table row");
        }

        var severity = default(Severity);
        if (severityName is not null && !SeverityNames.TryParse(severityName, out severity))
        {
            Report(fields.LineOf("severity"), $"severity \"{severityName}\" must be one of: {string.Join(", ", SeverityNames.All)}");
            severityName = null;
        }

        if (category is not null && !pack.Categories.Contains(category, StringComparer.Ordinal))
        {
            Report(fields.LineOf("category"), $"category \"{category}\" is not one of the categories in catalog/pack.json: {string.Join(", ", pack.Categories)}");
        }

        if (appliesTo is not null)
        {
            if (appliesTo.Count == 0)
            {
                Report(fields.LineOf("appliesTo"), "appliesTo must list at least one file pattern, such as \"*.cs\"");
            }

            foreach (var pattern in appliesTo)
            {
                if (!IsFilePattern(pattern))
                {
                    Report(fields.LineOf("appliesTo"), $"appliesTo entry \"{pattern}\" is not an .editorconfig file pattern (no spaces, no leading /, no [ ] # or ;)");
                }
            }
        }

        if (portcullisRule is not null)
        {
            if (!Patterns.PortcullisRuleId().IsMatch(portcullisRule))
            {
                Report(fields.LineOf("portcullisRule"), $"portcullisRule \"{portcullisRule}\" must look like PORTCULLIS_<SLUG>");
            }
            else if (!pack.PortcullisRules.Contains(portcullisRule, StringComparer.Ordinal))
            {
                Report(fields.LineOf("portcullisRule"), $"portcullisRule \"{portcullisRule}\" is not a known Portcullis diagnostic; add it to \"portcullisRules\" in catalog/pack.json only once the gate ships it");
            }
        }

        if (principle is not null && !Patterns.Principle().IsMatch(principle))
        {
            Report(fields.LineOf("principle"), $"principle \"{principle}\" must be a principle of the architecture constitution, P1 to P15 (for example P5)");
        }

        foreach (var analyzer in analyzers)
        {
            if (!Patterns.SdkAnalyzerId().IsMatch(analyzer))
            {
                Report(fields.LineOf("analyzers"), $"analyzers entry \"{analyzer}\" must be a .NET SDK analyzer id such as CA1310 (Portcullis ids go in portcullisRule)");
            }
        }

        if (analyzers.Distinct(StringComparer.Ordinal).Count() != analyzers.Count)
        {
            Report(fields.LineOf("analyzers"), "analyzers lists the same id twice");
        }

        var body = MarkdownBodyParser.Parse(split.Body, split.BodyFirstLine);
        foreach (var (line, message) in body.Issues)
        {
            Report(line, message);
        }

        CheckSections(body, split.BodyFirstLine, Report);

        if (problems.Count > 0 || id is null || title is null || severityName is null || category is null || appliesTo is null)
        {
            return (null, problems);
        }

        var rule = new Rule(
            id,
            title,
            severity,
            category,
            appliesTo,
            portcullisRule,
            principle,
            analyzers,
            body.Statement,
            body.Sections,
            path,
            split.Body.Trim());
        return (rule, problems);
    }

    private static void CheckSections(MarkdownBody body, int bodyFirstLine, Action<int?, string> report)
    {
        if (body.Statement.Length == 0)
        {
            report(bodyFirstLine, "the body must open with the rule's statement (what migrated code must or must not do) before the first ## section");
        }

        var allowed = RequiredSections.Concat(OptionalSections).ToArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in body.Sections)
        {
            if (!allowed.Contains(section.Heading, StringComparer.Ordinal))
            {
                report(section.Line, $"section \"## {section.Heading}\" is not one of: {string.Join(", ", allowed.Select(a => "## " + a))}");
            }
            else if (!seen.Add(section.Heading))
            {
                report(section.Line, $"section \"## {section.Heading}\" appears more than once");
            }
        }

        foreach (var required in RequiredSections)
        {
            var section = body.Sections.FirstOrDefault(s => string.Equals(s.Heading, required, StringComparison.Ordinal));
            if (section is null)
            {
                report(null, $"the \"## {required}\" section is missing; every rule needs a rationale, a non-compliant and a compliant example");
                continue;
            }

            if (string.Equals(required, RationaleSection, StringComparison.Ordinal))
            {
                if (section.Content.Length == 0)
                {
                    report(section.Line, "the \"## Rationale\" section is empty; say why the rule exists");
                }
            }
            else if (section.CodeBlocks == 0)
            {
                report(section.Line, $"the \"## {required}\" section has no fenced code example");
            }
        }
    }

    private static void CheckFileName(string path, string id, Action<int?, string> report)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (string.Equals(name, id, StringComparison.Ordinal))
        {
            return;
        }

        var prefix = id + "-";
        if (name.StartsWith(prefix, StringComparison.Ordinal) && Patterns.FileSlug().IsMatch(name[prefix.Length..]))
        {
            return;
        }

        report(null, $"the file name must be \"{id}.md\" or \"{id}-<lower-case-slug>.md\" so the rule can be found by its id");
    }

    private static bool IsFilePattern(string pattern) =>
        pattern.Length > 0
        && !pattern.StartsWith('/')
        && !pattern.Any(char.IsWhiteSpace)
        && pattern.IndexOfAny(['[', ']', '#', ';']) < 0;

    private static Fields? ReadFields(FrontMatterSplit split, Action<int?, string> report)
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(split.Yaml));
        }
        catch (YamlException ex)
        {
            var hint = split.Yaml.Contains("- *", StringComparison.Ordinal)
                ? " (quote file patterns: - \"*.cs\" — an unquoted * starts a YAML alias)"
                : "";
            report(split.YamlFirstLine + (int)ex.Start.Line - 1, $"the front matter is not valid YAML: {ex.Message}{hint}");
            return null;
        }

        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            report(split.YamlFirstLine, "the front matter must be a set of \"key: value\" lines");
            return null;
        }

        var allowed = RequiredKeys.Concat(OptionalKeys).ToArray();
        var entries = new Dictionary<string, (YamlNode Node, int Line)>(StringComparer.Ordinal);
        foreach (var (keyNode, valueNode) in root.Children)
        {
            var line = split.YamlFirstLine + (int)keyNode.Start.Line - 1;
            if (keyNode is not YamlScalarNode { Value: { } key })
            {
                report(line, "front-matter keys must be plain names");
                continue;
            }

            if (!allowed.Contains(key, StringComparer.Ordinal))
            {
                report(line, $"unknown front-matter key \"{key}\"; allowed keys are {string.Join(", ", allowed)}");
                continue;
            }

            entries[key] = (valueNode, line);
        }

        return new Fields(entries, split.YamlFirstLine, report);
    }

    private sealed class Fields(
        Dictionary<string, (YamlNode Node, int Line)> entries,
        int frontMatterLine,
        Action<int?, string> report)
    {
        public int LineOf(string key) => entries.TryGetValue(key, out var entry) ? entry.Line : frontMatterLine;

        public string? Scalar(string key, bool required)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                if (required)
                {
                    report(frontMatterLine, $"required front-matter key \"{key}\" is missing");
                }

                return null;
            }

            if (entry.Node is not YamlScalarNode scalar)
            {
                report(entry.Line, $"\"{key}\" must be a single value, not a list or a mapping");
                return null;
            }

            var value = scalar.Value?.Trim() ?? "";
            if (value.Length == 0)
            {
                report(entry.Line, required
                    ? $"required front-matter key \"{key}\" is empty"
                    : $"\"{key}\" is empty; give it a value or remove the key");
                return null;
            }

            if (value.Contains('\n', StringComparison.Ordinal))
            {
                report(entry.Line, $"\"{key}\" must fit on one line");
                return null;
            }

            return value;
        }

        public IReadOnlyList<string>? List(string key, bool required)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                if (required)
                {
                    report(frontMatterLine, $"required front-matter key \"{key}\" is missing");
                }

                return null;
            }

            if (entry.Node is not YamlSequenceNode sequence)
            {
                report(entry.Line, $"\"{key}\" must be a list, one \"- item\" per line");
                return null;
            }

            var items = new List<string>();
            foreach (var item in sequence.Children)
            {
                if (item is not YamlScalarNode { Value: { } value } || value.Trim().Length == 0)
                {
                    report(entry.Line, $"every \"{key}\" entry must be a non-empty value");
                    return null;
                }

                items.Add(value.Trim());
            }

            return items;
        }
    }
}
