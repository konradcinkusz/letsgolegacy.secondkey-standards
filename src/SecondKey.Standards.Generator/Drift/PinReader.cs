using System.Xml;
using System.Xml.Linq;
using SecondKey.Standards.Generator.Model;
using SecondKey.Standards.Generator.Parsing;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace SecondKey.Standards.Generator.Drift;

/// <summary>What a consuming repository pins, and anything that made a pin unreadable.</summary>
public sealed record PinScan(IReadOnlyList<Pin> Pins, IReadOnlyList<string> Problems);

/// <summary>
/// Finds the standards version a consuming repository pins, in the two places a consumer already
/// states it — no separate lock file to keep in step:
/// <list type="bullet">
/// <item>the <c>SecondKey.Standards</c> package reference (<c>PackageVersion</c>,
/// <c>GlobalPackageReference</c> or <c>PackageReference</c> in any MSBuild file, or
/// <c>packages.config</c>) — the version of the gate's configuration;</item>
/// <item>the installed skill's <c>metadata.version</c> — the version of the agent's instructions.</item>
/// </list>
/// </summary>
public static class PinReader
{
    /// <summary>Where GitHub Copilot and GitHub Copilot upgrade look for repository skills.</summary>
    public static IReadOnlyList<string> SkillRoots { get; } = [".github/skills", ".github/upgrades/skills", ".claude/skills", ".agents/skills"];

    private static readonly HashSet<string> MsBuildExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csproj", ".vbproj", ".fsproj", ".props", ".targets",
    };

    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", "bin", "obj", "node_modules", "packages", "artifacts", "TestResults",
    };

    private static readonly HashSet<string> ReferenceElements = new(StringComparer.Ordinal)
    {
        "PackageReference", "PackageVersion", "GlobalPackageReference",
    };

    public static PinScan Scan(string repositoryRoot, string packageId, string skillName)
    {
        var pins = new List<Pin>();
        var problems = new List<string>();

        foreach (var file in Files(repositoryRoot))
        {
            var name = Path.GetFileName(file);
            if (string.Equals(name, "packages.config", StringComparison.OrdinalIgnoreCase))
            {
                ReadPackagesConfig(repositoryRoot, file, packageId, pins, problems);
            }
            else if (MsBuildExtensions.Contains(Path.GetExtension(file)))
            {
                ReadMsBuildFile(repositoryRoot, file, packageId, pins, problems);
            }
        }

        foreach (var root in SkillRoots)
        {
            var skill = Path.Combine(repositoryRoot, root.Replace('/', Path.DirectorySeparatorChar), skillName, "SKILL.md");
            if (File.Exists(skill))
            {
                ReadSkill(repositoryRoot, skill, pins, problems);
            }
        }

        return new PinScan(pins, problems);
    }

    private static void ReadMsBuildFile(string root, string file, string packageId, List<Pin> pins, List<string> problems)
    {
        var document = Load(file);
        if (document is null)
        {
            return; // Not well-formed XML: not a file the standards package could be referenced from.
        }

        foreach (var element in document.Descendants().Where(e => ReferenceElements.Contains(e.Name.LocalName)))
        {
            var id = (string?)element.Attribute("Include") ?? (string?)element.Attribute("Update");
            if (!string.Equals(id, packageId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var version = (string?)element.Attribute("Version")
                ?? (string?)element.Attribute("VersionOverride")
                ?? element.Elements().FirstOrDefault(child => child.Name.LocalName is "Version" or "VersionOverride")?.Value;

            // Under central package management a PackageReference carries no version; the
            // PackageVersion that does is found on its own.
            if (version is not null)
            {
                AddVersion(root, file, PinKind.Package, version, pins, problems);
            }
        }
    }

    private static void ReadPackagesConfig(string root, string file, string packageId, List<Pin> pins, List<string> problems)
    {
        var document = Load(file);
        foreach (var package in document?.Descendants("package") ?? [])
        {
            if (string.Equals((string?)package.Attribute("id"), packageId, StringComparison.OrdinalIgnoreCase)
                && (string?)package.Attribute("version") is { } version)
            {
                AddVersion(root, file, PinKind.Package, version, pins, problems);
            }
        }
    }

    private static void ReadSkill(string root, string file, List<Pin> pins, List<string> problems)
    {
        var relative = Relative(root, file);
        var split = FrontMatter.Split(SourceText.Normalise(File.ReadAllText(file)), out _, out _);
        string? version = null;
        if (split is not null)
        {
            try
            {
                var stream = new YamlStream();
                stream.Load(new StringReader(split.Yaml));
                if (stream.Documents.Count > 0
                    && stream.Documents[0].RootNode is YamlMappingNode front
                    && front.Children.TryGetValue(new YamlScalarNode("metadata"), out var metadata)
                    && metadata is YamlMappingNode map
                    && map.Children.TryGetValue(new YamlScalarNode("version"), out var node)
                    && node is YamlScalarNode scalar)
                {
                    version = scalar.Value;
                }
            }
            catch (YamlException)
            {
                // Reported below as a skill without a readable version.
            }
        }

        if (version is null)
        {
            problems.Add($"{relative} has no readable metadata.version; copy the skill again from a release instead of editing it");
            return;
        }

        AddVersion(root, file, PinKind.Skill, version, pins, problems);
    }

    private static void AddVersion(string root, string file, PinKind kind, string text, List<Pin> pins, List<string> problems)
    {
        var relative = Relative(root, file);
        var trimmed = text.Trim();

        // NuGet's exact-version notation, [1.2.3], is a pin; any other range or float is not.
        if (trimmed.Length > 2 && trimmed[0] == '[' && trimmed[^1] == ']' && !trimmed.Contains(',', StringComparison.Ordinal))
        {
            trimmed = trimmed[1..^1].Trim();
        }

        if (trimmed.Contains("$(", StringComparison.Ordinal))
        {
            problems.Add($"{relative} sets the version through an MSBuild property ({trimmed}); pass the version with --pinned-version");
        }
        else if (!SemanticVersion.TryParse(trimmed, out var version))
        {
            problems.Add($"{relative} pins \"{trimmed}\", which is not an exact MAJOR.MINOR.PATCH version");
        }
        else
        {
            pins.Add(new Pin(kind, relative, version));
        }
    }

    private static XDocument? Load(string file)
    {
        try
        {
            // The default reader settings prohibit DTDs, so a hostile file cannot expand entities.
            using var reader = XmlReader.Create(file, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            return XDocument.Load(reader);
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IEnumerable<string> Files(string directory)
    {
        var pending = new Stack<string>();
        pending.Push(directory);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var file in Directory.EnumerateFiles(current).Order(StringComparer.Ordinal))
            {
                yield return file;
            }

            foreach (var child in Directory.EnumerateDirectories(current).Order(StringComparer.Ordinal))
            {
                if (!SkippedDirectories.Contains(Path.GetFileName(child)))
                {
                    pending.Push(child);
                }
            }
        }
    }

    private static string Relative(string root, string file) =>
        Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
}
