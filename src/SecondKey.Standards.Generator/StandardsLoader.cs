using SecondKey.Standards.Generator.Diagnostics;
using SecondKey.Standards.Generator.Model;
using SecondKey.Standards.Generator.Parsing;

namespace SecondKey.Standards.Generator;

/// <summary>Every rule in a repository, in id order, and every problem found reading them.</summary>
public sealed record StandardsSet(IReadOnlyList<Rule> Rules, IReadOnlyList<Problem> Problems)
{
    public bool IsValid => Problems.Count == 0;
}

/// <summary>
/// Reads <c>standards/*.md</c> (one rule per file; <c>README.md</c> is documentation, not a rule),
/// parses each file, and adds the checks that need the whole set: ids are unique, and no
/// diagnostic is mapped by two rules — otherwise the generated analyzer configuration would have
/// to pick one of two severities for the same id, and the agent and the gate could disagree.
/// </summary>
public static class StandardsLoader
{
    public const string ReadmeFileName = "README.md";

    public static StandardsSet Load(RepositoryLayout layout, PackConfig pack)
    {
        var problems = new List<Problem>();
        var rules = new List<Rule>();
        var standardsPath = layout.Relative(layout.StandardsDirectory);

        if (!Directory.Exists(layout.StandardsDirectory))
        {
            problems.Add(new Problem(standardsPath, null, "the standards directory does not exist"));
            return new StandardsSet(rules, problems);
        }

        var files = Directory.EnumerateFiles(layout.StandardsDirectory, "*.md", SearchOption.TopDirectoryOnly)
            .Where(file => !string.Equals(Path.GetFileName(file), ReadmeFileName, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
        {
            problems.Add(new Problem(standardsPath, null, "no rule files found; each rule is one standards/<id>-<slug>.md file"));
            return new StandardsSet(rules, problems);
        }

        foreach (var file in files)
        {
            var (rule, fileProblems) = RuleParser.Parse(layout.Relative(file), File.ReadAllText(file), pack);
            problems.AddRange(fileProblems);
            if (rule is not null)
            {
                rules.Add(rule);
            }
        }

        foreach (var duplicate in rules.GroupBy(rule => rule.Id, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            var first = duplicate.First();
            foreach (var other in duplicate.Skip(1))
            {
                problems.Add(new Problem(other.SourcePath, null, $"id {other.Id} is already used by {first.SourcePath}"));
            }
        }

        var owners = new Dictionary<string, Rule>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            foreach (var diagnostic in rule.Diagnostics)
            {
                if (owners.TryGetValue(diagnostic, out var owner))
                {
                    problems.Add(new Problem(rule.SourcePath, null, $"{diagnostic} is already mapped by {owner.Id} ({owner.SourcePath}); a diagnostic can have only one severity"));
                }
                else
                {
                    owners[diagnostic] = rule;
                }
            }
        }

        rules.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
        return new StandardsSet(rules, problems);
    }
}
