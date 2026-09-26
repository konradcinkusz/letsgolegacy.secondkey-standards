using System.Diagnostics;
using SecondKey.Standards.Generator.Generation;

namespace SecondKey.Standards.Generator.Tests.Support;

/// <summary>
/// A real git repository standing in for the standards repository: each <see cref="Release"/> commits a
/// changelog and a manifest and tags it <c>v&lt;version&gt;</c>, so the drift check runs its real
/// <c>git ls-remote</c> and <c>git fetch</c> against it, offline.
/// </summary>
internal sealed class StandardsSource : IDisposable
{
    public StandardsSource()
    {
        Path = Directory.CreateTempSubdirectory("secondkey-source-").FullName;
        Git("init", "--quiet", "--initial-branch=main");
    }

    public string Path { get; }

    public void Release(string version, string changelog, string manifestJson)
    {
        Commit(changelog, manifestJson, $"Release {version}");
        Tag($"v{version}");
    }

    public void Commit(string changelog, string? manifestJson, string message)
    {
        File.WriteAllText(System.IO.Path.Combine(Path, "CHANGELOG.md"), changelog);
        if (manifestJson is not null)
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "generated"));
            File.WriteAllText(System.IO.Path.Combine(Path, "generated", "manifest.json"), manifestJson);
        }

        Git("add", "--all");
        Git("commit", "--quiet", "--allow-empty", "-m", message);
    }

    public void Tag(string name) => Git("tag", name);

    /// <summary>A manifest with the given rules, each "id:severity" or "id:severity:text-version".</summary>
    public static string Manifest(string version, params string[] rules) =>
        new Manifest
        {
            Name = "secondkey-standards",
            Version = version,
            ContentHash = "sha256:" + version,
            Skill = "applying-dotnet-migration-standards",
            Package = "SecondKey.Standards",
            Rules = rules.Select(rule =>
            {
                var parts = rule.Split(':');
                return new ManifestRule
                {
                    Id = parts[0],
                    Title = $"Rule {parts[0]}",
                    Severity = parts[1],
                    Category = "behaviour",
                    Hash = "sha256:" + parts[0] + (parts.Length > 2 ? "-" + parts[2] : ""),
                };
            }).ToList(),
        }.ToJson();

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal); // git marks objects read-only
            }

            Directory.Delete(Path, recursive: true);
        }
    }

    private void Git(params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // Independent of whoever runs the tests: no signing, a fixed identity.
        foreach (var argument in new[] { "-c", "commit.gpgsign=false", "-c", "tag.gpgsign=false", "-c", "user.name=Test", "-c", "user.email=test@example.com" })
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEnd();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error}");
        }
    }
}
