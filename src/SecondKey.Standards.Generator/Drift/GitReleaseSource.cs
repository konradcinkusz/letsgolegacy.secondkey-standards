using System.Diagnostics;
using System.Globalization;
using SecondKey.Standards.Generator.Model;

namespace SecondKey.Standards.Generator.Drift;

/// <summary>Where released standards versions, and the files they shipped, are read from.</summary>
public interface IReleaseSource
{
    /// <summary>How to name the source in messages.</summary>
    string Description { get; }

    /// <summary>Every released version, highest first. Throws <see cref="ReleaseSourceException"/> when the source cannot be read.</summary>
    Task<IReadOnlyList<SemanticVersion>> ListReleasesAsync(CancellationToken cancellationToken);

    /// <summary>A file as the release tagged <c>v&lt;version&gt;</c> shipped it, or null when there is no such tag or file.</summary>
    Task<string?> ReadFileAsync(SemanticVersion version, string path, CancellationToken cancellationToken);
}

public sealed class ReleaseSourceException(string message) : Exception(message);

/// <summary>
/// Releases are git tags <c>v&lt;MAJOR.MINOR.PATCH&gt;</c> of the standards repository — no
/// service, no registry: <c>git ls-remote</c> lists them, and a shallow fetch of one tag reads the
/// changelog and manifest it shipped. Works the same against the public repository, a mirror, or a
/// local path, which is how the tests and the CI fixtures run it offline.
/// </summary>
public sealed class GitReleaseSource : IReleaseSource, IDisposable
{
    private readonly string source;
    private readonly TimeSpan timeout;
    private readonly HashSet<string> fetched = new(StringComparer.Ordinal);
    private string? workspace;

    public GitReleaseSource(string source, TimeSpan? timeout = null)
    {
        this.source = source;
        this.timeout = timeout ?? TimeSpan.FromSeconds(60);
    }

    public string Description => source;

    public async Task<IReadOnlyList<SemanticVersion>> ListReleasesAsync(CancellationToken cancellationToken)
    {
        var (exitCode, output, error) = await GitAsync(Path.GetTempPath(), cancellationToken, "ls-remote", "--tags", "--refs", source);
        if (exitCode != 0)
        {
            throw new ReleaseSourceException($"could not list the tags of {source}: {FirstLine(error)}");
        }

        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\t'))
            .Where(parts => parts.Length == 2 && parts[1].StartsWith("refs/tags/v", StringComparison.Ordinal))
            .Select(parts => SemanticVersion.TryParse(parts[1]["refs/tags/v".Length..].Trim(), out var version) ? version : null)
            .Where(version => version is { IsPrerelease: false })
            .Select(version => version!)
            .OrderDescending()
            .ToList();
    }

    public async Task<string?> ReadFileAsync(SemanticVersion version, string path, CancellationToken cancellationToken)
    {
        var tag = $"v{version}";
        workspace ??= await CreateWorkspaceAsync(cancellationToken);
        if (!fetched.Contains(tag))
        {
            var (fetchExit, _, fetchError) = await GitAsync(workspace, cancellationToken, "fetch", "--quiet", "--depth", "1", source, $"refs/tags/{tag}:refs/tags/{tag}");
            if (fetchExit != 0)
            {
                if (fetchError.Contains("couldn't find remote ref", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                throw new ReleaseSourceException($"could not fetch {tag} from {source}: {FirstLine(fetchError)}");
            }

            fetched.Add(tag);
        }

        var (showExit, contents, _) = await GitAsync(workspace, cancellationToken, "show", $"{tag}:{path}");
        return showExit == 0 ? contents : null;
    }

    public void Dispose()
    {
        if (workspace is not null && Directory.Exists(workspace))
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private async Task<string> CreateWorkspaceAsync(CancellationToken cancellationToken)
    {
        var directory = Directory.CreateTempSubdirectory("secondkey-drift-").FullName;
        var (exitCode, _, error) = await GitAsync(directory, cancellationToken, "init", "--quiet");
        if (exitCode != 0)
        {
            throw new ReleaseSourceException($"could not initialise a git workspace: {FirstLine(error)}");
        }

        return directory;
    }

    private async Task<(int ExitCode, string Output, string Error)> GitAsync(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // Never wait for a credential prompt: a source that needs one is reported as unreachable.
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";

        Process process;
        try
        {
            process = Process.Start(start) ?? throw new ReleaseSourceException("git could not be started");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new ReleaseSourceException($"git is not available: {ex.Message}");
        }

        using (process)
        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            deadline.CancelAfter(timeout);

            // Both streams are drained concurrently, so neither pipe can fill and stall the process.
            var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
            var error = process.StandardError.ReadToEndAsync(deadline.Token);
            try
            {
                await process.WaitForExitAsync(deadline.Token);
                return (process.ExitCode, await output, await error);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                throw new ReleaseSourceException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"git {arguments[0]} against {source} did not finish within {timeout.TotalSeconds:0} seconds"));
            }
        }
    }

    private static string FirstLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "no error output";
}
