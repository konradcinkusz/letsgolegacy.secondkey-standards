namespace SecondKey.Standards.Generator;

/// <summary>
/// Where things live in a standards repository. Every path the tool reads or writes is derived
/// from <see cref="Root"/>, so the same code runs against this repository and against the
/// throwaway repositories the tests build.
/// </summary>
public sealed class RepositoryLayout
{
    public const string PackConfigRelativePath = "catalog/pack.json";
    public const string StandardsRelativePath = "standards";

    public RepositoryLayout(string root)
    {
        Root = Path.GetFullPath(root);
    }

    public string Root { get; }

    public string PackConfigPath => Combine(PackConfigRelativePath);

    public string StandardsDirectory => Combine(StandardsRelativePath);

    public string Combine(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>A repository-relative path with forward slashes, for messages and generated links.</summary>
    public string Relative(string fullPath) =>
        Path.GetRelativePath(Root, fullPath).Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>
    /// Uses <paramref name="explicitRoot"/> when given; otherwise walks up from
    /// <paramref name="startDirectory"/> to the first directory containing
    /// <c>catalog/pack.json</c>. Returns null when there is none.
    /// </summary>
    public static RepositoryLayout? Discover(string? explicitRoot, string startDirectory)
    {
        if (explicitRoot is not null)
        {
            return new RepositoryLayout(explicitRoot);
        }

        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = new RepositoryLayout(directory.FullName);
            if (File.Exists(candidate.PackConfigPath))
            {
                return candidate;
            }
        }

        return null;
    }
}
