using SecondKey.Standards.Generator.Generation;
using SecondKey.Standards.Generator.Tests.Support;
using Xunit;

namespace SecondKey.Standards.Generator.Tests.Generation;

public class TreeSyncTests
{
    private static GeneratedTree Tree(params (string Path, string Contents)[] files)
    {
        var tree = new GeneratedTree();
        foreach (var (path, contents) in files)
        {
            tree.Add(path, contents);
        }

        return tree;
    }

    [Fact]
    public void Missing_stale_and_orphaned_files_are_each_reported()
    {
        using var repository = new TestRepository();
        repository.WriteFile("generated/a.txt", "old\n");
        repository.WriteFile("generated/orphan.txt", "left behind\n");
        var tree = Tree(("generated/a.txt", "new"), ("generated/b.txt", "added"));

        var differences = TreeSync.Compare(repository.Layout, tree);

        Assert.Equal(
            [
                new TreeDifference(DifferenceKind.Stale, "generated/a.txt"),
                new TreeDifference(DifferenceKind.Missing, "generated/b.txt"),
                new TreeDifference(DifferenceKind.Orphaned, "generated/orphan.txt"),
            ],
            differences);
    }

    [Fact]
    public void Writing_makes_the_disk_match_and_removes_orphans_and_their_empty_directories()
    {
        using var repository = new TestRepository();
        repository.WriteFile("generated/gone/orphan.txt", "left behind\n");
        var tree = Tree(("generated/config/.editorconfig", "root = false"));

        var (written, removed) = TreeSync.Write(repository.Layout, tree);

        Assert.Equal((1, 1), (written, removed));
        Assert.Empty(TreeSync.Compare(repository.Layout, tree));
        Assert.Equal("root = false\n", repository.ReadFile("generated/config/.editorconfig"));
        Assert.False(Directory.Exists(Path.Combine(repository.Root, "generated", "gone")));
    }

    [Fact]
    public void Build_output_from_packing_the_package_is_not_an_orphan()
    {
        using var repository = new TestRepository();
        repository.WriteFile("generated/nuget/bin/Release/Test.Standards.dll", "binary");
        repository.WriteFile("generated/nuget/obj/project.assets.json", "{}");
        var tree = Tree(("generated/nuget/Test.Standards.csproj", "<Project />"));
        TreeSync.Write(repository.Layout, tree);

        Assert.Empty(TreeSync.Compare(repository.Layout, tree));
        Assert.True(repository.Exists("generated/nuget/bin/Release/Test.Standards.dll"));
    }

    [Fact]
    public void A_crlf_copy_of_a_generated_file_is_stale()
    {
        using var repository = new TestRepository();
        repository.WriteFile("generated/a.txt", "line one\r\nline two\r\n");

        var differences = TreeSync.Compare(repository.Layout, Tree(("generated/a.txt", "line one\nline two")));

        Assert.Equal(DifferenceKind.Stale, Assert.Single(differences).Kind);
    }

    [Fact]
    public void A_copy_with_a_byte_order_mark_is_stale()
    {
        using var repository = new TestRepository();
        Directory.CreateDirectory(Path.Combine(repository.Root, "generated"));
        File.WriteAllText(Path.Combine(repository.Root, "generated", "a.txt"), "same\n", new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var differences = TreeSync.Compare(repository.Layout, Tree(("generated/a.txt", "same")));

        Assert.Equal(DifferenceKind.Stale, Assert.Single(differences).Kind);
    }

    [Fact]
    public void Only_paths_under_generated_can_be_emitted()
    {
        Assert.Throws<ArgumentException>(() => Tree(("standards/SK-MIG-001.md", "overwritten")));
    }
}
