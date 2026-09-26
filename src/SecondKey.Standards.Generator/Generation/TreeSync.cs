using System.Text;

namespace SecondKey.Standards.Generator.Generation;

public enum DifferenceKind
{
    /// <summary>Generated, but not on disk.</summary>
    Missing,

    /// <summary>On disk with different contents.</summary>
    Stale,

    /// <summary>On disk under <c>generated/</c>, but no longer generated.</summary>
    Orphaned,
}

public sealed record TreeDifference(DifferenceKind Kind, string Path);

/// <summary>
/// Compares the generated tree with <c>generated/</c> on disk, or makes the disk match it. The
/// generator owns the whole directory, so a file it no longer produces is removed rather than left
/// to mislead — except build output (<c>bin/</c>, <c>obj/</c>) from packing the package project,
/// which is git-ignored and not the generator's.
/// </summary>
public static class TreeSync
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static IReadOnlyList<TreeDifference> Compare(RepositoryLayout layout, GeneratedTree tree)
    {
        var differences = new List<TreeDifference>();
        foreach (var (path, contents) in tree.Files)
        {
            var full = layout.Combine(path);
            if (!File.Exists(full))
            {
                differences.Add(new TreeDifference(DifferenceKind.Missing, path));
            }
            else if (!File.ReadAllBytes(full).AsSpan().SequenceEqual(Utf8NoBom.GetBytes(contents)))
            {
                // Byte for byte: a CRLF checkout or a byte-order mark is a difference too.
                differences.Add(new TreeDifference(DifferenceKind.Stale, path));
            }
        }

        foreach (var path in ExistingFiles(layout).Where(path => !tree.Files.ContainsKey(path)))
        {
            differences.Add(new TreeDifference(DifferenceKind.Orphaned, path));
        }

        return differences;
    }

    /// <summary>Writes what differs and removes orphans. Returns the number of files written and removed.</summary>
    public static (int Written, int Removed) Write(RepositoryLayout layout, GeneratedTree tree)
    {
        var differences = Compare(layout, tree);
        var written = 0;
        var removed = 0;
        foreach (var difference in differences)
        {
            var full = layout.Combine(difference.Path);
            if (difference.Kind == DifferenceKind.Orphaned)
            {
                File.Delete(full);
                removed++;
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, tree.Files[difference.Path], Utf8NoBom);
                written++;
            }
        }

        RemoveEmptyDirectories(layout.Combine(GeneratedTree.Directory));
        return (written, removed);
    }

    private static IEnumerable<string> ExistingFiles(RepositoryLayout layout)
    {
        var root = layout.Combine(GeneratedTree.Directory);
        if (!Directory.Exists(root))
        {
            return [];
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = 0, // .editorconfig and .globalconfig are generated too
            IgnoreInaccessible = false,
        };

        return Directory.EnumerateFiles(root, "*", options)
            .Select(layout.Relative)
            .Where(path => !IsBuildOutput(path))
            .Order(StringComparer.Ordinal);
    }

    private static bool IsBuildOutput(string path) =>
        path.Split('/').Any(segment => segment is "bin" or "obj");

    private static void RemoveEmptyDirectories(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(child);
            if (name is "bin" or "obj")
            {
                continue;
            }

            RemoveEmptyDirectories(child);
            if (!Directory.EnumerateFileSystemEntries(child).Any())
            {
                Directory.Delete(child);
            }
        }
    }
}
