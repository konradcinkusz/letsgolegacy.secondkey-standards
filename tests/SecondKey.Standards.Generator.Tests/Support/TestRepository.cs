using System.Text.Json;

namespace SecondKey.Standards.Generator.Tests.Support;

/// <summary>
/// A throwaway standards repository on disk: a <c>catalog/pack.json</c> and whatever rule files a
/// test writes. Deleted on dispose.
/// </summary>
internal sealed class TestRepository : IDisposable
{
    public const string PortcullisRule = "PORTCULLIS_MIG_EXAMPLE";

    public TestRepository()
    {
        Root = Path.Combine(Path.GetTempPath(), "secondkey-standards-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(Root, "catalog"));
        Directory.CreateDirectory(Path.Combine(Root, "standards"));
        WritePack();
        Layout = new RepositoryLayout(Root);
    }

    public string Root { get; }

    public RepositoryLayout Layout { get; }

    public void WritePack(object? pack = null)
    {
        pack ??= new
        {
            name = "test-pack",
            categories = new[] { "globalization", "hosting" },
            portcullisRules = new[] { PortcullisRule },
        };
        File.WriteAllText(
            Path.Combine(Root, "catalog", "pack.json"),
            JsonSerializer.Serialize(pack, new JsonSerializerOptions { WriteIndented = true }));
    }

    public string WriteRule(string fileName, string contents)
    {
        var path = Path.Combine(Root, "standards", fileName);
        File.WriteAllText(path, contents);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
