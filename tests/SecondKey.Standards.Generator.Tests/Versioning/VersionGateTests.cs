using SecondKey.Standards.Generator.Generation;
using SecondKey.Standards.Generator.Model;
using SecondKey.Standards.Generator.Tests.Support;
using Xunit;

namespace SecondKey.Standards.Generator.Tests.Versioning;

/// <summary>
/// The version gate, end to end: generate once (the committed state), change the sources, and
/// generate again.
/// </summary>
public class VersionGateTests
{
    private static GenerationResult Generate(TestRepository repository, bool write = true)
    {
        var result = PackGenerator.Build(repository.Layout, PackConfig.Load(repository.Layout.PackConfigPath));
        if (write && result.Succeeded)
        {
            TreeSync.Write(repository.Layout, result.Tree!);
        }

        return result;
    }

    private static void EditRule(TestRepository repository) =>
        repository.WriteRule("SK-MIG-001.md", RuleText.Valid().Replace("NLS and ICU disagree.", "NLS and ICU disagree on sorting.", StringComparison.Ordinal));

    [Fact]
    public void Changing_the_content_of_a_released_version_is_refused()
    {
        using var repository = new TestRepository(version: "1.0.0", changelogStatus: "2026-09-01");
        repository.WriteRule("SK-MIG-001.md", RuleText.Valid());
        Assert.True(Generate(repository).Succeeded);

        EditRule(repository);
        var result = Generate(repository);

        Assert.False(result.Succeeded);
        var problem = Assert.Single(result.Problems);
        Assert.Equal("catalog/pack.json", problem.Path);
        Assert.Contains("version 1.0.0 is already released", problem.Message, StringComparison.Ordinal);
        Assert.Null(result.Tree);
    }

    [Fact]
    public void A_refused_change_writes_nothing()
    {
        using var repository = new TestRepository(version: "1.0.0", changelogStatus: "2026-09-01");
        repository.WriteRule("SK-MIG-001.md", RuleText.Valid());
        Generate(repository);
        var before = repository.ReadFile(GeneratedPaths.Manifest);

        EditRule(repository);
        Generate(repository);

        Assert.Equal(before, repository.ReadFile(GeneratedPaths.Manifest));
    }

    [Fact]
    public void Changing_the_content_of_an_unreleased_version_is_allowed()
    {
        using var repository = new TestRepository(version: "1.0.0", changelogStatus: "Unreleased");
        repository.WriteRule("SK-MIG-001.md", RuleText.Valid());
        Generate(repository);

        EditRule(repository);

        Assert.True(Generate(repository).Succeeded);
    }

    [Fact]
    public void Bumping_the_version_accepts_the_new_content()
    {
        using var repository = new TestRepository(version: "1.0.0", changelogStatus: "2026-09-01");
        repository.WriteRule("SK-MIG-001.md", RuleText.Valid());
        Generate(repository);

        EditRule(repository);
        repository.WritePack(version: "1.1.0");
        repository.WriteChangelog("## [1.1.0] — Unreleased\n\n- Reworded.\n\n## [1.0.0] — 2026-09-01\n\n- First.\n");
        var result = Generate(repository);

        Assert.True(result.Succeeded, string.Join('\n', result.Problems));
        Assert.Equal("1.1.0", Manifest.TryParse(repository.ReadFile(GeneratedPaths.Manifest))!.Version);
    }

    [Fact]
    public void A_version_without_a_changelog_entry_is_refused()
    {
        using var repository = new TestRepository(version: "1.0.0");
        repository.WriteRule("SK-MIG-001.md", RuleText.Valid());
        repository.WriteChangelog("## [0.9.0] — 2026-08-01\n\n- Older.\n");

        var result = Generate(repository);

        Assert.Contains(result.Problems, p => p.Path == "CHANGELOG.md" && p.Message.Contains("no \"## [1.0.0]\" entry", StringComparison.Ordinal));
    }

    [Fact]
    public void A_version_lower_than_the_committed_one_is_refused()
    {
        using var repository = new TestRepository(version: "1.2.0");
        repository.WriteRule("SK-MIG-001.md", RuleText.Valid());
        Generate(repository);

        repository.WritePack(version: "1.1.0");
        repository.WriteChangelog("## [1.1.0] — Unreleased\n\n- Back.\n");
        var result = Generate(repository);

        Assert.Contains(result.Problems, p => p.Message.Contains("versions only go up", StringComparison.Ordinal));
    }

    [Fact]
    public void A_bump_with_no_content_change_is_a_plain_re_release()
    {
        using var repository = new TestRepository(version: "1.0.0", changelogStatus: "2026-09-01");
        repository.WriteRule("SK-MIG-001.md", RuleText.Valid());
        var first = Generate(repository);

        repository.WritePack(version: "1.0.1");
        repository.WriteChangelog("## [1.0.1] — Unreleased\n\n- Re-release.\n\n## [1.0.0] — 2026-09-01\n\n- First.\n");
        var second = Generate(repository);

        Assert.True(second.Succeeded);
        Assert.Equal(first.Manifest!.ContentHash, second.Manifest!.ContentHash);
    }
}
