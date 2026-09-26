using SecondKey.Standards.Generator.Diagnostics;
using SecondKey.Standards.Generator.Model;
using SecondKey.Standards.Generator.Parsing;
using SecondKey.Standards.Generator.Versioning;

namespace SecondKey.Standards.Generator.Generation;

/// <summary>What one generator run produced, or why it could not.</summary>
public sealed record GenerationResult(GeneratedTree? Tree, Manifest? Manifest, IReadOnlyList<Problem> Problems)
{
    public bool Succeeded => Problems.Count == 0 && Tree is not null;
}

/// <summary>
/// Reads every source — the rules, the pack metadata, the skill template, the changelog — builds the
/// generated tree in memory, and applies the version gate against the committed manifest. Nothing is
/// written here: <c>generate</c> and <c>generate --check</c> share this and differ only in what they
/// do with the result, so they cannot disagree about what the output should be.
/// </summary>
public static class PackGenerator
{
    public static GenerationResult Build(RepositoryLayout layout, PackConfig pack)
    {
        var problems = new List<Problem>();

        var standards = StandardsLoader.Load(layout, pack);
        problems.AddRange(standards.Problems);

        var template = ReadSource(layout, SkillTemplate.RelativePath, problems);
        if (template is not null)
        {
            template = SourceText.Normalise(template);
            foreach (var (line, token) in SkillTemplate.UnknownPlaceholders(template))
            {
                problems.Add(new Problem(SkillTemplate.RelativePath, line,
                    $"unknown placeholder {{{{{token}}}}}; known placeholders are {string.Join(", ", SkillTemplate.Placeholders.Select(p => "{{" + p + "}}"))}"));
            }

            foreach (var missing in SkillTemplate.MissingPlaceholders(template))
            {
                problems.Add(new Problem(SkillTemplate.RelativePath, null,
                    $"the skill template must include {{{{{missing}}}}}"));
            }
        }

        Changelog? changelog = null;
        var changelogText = ReadSource(layout, Changelog.RelativePath, problems);
        if (changelogText is not null)
        {
            changelog = Changelog.Parse(changelogText, Changelog.RelativePath, out var changelogProblems);
            problems.AddRange(changelogProblems);
        }

        if (problems.Count > 0 || template is null || changelog is null)
        {
            return new GenerationResult(null, null, problems);
        }

        var (tree, manifest) = TreeEmitter.Emit(pack, standards.Rules, template);

        var committedPath = layout.Combine(GeneratedPaths.Manifest);
        var committed = File.Exists(committedPath) ? Manifest.TryParse(File.ReadAllText(committedPath)) : null;
        problems.AddRange(VersionGate.Check(pack, committed, manifest, changelog));

        return new GenerationResult(problems.Count == 0 ? tree : null, manifest, problems);
    }

    private static string? ReadSource(RepositoryLayout layout, string relativePath, List<Problem> problems)
    {
        var path = layout.Combine(relativePath);
        if (File.Exists(path))
        {
            return File.ReadAllText(path);
        }

        problems.Add(new Problem(relativePath, null, "this source file is missing"));
        return null;
    }
}
