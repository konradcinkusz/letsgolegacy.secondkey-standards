using System.Text.Json;

namespace SecondKey.Standards.Generator.Tests.Support;

/// <summary>
/// A throwaway standards repository on disk: a <c>catalog/pack.json</c>, a skill template, a
/// changelog, and whatever rule files a test writes. Deleted on dispose.
/// </summary>
internal sealed class TestRepository : IDisposable
{
    public const string PortcullisRule = "PORTCULLIS_MIG_EXAMPLE";
    public const string SkillName = "applying-test-standards";
    public const string PackageId = "Test.Standards";

    public TestRepository(string version = "1.0.0", string changelogStatus = "Unreleased")
    {
        Root = Path.Combine(Path.GetTempPath(), "secondkey-standards-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(Root, "catalog"));
        Directory.CreateDirectory(Path.Combine(Root, "standards"));
        WritePack(version: version);
        WriteFile("catalog/skill.template.md", "# Test skill\n\nVersion {{version}}, {{rule-count}} rules.\n\n{{rule-table}}\n\n{{gate-table}}\n");
        WriteChangelog($"## [{version}] — {changelogStatus}\n\n- Test.\n");
        Layout = new RepositoryLayout(Root);
    }

    public string Root { get; }

    public RepositoryLayout Layout { get; }

    public static object Pack(string version = "1.0.0") => new Dictionary<string, object>
    {
        ["name"] = "test-pack",
        ["version"] = version,
        ["repository"] = "https://example.com/test-standards",
        ["constitution"] = "https://example.com/constitution.md",
        ["skill"] = new
        {
            name = SkillName,
            description = "Applies the test standards. Use in tests.",
            discovery = "preload",
            traits = ".NET|CSharp",
        },
        ["package"] = new { id = PackageId, description = "Test analyzer configuration." },
        ["categories"] = new[] { "globalization", "hosting" },
        ["portcullisRules"] = new[] { PortcullisRule },
    };

    public void WritePack(object? pack = null, string version = "1.0.0") =>
        WriteFile("catalog/pack.json", JsonSerializer.Serialize(pack ?? Pack(version), new JsonSerializerOptions { WriteIndented = true }));

    public void WriteChangelog(string entries) => WriteFile("CHANGELOG.md", "# Changelog\n\n" + entries);

    public string WriteRule(string fileName, string contents) => WriteFile($"standards/{fileName}", contents);

    public string WriteFile(string relativePath, string contents)
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    public string ReadFile(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    public bool Exists(string relativePath) =>
        File.Exists(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
