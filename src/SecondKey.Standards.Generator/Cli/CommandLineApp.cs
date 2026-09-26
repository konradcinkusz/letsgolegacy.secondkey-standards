using System.CommandLine;
using SecondKey.Standards.Generator.Model;

namespace SecondKey.Standards.Generator.Cli;

/// <summary>The command-line surface. Each command is a thin adapter over a class the tests call directly.</summary>
public static class CommandLineApp
{
    public static int Run(string[] args, CliContext context)
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

        var root = new RootCommand("Second Key standards: validate the rules and compile them for the agent and the gate.")
        {
            validate,
            generate,
        };

        var result = root.Parse(args);
        if (result.Errors.Count > 0)
        {
            foreach (var error in result.Errors)
            {
                context.Error.WriteLine($"error: {error.Message}");
            }

            context.Error.WriteLine("Run with --help for usage.");
            return ExitCodes.CouldNotRun;
        }

        return result.Invoke(new InvocationConfiguration { Output = context.Output, Error = context.Error });
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
