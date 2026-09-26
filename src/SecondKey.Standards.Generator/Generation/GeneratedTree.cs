namespace SecondKey.Standards.Generator.Generation;

/// <summary>
/// The generator's output, in memory: repository-relative path (forward slashes) → file contents.
/// Everything is built here first and only then compared with or written to disk, so
/// <c>--check</c> and a real run cannot disagree about what the output is.
/// </summary>
public sealed class GeneratedTree
{
    public const string Directory = "generated";

    private readonly SortedDictionary<string, string> files = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> Files => files;

    /// <summary>Adds a file. Contents are normalised to LF line endings and a single trailing newline.</summary>
    public void Add(string path, string contents)
    {
        if (!path.StartsWith(Directory + "/", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Generated files live under {Directory}/, not at {path}.", nameof(path));
        }

        if (!files.TryAdd(path, Normalise(contents)))
        {
            throw new InvalidOperationException($"{path} is generated twice.");
        }
    }

    private static string Normalise(string contents) =>
        contents.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n') + "\n";
}
