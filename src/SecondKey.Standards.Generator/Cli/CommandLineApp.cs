using System.CommandLine;
using SecondKey.Standards.Generator.Drift;
using SecondKey.Standards.Generator.Model;

namespace SecondKey.Standards.Generator.Cli;

/// <summary>The command-line surface. Each command is a thin adapter over a class the tests call directly.</summary>
public static class CommandLineApp
{
    public static async Task<int> RunAsync(string[] args, CliContext context)
    {
        var rootOption = new Option<DirectoryInfo?>("--root")
        {
            Description = "The standards repository. Defaults to the nearest directory at or above the current "
                + "one that contains catalog/pack.json.",
        };

        var validate = new Command(
            "validate",
            "Check every rule in standards/: front matter, vocabularies, file names, and a rationale with a "
            + "non-compliant and a compliant example.")
        {
            rootOption,
        };
        validate.SetAction(parseResult => Validate(context, parseResult.GetValue(rootOption)));

        var checkOption = new Option<bool>("--check")
        {
            Description = "Write nothing; fail if generated/ differs from what the sources produce. This is what CI runs.",
        };

        var generate = new Command(
            "generate",
            "Compile standards/ into generated/: the agent skill, .editorconfig and .globalconfig, the NuGet package "
            + "project, and the manifest. Refuses a content change to a released version.")
        {
            rootOption,
            checkOption,
        };
        generate.SetAction(parseResult => Generate(context, parseResult.GetValue(rootOption), parseResult.GetValue(checkOption)));

        var driftCheck = DriftCheckCommand(context);

        var root = new RootCommand("Second Key standards: validate the rules, compile them for the agent and the gate, "
            + "and check a consuming repository's pinned version.")
        {
            validate,
            generate,
            driftCheck,
        };

        var result = root.Parse(args);
        if (result.Errors.Count > 0)
        {
            foreach (var error in result.Errors)
            {
                await context.Error.WriteLineAsync($"error: {error.Message}");
            }

            await context.Error.WriteLineAsync("Run with --help for usage.");
            return ExitCodes.CouldNotRun;
        }

        return await result.InvokeAsync(new InvocationConfiguration { Output = context.Output, Error = context.Error });
    }

    private static Command DriftCheckCommand(CliContext context)
    {
        var repoOption = new Option<string?>("--repo")
        {
            Description = "The consuming repository to check. Defaults to the current directory.",
        };
        var sourceOption = new Option<string>("--source")
        {
            Description = "Where releases are read from: the standards repository's git URL or a local path. "
                + "Releases are its tags v<MAJOR.MINOR.PATCH>.",
            DefaultValueFactory = _ => DriftDefaults.Source,
        };
        var packageOption = new Option<string>("--package-id")
        {
            Description = "The NuGet package that carries the gate configuration.",
            DefaultValueFactory = _ => DriftDefaults.PackageId,
        };
        var skillOption = new Option<string>("--skill-name")
        {
            Description = "The installed skill's directory name under .github/skills/ (or .github/upgrades/skills/, "
                + ".claude/skills/, .agents/skills/).",
            DefaultValueFactory = _ => DriftDefaults.SkillName,
        };
        var pinnedOption = new Option<string?>("--pinned-version")
        {
            Description = "Use this pinned version instead of reading the repository's package reference and skill.",
        };
        var latestOption = new Option<string?>("--latest-version")
        {
            Description = "Use this as the latest release instead of listing the source's tags (for an offline or mirrored setup).",
        };

        var command = new Command(
            "drift-check",
            "Fail when a consuming repository's pinned standards version is behind the latest release (printing what "
            + "changed), or when its skill and its package pin different versions.")
        {
            repoOption,
            sourceOption,
            packageOption,
            skillOption,
            pinnedOption,
            latestOption,
        };

        command.SetAction((parseResult, cancellationToken) => DriftCheckAsync(
            context,
            parseResult.GetValue(repoOption),
            parseResult.GetValue(sourceOption)!,
            parseResult.GetValue(packageOption)!,
            parseResult.GetValue(skillOption)!,
            parseResult.GetValue(pinnedOption),
            parseResult.GetValue(latestOption),
            cancellationToken));
        return command;
    }

    private static async Task<int> DriftCheckAsync(
        CliContext context,
        string? repo,
        string source,
        string packageId,
        string skillName,
        string? pinned,
        string? latest,
        CancellationToken cancellationToken)
    {
        var reporter = context.CreateReporter();
        var repository = Path.GetFullPath(repo ?? ".", context.CurrentDirectory);
        if (!Directory.Exists(repository))
        {
            reporter.Fail($"the repository to check, {repository}, does not exist");
            return ExitCodes.CouldNotRun;
        }

        SemanticVersion? pinnedVersion = null;
        SemanticVersion? latestVersion = null;
        if ((pinned is not null && !SemanticVersion.TryParse(pinned, out pinnedVersion))
            || (latest is not null && !SemanticVersion.TryParse(latest, out latestVersion)))
        {
            reporter.Fail("--pinned-version and --latest-version take a MAJOR.MINOR.PATCH version");
            return ExitCodes.CouldNotRun;
        }

        using var releases = new GitReleaseSource(ResolveSource(source, context.CurrentDirectory));
        var result = await DriftChecker.CheckAsync(
            new DriftOptions(repository, packageId, skillName, pinnedVersion, latestVersion),
            releases,
            cancellationToken);

        await DriftReport.WriteAsync(result, context, reporter, releases.Description);
        return result.ExitCode;
    }

    /// <summary>A URL (https, ssh, git, file, or scp-style) is passed to git as is; anything else is a local path.</summary>
    internal static string ResolveSource(string source, string currentDirectory)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" or "ssh" or "git" or "file")
        {
            return source;
        }

        var scpLike = source.Contains('@', StringComparison.Ordinal) && source.Contains(':', StringComparison.Ordinal);
        return scpLike ? source : Path.GetFullPath(source, currentDirectory);
    }

    private static int Validate(CliContext context, DirectoryInfo? rootDirectory)
    {
        var reporter = context.CreateReporter();
        if (!TryOpen(context, rootDirectory, reporter, out var layout, out var pack))
        {
            return ExitCodes.CouldNotRun;
        }

        var standards = StandardsLoader.Load(layout, pack);
        if (!standards.IsValid)
        {
            reporter.Problems(standards.Problems);
            reporter.Info($"{standards.Problems.Count} problem(s) in {layout.Relative(layout.StandardsDirectory)}/.");
            return ExitCodes.CheckFailed;
        }

        var mapped = standards.Rules.Count(rule => rule.Diagnostics.Count > 0);
        reporter.Info($"{standards.Rules.Count} rules are valid ({mapped} mapped to gate diagnostics).");
        return ExitCodes.Success;
    }

    private static int Generate(CliContext context, DirectoryInfo? rootDirectory, bool check)
    {
        var reporter = context.CreateReporter();
        if (!TryOpen(context, rootDirectory, reporter, out var layout, out var pack))
        {
            return ExitCodes.CouldNotRun;
        }

        var result = Generation.PackGenerator.Build(layout, pack);
        if (!result.Succeeded)
        {
            reporter.Problems(result.Problems);
            reporter.Info($"{result.Problems.Count} problem(s); nothing was generated.");
            return ExitCodes.CheckFailed;
        }

        var tree = result.Tree!;
        if (check)
        {
            var differences = Generation.TreeSync.Compare(layout, tree);
            if (differences.Count == 0)
            {
                reporter.Info($"{Generation.GeneratedTree.Directory}/ is up to date ({tree.Files.Count} files, standards {pack.Version}).");
                return ExitCodes.Success;
            }

            reporter.Problems(differences
                .Select(difference => new Diagnostics.Problem(
                    difference.Path,
                    null,
                    $"{difference.Kind.ToString().ToLowerInvariant()}: the committed file does not match the sources"))
                .ToList());
            reporter.Info($"{Generation.GeneratedTree.Directory}/ is out of date ({differences.Count} file(s)). Run: secondkey-standards generate");
            return ExitCodes.CheckFailed;
        }

        var (written, removed) = Generation.TreeSync.Write(layout, tree);
        reporter.Info($"Generated standards {pack.Version}: {tree.Files.Count} files ({written} written, {removed} removed).");
        return ExitCodes.Success;
    }

    internal static bool TryOpen(
        CliContext context,
        DirectoryInfo? rootDirectory,
        Reporter reporter,
        out RepositoryLayout layout,
        out PackConfig pack)
    {
        layout = null!;
        pack = null!;

        var discovered = RepositoryLayout.Discover(rootDirectory?.FullName, context.CurrentDirectory);
        if (discovered is null)
        {
            reporter.Fail($"no {RepositoryLayout.PackConfigRelativePath} found at or above {context.CurrentDirectory}; pass --root");
            return false;
        }

        try
        {
            pack = PackConfig.Load(discovered.PackConfigPath);
        }
        catch (PackConfigException ex)
        {
            reporter.Fail(ex.Message);
            return false;
        }

        layout = discovered;
        return true;
    }
}
